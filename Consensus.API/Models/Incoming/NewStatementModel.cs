namespace Consensus.API.Models.Incoming
{
    public class NewStatementModel
    {
        public string HiveId { get; set; }
        public string StatementCollectionId { get; set; }
        public string Statement { get; set; }
        public string[] SupportingLinks { get; set; }
    }
}