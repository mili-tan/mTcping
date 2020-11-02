using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace mScan
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var aStopwatch = new Stopwatch();
            var hostArg = args.FirstOrDefault() ?? "8.8.8.8";
            var host = hostArg.Contains("://")
                ? new Uri(hostArg)
                : new Uri("http://" + hostArg);
            var point = host.HostNameType == UriHostNameType.Dns
                ? new IPEndPoint(Dns.GetHostAddresses(host.Host).FirstOrDefault(), host.Port)
                : new IPEndPoint(IPAddress.Parse(host.Host), host.Port);
            var tasks = new List<Task>();
            var ports = new List<int>();
            aStopwatch.Start();
            Parallel.For(1, 65535, i =>
            {
                var bgWorker = new BackgroundWorker();
                bgWorker.DoWork += (sender, eventArgs) =>
                {
                    var i1 = i;
                    var t = Task.Run(() =>
                    {
                        var conn = true;
                        var stopWatch = new Stopwatch();
                        stopWatch.Start();
                        try
                        {
                            var socks = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                            {
                                Blocking = false,
                                ReceiveTimeout = 100,
                                SendTimeout = 100
                            };
                            var result = socks.BeginConnect(new IPEndPoint(point.Address, i1), null, null);
                            if (!result.AsyncWaitHandle.WaitOne(100, true)) conn = false;
                            else socks.Close(100);
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception.Message);
                            conn = false;
                        }

                        stopWatch.Stop();
                        var time = Convert.ToInt32(stopWatch.Elapsed.TotalMilliseconds);
                        if (conn)
                        {
                            ports.Add(i1);
                            Console.ForegroundColor = ConsoleColor.Green;
                        }
                        Console.WriteLine($"来自 {point.Address}:{i1} 的 TCP 响应: 端口={conn} 时间={time}ms");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    });
                    tasks.Add(t);
                };
                bgWorker.RunWorkerAsync();
            });

            //while (!parallel.IsCompleted){}

            Task.WaitAll(tasks.ToArray()); 
            aStopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done!");
            Console.WriteLine(string.Join(" ", ports));
            Console.WriteLine(Convert.ToInt32(aStopwatch.Elapsed.TotalSeconds));
        }
    }
}
