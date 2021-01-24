namespace Consensus.Backend.Models
{
    public class Effect
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        
        public Response[] Responses { get; set; }
    }
}