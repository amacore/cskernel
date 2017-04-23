using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace CSKernel
{
    class Program
    {
        static void Main(string[] args)
        {
            var file = JsonConvert.DeserializeObject<ConnectionFile>(File.ReadAllText(args[0]));

            var source = new CancellationTokenSource();

            var hbTask = new Task(() => ListenHeartbeat(file, source.Token));
            hbTask.Start();

            var ioEndpoint = $"{file.Transport}://{file.IP}:{file.IOPubPort}";
            var shellEndpoint = $"{file.Transport}://{file.IP}:{file.ShellPort}";


        }

        private static void ListenHeartbeat(ConnectionFile file, CancellationToken ct)
        {
            var heatbeatEndpoint = $"{file.Transport}://{file.IP}:{file.HBPort}";
            using (var responseSocket = new ResponseSocket())
            {
                responseSocket.Bind(heatbeatEndpoint);
                while (!ct.IsCancellationRequested)
                {
                    var message = responseSocket.ReceiveFrameString();
                    responseSocket.SendFrame(message);
                }
            }
        }
    }
}
