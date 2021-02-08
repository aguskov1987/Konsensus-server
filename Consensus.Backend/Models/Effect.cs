namespace Consensus.Backend.Models
{
    public class Effect
    {
        public string _id { get; set; }
        public string _from { get; set; }
        public string _to { get; set; }
        
        public Response[] Responses { get; set; }
    }
}