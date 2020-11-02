using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace mTcping
{
    class Program
    {
        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "mTcping",
                Description = "mTcping - A simple ping-over-tcp tool" +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the Mozilla Public License 2.0"
            };

            cmd.HelpOption("-?|-h|--help");
            var hostArgument = cmd.Argument("host", "指定的目标主机地址。");
            var portArgument = cmd.Argument("port", "指定的目标主机端口。");
            var tOption = cmd.Option<string>("-t", "Tcping 指定的主机，直到键入 Ctrl+C 停止。", CommandOptionType.NoValue);
            var aOption = cmd.Option<string>("-a|--async", "Async Tcping 指定的主机，异步快速模式。", CommandOptionType.NoValue);
            var nOption = cmd.Option<int>("-n <count>", "要发送的回显请求数。", CommandOptionType.SingleValue);
            var iOption = cmd.Option<int>("-i <time>", "要发送的请求间隔时间。", CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>", "等待每次回复的超时时间(毫秒)。", CommandOptionType.SingleValue);
            var times = new List<int>();
            var errors = new List<int>();
            var tasks = new List<Task>();
            var sent = new List<int>();
            IPEndPoint point = null;

            cmd.OnExecute(() =>
            {
                var host = hostArgument.Value.Contains("://")
                    ? new Uri(hostArgument.Value)
                    : new Uri("http://" + hostArgument.Value + (!string.IsNullOrWhiteSpace(portArgument.Value)
                        ? ":" + portArgument.Value
                        : string.Empty));
                point = host.HostNameType == UriHostNameType.Dns
                    ? new IPEndPoint(Dns.GetHostAddresses(host.Host).FirstOrDefault(), host.Port)
                    : new IPEndPoint(IPAddress.Parse(host.Host), host.Port);
                if (string.IsNullOrWhiteSpace(hostArgument.Value))
                {
                    Console.WriteLine("指定的目标主机地址不应该为空");
                    return;
                }

                Console.WriteLine();
                Console.WriteLine($"正在 Tcping {point.Address}:{point.Port} 目标主机" +
                                  (host.HostNameType == UriHostNameType.Dns ? $" [{host.Host}]" : string.Empty) + ":");

                for (var i = 0;
                    i < (nOption.HasValue() ? nOption.ParsedValue : tOption.HasValue() ? int.MaxValue : 4);
                    i++)
                {
                    var i1 = i;
                    var t = Task.Run(() =>
                    {
                        if (!aOption.HasValue()) Thread.Sleep(iOption.HasValue() ? iOption.ParsedValue : 500);
                        else Thread.Sleep(i1 * 50);
                        var stopWatch = new Stopwatch();
                        var conn = true;
                        stopWatch.Start();
                        sent.Add(0);
                        try
                        {
                            var socks = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                            {
                                Blocking = false, ReceiveTimeout = wOption.HasValue() ? wOption.ParsedValue : 2000,
                                SendTimeout = wOption.HasValue() ? wOption.ParsedValue : 2000
                            };
                            var result = socks.BeginConnect(point, null, null);
                            if (!result.AsyncWaitHandle.WaitOne(wOption.HasValue() ? wOption.ParsedValue : 2000, true))
                            {
                                errors.Add(0);
                                conn = false;
                            }
                            else socks.Close(wOption.HasValue() ? wOption.ParsedValue : 2000);
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception.Message);
                            errors.Add(0);
                            conn = false;
                        }

                        stopWatch.Stop();
                        var time = Convert.ToInt32(stopWatch.Elapsed.TotalMilliseconds);
                        if (conn) times.Add(time);
                        Console.WriteLine($"来自 {point.Address}:{point.Port} 的 TCP 响应: 端口={conn} 时间={time}ms");
                    });
                    if (aOption.HasValue()) tasks.Add(t);
                    else t.Wait(wOption.HasValue() ? wOption.ParsedValue + 1000 : 3000);
                }

                if (aOption.HasValue()) Task.WaitAll(tasks.ToArray());

                Thread.Sleep(500);
                Console.WriteLine();
                Console.WriteLine($"{point.Address}:{point.Port} 的 Tcping 统计信息:");
                Console.WriteLine(
                    $"    连接: 已发送 = {sent.Count}，已接收 = {times.Count}，失败 = {errors.Count} ({errors.Count / (double) sent.Count:0%} 失败)");
                if (times.Count <= 0) return;
                Console.WriteLine("往返行程的估计时间(以毫秒为单位):");
                Console.WriteLine(
                    $"    最短 = {times.Min():0.0}ms，最长 = {times.Max():0.0}ms，平均 = {times.Average():0.0}ms");
                Console.WriteLine();
            });

            Console.CancelKeyPress += (sender, e) =>
            {
                Thread.Sleep(500);
                if (point == null) return;
                Console.WriteLine();
                Console.WriteLine($"{point.Address}:{point.Port} 的 Tcping 统计信息:");
                Console.WriteLine(
                    $"    连接: 已发送 = {sent.Count}，已接收 = {times.Count}，失败 = {errors.Count} ({errors.Count / (double) sent.Count:0%} 失败)");
                if (times.Count <= 0) return;
                Console.WriteLine("往返行程的估计时间(以毫秒为单位):");
                Console.WriteLine(
                    $"    最短 = {times.Min():0.0}ms，最长 = {times.Max():0.0}ms，平均 = {times.Average():0.0}ms");
                Console.WriteLine();
            };

            cmd.Execute(args);
        }
    }
}
