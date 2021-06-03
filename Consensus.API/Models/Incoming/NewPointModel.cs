namespace Consensus.API.Models.Incoming
{
    public class NewPointModel
    {
        public string HiveId { get; set; }
        public string Identifier { get; set; }
        public string Point { get; set; }
        public string[] SupportingLinks { get; set; }
    }
}