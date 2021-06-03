using Newtonsoft.Json;

namespace Consensus.Backend.Models
{
    public class User
    {
        [JsonProperty("_id")]
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CurrentHiveId { get; set; }
    }
}