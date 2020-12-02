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
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            cmd.HelpOption("-?|-h|--help");
            var hostArg = cmd.Argument("host", isZh ? "指定的目标主机地址。" : "Target host address");
            var portArg = cmd.Argument("port", isZh ? "指定的目标主机端口。" : "Target host port");
            var tOption = cmd.Option<string>("-t",
                isZh ? "Tcping 指定的主机，直到键入 Ctrl+C 停止。" : "Tcping target host until you type Ctrl+C to stop.",
                CommandOptionType.NoValue);
            var aOption = cmd.Option("-a|--async",
                isZh ? "Async Tcping 指定的主机，异步快速模式。" : "Async Tcping target host, asynchronous fast mode.",
                CommandOptionType.NoValue);
            var nOption = cmd.Option<int>("-n|-c|--count <count>",
                isZh ? "要发送的回显请求数。" : "Number of echo requests to send", CommandOptionType.SingleValue);
            var iOption = cmd.Option<int>("-i <time>", isZh ? "要发送的请求间隔时间。" : "Request interval to send",
                CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待每次回复的超时时间(毫秒)。" : "Timeout time to wait for each reply", CommandOptionType.SingleValue);
            var ipv4Option = cmd.Option("-4", isZh ? "强制使用 IPv4。" : "Forced IPv4", CommandOptionType.NoValue);
            var ipv6Option = cmd.Option("-6", isZh ? "强制使用 IPv6。" : "Forced IPv6", CommandOptionType.NoValue);
            var dateOption = cmd.Option("-d", isZh ? "显示响应时间戳。" : "Display response timestamp",
                CommandOptionType.NoValue);
            var stopOption = cmd.Option("-s", isZh ? "在收到响应时停止。" : "Stop on receipt of response",
                CommandOptionType.NoValue);
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
                    Console.WriteLine(isZh ? "指定的目标主机地址不应该为空。" : "The target host address should not be empty.");
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
                    Console.WriteLine(isZh
                        ? "请求找不到目标主机。请检查该名称，然后重试。"
                        : "The request could not find the target host. Please check the name and try again");
                    Environment.Exit(0);
                }

                Console.WriteLine();
                Console.WriteLine(
                    string.Format(isZh ? "正在 Tcping {0}:{1} 目标主机" : "Tcping {0}:{1} target host in progress.",
                        point.Address, point.Port) +
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
                                Blocking = false,
                                ReceiveTimeout = wOption.HasValue() ? wOption.ParsedValue : 2000,
                                SendTimeout = wOption.HasValue() ? wOption.ParsedValue : 2000
                            };
                            socks.BeginConnect(point, null, null).AsyncWaitHandle.WaitOne(500);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
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
                        Console.WriteLine(
                            isZh
                                ? "来自 {0}:{1} 的 TCP 响应: 端口={2} 时间={3}ms"
                                : "TCP response from {0}:{1}: port={2} time={3}ms", 
                            point.Address, point.Port, conn, time);
                    });
                    if (aOption.HasValue()) tasks.Add(t);
                    else t.Wait(wOption.HasValue() ? wOption.ParsedValue + 1000 : 3000);
                }

                if (aOption.HasValue()) Task.WaitAll(tasks.ToArray());

                Thread.Sleep(100);
                Console.WriteLine();
                Console.WriteLine(isZh ? "{0}:{1} 的 Tcping 统计信息:" : "Tcping statistics for {0}:{1}:", 
                    point.Address, point.Port);
                Console.WriteLine(
                    isZh
                        ? "    连接: 已发送 = {0}，已接收 = {1}，失败 = {2} ({3:0%} 失败)"
                        : "    Connection: Sent = {0}, Received = {1}, Failed = {2} ({3:0%} Failed)", 
                    sent.Count, times.Count, errors.Count, errors.Count / (double) sent.Count);
                if (times.Count <= 0) return;
                Console.WriteLine(isZh ? "往返行程的估计时间(以毫秒为单位):" : "Time (in milliseconds) for a round trip:");
                Console.WriteLine(
                    isZh
                        ? "    最短 = {0:0.0}ms，最长 = {1:0.0}ms，平均 = {2:0.0}ms"
                        : "    Shortest = {0:0.0}ms, Longest = {1:0.0}ms, Average = {2:0.0}ms.", 
                    times.Min(), times.Max(), times.Average());
                Console.WriteLine();
            });

            Console.CancelKeyPress += (sender, e) =>
            {
                Thread.Sleep(500);
                if (point == null) return;
                Console.WriteLine();
                Console.WriteLine(isZh ? "{0}:{1} 的 Tcping 统计信息:" : "Tcping statistics for {0}:{1}:",
                    point.Address, point.Port);
                Console.WriteLine(
                    isZh
                        ? "    连接: 已发送 = {0}，已接收 = {1}，失败 = {2} ({3:0%} 失败)"
                        : "    Connection: Sent = {0}, Received = {1}, Failed = {2} ({3:0%} Failed)",
                    sent.Count, times.Count, errors.Count, errors.Count / (double)sent.Count);
                if (times.Count <= 0) return;
                Console.WriteLine(isZh ? "往返行程的估计时间(以毫秒为单位):" : "Time (in milliseconds) for a round trip:");
                Console.WriteLine(
                    isZh
                        ? "    最短 = {0:0.0}ms，最长 = {1:0.0}ms，平均 = {2:0.0}ms"
                        : "    Shortest = {0:0.0}ms, Longest = {1:0.0}ms, Average = {2:0.0}ms.",
                    times.Min(), times.Max(), times.Average());
                Console.WriteLine();
            };

            cmd.Execute(args);
        }
    }
}
