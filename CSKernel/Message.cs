using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CSKernel
{
    public class Message
    {
        public List<byte[]> Identity { get; set; }
        public string UUID { get; set; }
        public string Delimiter { get; set; }
        public string HMacSignature { get; set; }
        public Header Header { get; set; }
        public Header ParentHeader { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public string Content { get; set; }
    }

    public class Header
    {
        [JsonProperty("msg_id")]
        public string MessageId { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("session")]
        public string Session { get; set; }
        [JsonProperty("msg_type")]
        public string MessageType { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
