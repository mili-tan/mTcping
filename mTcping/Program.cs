using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
                Description = "mTcping - A simple Ping over TCP tool" +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License"
            };

            cmd.HelpOption("-?|-h|--help");
            var hostArg = cmd.Argument("host", "指定的目标主机地址。");
            var portArg = cmd.Argument("port", "指定的目标主机端口。");
            var tOption = cmd.Option<string>("-t", "Tcping 指定的主机，直到键入 Ctrl+C 停止。", CommandOptionType.NoValue);
            var aOption = cmd.Option("-a|--async", "Async Tcping 指定的主机，异步快速模式。", CommandOptionType.NoValue);
            var nOption = cmd.Option<int>("-n|-c|--count <count>", "要发送的回显请求数。", CommandOptionType.SingleValue);
            var iOption = cmd.Option<int>("-i <time>", "要发送的请求间隔时间。", CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>", "等待每次回复的超时时间(毫秒)。", CommandOptionType.SingleValue);
            var ipv4Option = cmd.Option("-4", "强制使用 IPv4。", CommandOptionType.NoValue);
            var ipv6Option = cmd.Option("-6", "强制使用 IPv6。", CommandOptionType.NoValue);
            var dateOption = cmd.Option("-d", "显示响应时间戳。", CommandOptionType.NoValue);
            var stopOption = cmd.Option("-s", "在收到响应时停止。", CommandOptionType.NoValue);
            var times = new List<int>();
            var errors = new List<int>();
            var tasks = new List<Task>();
            var sent = new List<int>();
            var breakFlag = false;
            IPEndPoint point = null;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) aOption.ShowInHelpText = false;

            var ip = IPAddress.None;
            cmd.OnExecute(() =>
            {
                if (string.IsNullOrWhiteSpace(hostArg.Value))
                {
                    Console.WriteLine("指定的目标主机地址不应该为空。");
                    cmd.ShowHelp();
                    return;
                }
                var host = hostArg.Value.Contains("://")
                    ? new Uri(hostArg.Value)
                    : new Uri("http://" + hostArg.Value + (!string.IsNullOrWhiteSpace(portArg.Value)
                        ? ":" + portArg.Value
                        : string.Empty));

                if (host.HostNameType == UriHostNameType.Dns)
                {
                    if (ipv4Option.HasValue())
                    {
                        foreach (var hostAddress in Dns.GetHostAddresses(host.Host))
                            if (hostAddress.AddressFamily == AddressFamily.InterNetwork)
                                ip = hostAddress;
                    }
                    else if (ipv6Option.HasValue())
                    {
                        foreach (var hostAddress in Dns.GetHostAddresses(host.Host))
                            if (hostAddress.AddressFamily == AddressFamily.InterNetworkV6)
                                ip = hostAddress;
                    }
                    else ip = Dns.GetHostAddresses(host.Host).FirstOrDefault();
                }
                else
                    ip = IPAddress.Parse(host.Host);

                try
                {
                    point = new IPEndPoint(ip, host.Port);
                }
                catch (Exception)
                {
                    //Console.WriteLine(e); 
                    point = new IPEndPoint(ip, 80);
                    if (hostArg.Value.StartsWith("ssh://")) point.Port = 22;
                }

                if (Equals(ip, IPAddress.None))
                {
                    Console.WriteLine("请求找不到目标主机。请检查该名称，然后重试。");
                    Environment.Exit(0);
                }

                Console.WriteLine();
                Console.WriteLine($"正在 Tcping {point.Address}:{point.Port} 目标主机" +
                                  (host.HostNameType == UriHostNameType.Dns ? $" [{host.Host}]" : string.Empty) + ":");

                for (var i = 0;
                    i < (nOption.HasValue() ? nOption.ParsedValue : tOption.HasValue() ? int.MaxValue : 4);
                    i++)
                {
                    if (breakFlag) break;
                    var i1 = i;

                    var t = Task.Run(() =>
                    {
                        if (!aOption.HasValue()) Thread.Sleep(iOption.HasValue() ? iOption.ParsedValue : 500);
                        else Thread.Sleep(i1 * 10);
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
                            else Task.Run(() => socks.Close(wOption.HasValue() ? wOption.ParsedValue : 2000));
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
                                                if (conn && stopOption.HasValue()) breakFlag = true;
                        if (dateOption.HasValue()) Console.Write(DateTime.Now + " ");
                        Console.WriteLine($"来自 {point.Address}:{point.Port} 的 TCP 响应: 端口={conn} 时间={time}ms");
                    });
                    if (aOption.HasValue()) tasks.Add(t);
                    else t.Wait(wOption.HasValue() ? wOption.ParsedValue + 1000 : 3000);
                }

                if (aOption.HasValue()) Task.WaitAll(tasks.ToArray());

                Thread.Sleep(100);
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
