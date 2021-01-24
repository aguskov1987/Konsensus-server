namespace Consensus.Backend.Models
{
    public class HiveManifest
    {
        public string _id { get; set; }
        
        public string StatementCollection { get; set; }
        public string SynapseCollection { get; set; }
        
        public string Title { get; set; }
        public string Description { get; set; }
    }
}