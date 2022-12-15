using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TPL
{
    public class SequentialScanner : IPScanner
    {
        public Task Scan(IPAddress[] ipAddrs, int[] ports)
        {
            var pingAddresses = new List<Task>();
            foreach (var ipAddr in ipAddrs)
            {
                var pingAddr = Task.Run(() => PingAddr(ipAddr));
                pingAddresses.Add(pingAddr.ContinueWith(alsoPingAddr =>
                {
                    var status = alsoPingAddr.Result;
                    
                    if (status == IPStatus.Success)
                    {
                        foreach (var port in ports)
                        {
                            Task.Factory.StartNew(() => CheckPort(ipAddr, port),
                                TaskCreationOptions.AttachedToParent);
                        }
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion));
            }


            return Task.WhenAll(pingAddresses.ToArray());
        }
        
        private Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            using var ping = new Ping();

            Console.WriteLine($"Pinging {ipAddr}");
            var task = ping.SendPingAsync(ipAddr, timeout);
            var answer = task.ContinueWith(alsoPingAddr =>
            {
                var status = alsoPingAddr.Result.Status;
                Console.WriteLine($"Pinged {ipAddr}: {status}");
                return status;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return answer;
        }

        private static void CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
        {
            Console.WriteLine($"Checking {ipAddr}:{port}");
            var tcpClient = new TcpClient();
            var task = tcpClient.ConnectAsync(ipAddr, port, timeout);
            
            task.ContinueWith(alsoTask =>
            {
                Console.WriteLine($"Checked {ipAddr}:{port} - {alsoTask.Result}");
                tcpClient.Dispose();
            }, TaskContinuationOptions.AttachedToParent);
        }
    }
}
