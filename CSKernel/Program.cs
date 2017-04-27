using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CSKernel.IPythonProtocol;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace CSKernel
{
    class Program
    {
        private const string ImplementationName = "IPython";
        private const string ProtocolVersion = "5.0";
        private const string Delimiter = "<IDS|MSG>";

        static void Main(string[] args)
        {
            var file = JsonConvert.DeserializeObject<ConnectionFile>(File.ReadAllText(args[0]));

            var source = new CancellationTokenSource();
            var signatureChecker = HMAC.Create(file.SignatureScheme.Replace("-", "").ToUpperInvariant());
            signatureChecker.Key = Encoding.UTF8.GetBytes(file.Key);

            var hbTask = new Task(() => ListenHeartbeat(file, source.Token));
            hbTask.Start();

            var ioEndpoint = $"{file.Transport}://{file.IP}:{file.IOPubPort}";
            var shellEndpoint = $"{file.Transport}://{file.IP}:{file.ShellPort}";
            var shellToken = source.Token;
            using (var ioSocket = new PublisherSocket())
            using (var shellSocket = new RouterSocket())
            {
                ioSocket.Bind(ioEndpoint);
                shellSocket.Bind(shellEndpoint);

                while (!shellToken.IsCancellationRequested)
                {
                    var message = ReceiveMessage(shellSocket);
                    var messageType = message.Header.MessageType;
                    switch (messageType)
                    {
                        case "kernel_info_request":
                            var kernelInfoReply = new KernelInfoReply()
                            {
                                Implementation = ImplementationName,
                                ImplementationVersion = "0.0.1",
                                ProtocolVersion = ProtocolVersion,
                                LanguangeInfo = new KernelInfoReply.LangInfo
                                {
                                    Name = "csharp",
                                    Version = "7.0",
                                    FileExtension = ".cs",
                                    MimeType = "text/plain"
                                },
                                Banner = "<<< CSharp Kernel >>>"
                            };
                            var replyMessage = GetReplyMessage(message, "kernel_info_reply", kernelInfoReply);
                            replyMessage.HMacSignature = GetSignature(replyMessage, signatureChecker);
                            SendMessage(replyMessage, shellSocket);

                            break;
                        case "shutdown_request":
                            var requestInfo = JsonConvert.DeserializeObject<ShutdownRequest>(message.Content);
                            if (!requestInfo.Restart)
                            {
                                source.Cancel();
                            }
                            var shutdownReplyMessage = GetReplyMessage(message, "shutdown_reply", requestInfo);
                            shutdownReplyMessage.HMacSignature = GetSignature(shutdownReplyMessage, signatureChecker);
                            SendMessage(shutdownReplyMessage, shellSocket);

                            break;
                    }
                }
            }
        }

        private static string GetSignature(Message replyMessage, HashAlgorithm signatureChecker)
        {
            signatureChecker.Initialize();
            foreach (var data in new[]
            {
                JsonConvert.SerializeObject(replyMessage.Header),
                JsonConvert.SerializeObject(replyMessage.ParentHeader),
                JsonConvert.SerializeObject(replyMessage.Metadata),
                replyMessage.Content
            })
            {
                var array = Encoding.UTF8.GetBytes(data);
                signatureChecker.TransformBlock(array, 0, array.Length, null, 0);
            }
            signatureChecker.TransformFinalBlock(new byte[] {}, 0, 0);

            return BitConverter.ToString(signatureChecker.Hash).Replace("-", "").ToLowerInvariant();
        }

        private static void SendMessage(Message message, IOutgoingSocket shellSocket)
        {
            if (message.Identity.Count > 0)
            {
                foreach (var identity in message.Identity)
                {
                    shellSocket.TrySendFrame(identity, true);
                }
            }
            else
            {
                shellSocket.SendFrame(message.UUID, true);
            }
            shellSocket.SendFrame(Delimiter, true);
            shellSocket.SendFrame(message.HMacSignature ?? string.Empty, true);
            shellSocket.SendFrame(JsonConvert.SerializeObject(message.Header), true);
            shellSocket.SendFrame(JsonConvert.SerializeObject(message.ParentHeader), true);
            shellSocket.SendFrame(JsonConvert.SerializeObject(message.Metadata), true);
            shellSocket.SendFrame(message.Content);
        }

        private static Message GetReplyMessage(Message parentMessage, string messageType, object kernelInfoReply)
        {
            var replyMessage = new Message
            {
                Identity = parentMessage.Identity,
                UUID = parentMessage.Header.Session, //todo: should be clarified
                Header = new Header
                {
                    MessageId = Guid.NewGuid().ToString(),
                    MessageType = messageType,
                    Session = parentMessage.Header.Session,
                    Username = ImplementationName,
                    Version = ProtocolVersion
                },
                ParentHeader = parentMessage.Header,
                Content = JsonConvert.SerializeObject(kernelInfoReply)
            };
            return replyMessage;
        }

        private static Message ReceiveMessage(IReceivingSocket shellSocket)
        {
            var message  = new Message();
            var ident = shellSocket.ReceiveFrameBytes();
            var identStr = Encoding.ASCII.GetString(ident);
            message.Identity = new List<byte[]>();
            while (!identStr.Equals(Delimiter))
            {
                message.Identity.Add(ident);
                ident = shellSocket.ReceiveFrameBytes();
                identStr = Encoding.ASCII.GetString(ident);
            }
            message.Delimiter = identStr;
            message.HMacSignature = shellSocket.ReceiveFrameString();
            message.Header = JsonConvert.DeserializeObject<Header>(shellSocket.ReceiveFrameString());
            message.ParentHeader = JsonConvert.DeserializeObject<Header>(shellSocket.ReceiveFrameString());
            message.Metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(shellSocket.ReceiveFrameString());
            message.Content = shellSocket.ReceiveFrameString();

            return message;
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
