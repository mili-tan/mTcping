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

namespace mTcping;

class Program
{
    public static List<int> Times = new();
    public static List<int> Errors = new();
    public static List<int> Sends = new();
    public static List<Task> Tasks = new();
    public static IPEndPoint Point = new(IPAddress.Any, 80);
    public static IPAddress IpAddress = IPAddress.None;
    public static bool IsZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
    public static bool BreakFlag;
    public static List<Uri> Hosts = new();


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

        var hostArg = cmd.Argument("host", IsZh ? "指定的目标主机地址。" : "Target host address");
        var portArg = cmd.Argument("port", IsZh ? "指定的目标主机端口。" : "Target host port");
        var tOption = cmd.Option<string>("-t",
            IsZh ? "Tcping 指定的主机，直到键入 Ctrl+C 停止。" : "Tcping target host until you type Ctrl+C to stop",
            CommandOptionType.NoValue);
        var aOption = cmd.Option("-a|--async",
            IsZh ? "Async Tcping 指定的主机，异步快速模式。" : "Async Tcping target host, asynchronous fast mode",
            CommandOptionType.NoValue);
        var nOption = cmd.Option<int>("-n|-c|--count <count>",
            IsZh ? "要发送的回显请求数。" : "Number of echo requests to send", CommandOptionType.SingleValue);
        var iOption = cmd.Option<int>("-i <time>", IsZh ? "要发送的请求间隔时间。" : "Request interval to send",
            CommandOptionType.SingleValue);
        var wOption = cmd.Option<int>("-w <timeout>",
            IsZh ? "等待每次回复的超时时间(毫秒)。" : "Timeout time to wait for each reply", CommandOptionType.SingleValue);
        var ipv4Option = cmd.Option("-4", IsZh ? "强制使用 IPv4。" : "Forced IPv4", CommandOptionType.NoValue);
        var ipv6Option = cmd.Option("-6", IsZh ? "强制使用 IPv6。" : "Forced IPv6", CommandOptionType.NoValue);
        var dateOption = cmd.Option("-d", IsZh ? "显示响应时间戳。" : "Display response timestamp",
            CommandOptionType.NoValue);
        var stopOption = cmd.Option("-s", IsZh ? "在收到响应时停止。" : "Stop on receipt of response",
            CommandOptionType.NoValue);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) aOption.ShowInHelpText = false;

        cmd.OnExecute(() =>
        {
            if (string.IsNullOrWhiteSpace(hostArg.Value))
            {
                Console.WriteLine(IsZh ? "指定的目标主机地址不应该为空。" : "The target host address should not be empty.");
                cmd.ShowHelp();
                return;
            }

            if (hostArg.Value.Contains("/") && IPNetwork.TryParse(hostArg.Value, out var hostNetwork))
                Hosts.AddRange(hostNetwork.ListIPAddress()
                    .Select(address => new Uri("http://" + (address.AddressFamily == AddressFamily.InterNetworkV6
                        ? $"[{address}]"
                        : address) + (!string.IsNullOrWhiteSpace(portArg.Value)
                        ? ":" + portArg.Value
                        : string.Empty))));
            else
                Hosts.Add(hostArg.Value.Contains("://")
                    ? new Uri(hostArg.Value)
                    : new Uri("http://" + (IPAddress.TryParse(hostArg.Value, out var ipAddress) &&
                                           ipAddress.AddressFamily == AddressFamily.InterNetworkV6
                                  ? $"[{ipAddress}]"
                                  : hostArg.Value) +
                              (!string.IsNullOrWhiteSpace(portArg.Value) ? ":" + portArg.Value : string.Empty)));

            foreach (var host in Hosts)
            {
                if (host.HostNameType == UriHostNameType.Dns)
                    if (ipv4Option.HasValue())
                        IpAddress = Dns.GetHostAddresses(host.Host)
                            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                    else if (ipv6Option.HasValue())
                        IpAddress = Dns.GetHostAddresses(host.Host)
                            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6);
                    else IpAddress = Dns.GetHostAddresses(host.Host).FirstOrDefault();
                else
                    IpAddress = IPAddress.Parse(host.Host);

                try
                {
                    Point = new IPEndPoint(IpAddress, host.Port);
                }
                catch (Exception)
                {
                    Point = new IPEndPoint(IpAddress, 80);
                    if (hostArg.Value.StartsWith("ssh://")) Point.Port = 22;
                }

                if (Equals(IpAddress, IPAddress.None))
                {
                    Console.WriteLine(IsZh
                        ? "请求找不到目标主机。请检查该名称，然后重试。"
                        : "The request could not find the target host. Please check the name and try again");
                    Environment.Exit(0);
                }

                if (Hosts.Count == 1)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        string.Format(IsZh ? "正在 Tcping {0}:{1} 目标主机" : "Tcping {0}:{1} target host in progress",
                            Point.Address.AddressFamily == AddressFamily.InterNetworkV6
                                ? $"[{Point.Address}]"
                                : Point.Address, Point.Port) +
                        (host.HostNameType == UriHostNameType.Dns ? $" [{host.Host}]" : string.Empty) + ":");
                }

                var waitTime = wOption.HasValue() ? wOption.ParsedValue : 2000;
                var intervalTime = iOption.HasValue() ? iOption.ParsedValue : 1000;
                var sendCount = nOption.HasValue() ? nOption.ParsedValue :
                    tOption.HasValue() ? int.MaxValue :
                    Hosts.Count > 1 ? 1 : 4;

                try
                {
                    InitSocket(waitTime).BeginConnect(Point, null, null).AsyncWaitHandle.WaitOne(500);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                for (var i = 0; i < sendCount; i++)
                {
                    if (BreakFlag) break;

                    var number = i;
                    var t = Task.Run(() =>
                    {
                        var stopWatch = new Stopwatch();
                        var isConnect = true;

                        if (aOption.HasValue()) Thread.Sleep(number * 10);
                        else Thread.Sleep(intervalTime);

                        stopWatch.Start();
                        Sends.Add(0);

                        try
                        {
                            var socks = InitSocket(waitTime);
                            var result = socks.BeginConnect(Point, null, null);
                            if (!result.AsyncWaitHandle.WaitOne(waitTime, true))
                            {
                                Errors.Add(0);
                                isConnect = false;
                            }
                            else Task.Run(() => socks.Close(waitTime));
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception.Message);
                            Errors.Add(0);
                            isConnect = false;
                        }

                        stopWatch.Stop();

                        var time = Convert.ToInt32(stopWatch.Elapsed.TotalMilliseconds);
                        if (isConnect) Times.Add(time);
                        if (isConnect && stopOption.HasValue()) BreakFlag = true;
                        if (dateOption.HasValue()) Console.Write(DateTime.Now + " ");

                        if (Hosts.Count > 1 && isConnect) Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine(
                            IsZh
                                ? "来自 {0}:{1} 的 TCP 响应: 端口={2} 时间={3}ms"
                                : "TCP response from {0}:{1} Port={2} Time={3}ms",
                            Point.Address.AddressFamily == AddressFamily.InterNetworkV6
                                ? $"[{Point.Address}]"
                                : Point.Address, Point.Port, isConnect, time);
                        Console.ResetColor();
                    });

                    if (aOption.HasValue()) Tasks.Add(t);
                    else t.Wait(waitTime + 1000);
                }

                if (aOption.HasValue()) Task.WaitAll(Tasks.ToArray());

                Thread.Sleep(100);
                if (Hosts.Count == 1) PrintCount();
            }
        });

        Console.CancelKeyPress += (_, _) =>
        {
            Thread.Sleep(500);
            PrintCount();
        };
        cmd.Execute(args);
    }

    public static Socket InitSocket(int timeout)
    {
        return new Socket(Point.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            Blocking = false,
            ReceiveTimeout = timeout,
            SendTimeout = timeout
        };
    }

    public static void PrintCount()
    {
        if (Point == null) return;
        Console.WriteLine();
        Console.WriteLine(IsZh ? "{0}:{1} 的 Tcping 统计信息:" : "Tcping statistics for {0}:{1}",
            (Hosts.Count > 1 ? Hosts.FirstOrDefault().Host + " ~ " : string.Empty) +
            (Point.Address.AddressFamily == AddressFamily.InterNetworkV6
                ? $"[{Point.Address}]"
                : Point.Address), Point.Port);
        Console.WriteLine(
            IsZh
                ? "    连接: 已发送 = {0}，已接收 = {1}，失败 = {2} ({3:0%} 失败)"
                : "    Connection: Sent = {0}, Received = {1}, Failed = {2} ({3:0%} Failed)",
            Sends.Count, Times.Count, Errors.Count, Errors.Count / (double) Sends.Count);
        if (!Times.Any()) return;
        Console.WriteLine(IsZh ? "往返行程的估计时间(以毫秒为单位):" : "Time (in milliseconds) for a round trip:");
        Console.WriteLine(
            IsZh
                ? "    最短 = {0:0.0}ms，最长 = {1:0.0}ms，平均 = {2:0.0}ms"
                : "    Shortest = {0:0.0}ms, Longest = {1:0.0}ms, Average = {2:0.0}ms.",
            Times.Min(), Times.Max(), Times.Average());
        Console.WriteLine();
    }
}