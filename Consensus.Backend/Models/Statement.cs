namespace Consensus.Backend.Models
{
    public class Statement
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string[] Links { get; set; }
        
        public Response[] Responses { get; set; }
    }
}