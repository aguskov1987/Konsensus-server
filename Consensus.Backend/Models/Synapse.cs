using Newtonsoft.Json;

namespace Consensus.Backend.Models
{
    public class Synapse
    {
        [JsonProperty("_id")]
        public string Id { get; set; }
        [JsonProperty("_from")]
        public string From { get; set; }
        [JsonProperty("_to")]
        public string To { get; set; }
        
        public Response[] Responses { get; set; }
    }
}