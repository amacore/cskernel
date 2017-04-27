using Newtonsoft.Json;

namespace CSKernel.IPythonProtocol
{
	public class ShutdownRequest
	{
		[JsonProperty("restart")]
		public bool Restart { get; set; }
	}
}