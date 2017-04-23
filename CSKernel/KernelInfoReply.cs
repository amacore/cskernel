using Newtonsoft.Json;

namespace CSKernel
{
    public class KernelInfoReply
    {
        [JsonProperty("protocol_version")]
        public string ProtocolVersion { get; set; }

        [JsonProperty("implementation")]
        public string Implementation { get; set; }

        [JsonProperty("implementation_version")]
        public string ImplementationVersion { get; set; }

        [JsonProperty("language_info")]
        public LangInfo LanguangeInfo { get; set; }

        [JsonProperty("banner")]
        public string Banner { get; set; }

        public class LangInfo
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("mimetype")]
            public string MimeType { get; set; }

            [JsonProperty("file_extension")]
            public string FileExtension { get; set; }
        }
    }
}