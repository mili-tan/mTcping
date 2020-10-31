using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
            var hostArgument = cmd.Argument("host", "指定的目标主机地址");
            var tOption = cmd.Option<string>("-t", "Tcping 指定的主机，直到键入 Ctrl+C 停止。", CommandOptionType.NoValue);
            var nOption = cmd.Option<int>("-n <count>", "要发送的回显请求数。", CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>", "等待每次回复的超时时间(毫秒)。", CommandOptionType.SingleValue);
            cmd.OnExecute(() =>
            {
                if (string.IsNullOrWhiteSpace(hostArgument.Value))
                {
                    Console.WriteLine("指定的目标主机地址不应该为空");
                    return;
                }

                var host = hostArgument.Value.Contains("://")
                    ? new Uri(hostArgument.Value)
                    : new Uri("http://" + hostArgument.Value);
                var point = host.HostNameType == UriHostNameType.Dns
                    ? new IPEndPoint(Dns.GetHostAddresses(host.Host).FirstOrDefault(), host.Port)
                    : new IPEndPoint(IPAddress.Parse(host.Host), host.Port);
                Console.WriteLine($"正在 Tcping {point.Address}:{point.Port} 目标主机" +
                                  (host.HostNameType == UriHostNameType.Dns ? $" [{host}]" : string.Empty) + ":");

                var times = new List<int>();
                var errors = new List<int>();
                for (var i = 0;
                    i < (nOption.HasValue() ? nOption.ParsedValue : tOption.HasValue() ? int.MaxValue : 4);
                    i++)
                {
                    Task.Run(() =>
                    {
                        var stopWatch = new Stopwatch();
                        var conn = true;
                        stopWatch.Start();
                        try
                        {
                            var socks = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                                { Blocking = false, ReceiveTimeout = 3000, SendTimeout = 3000 };
                            var result = socks.BeginConnect(point, null, null);
                            if (!result.AsyncWaitHandle.WaitOne(3000, true))
                            {
                                errors.Add(0);
                                conn = false;
                            }
                            else
                            {
                                socks.Close(3000);
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            errors.Add(0);
                            conn = false;
                        }
                        stopWatch.Stop();
                        var time = Convert.ToInt32(stopWatch.Elapsed.TotalMilliseconds);
                        if (conn) times.Add(time);
                        Console.WriteLine($"来自 {point.Address}:{point.Port} 的 TCP 响应: 端口={conn} 时间={time}ms");
                    }).Wait(6000);
                }
            });
            cmd.Execute(args);
        }
    }
}
