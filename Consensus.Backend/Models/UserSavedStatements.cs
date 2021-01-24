namespace Consensus.Backend.Models
{
    public class UserSavedStatements
    {
        public string _id { get; set; }
        public string _from { get; set; }
        public string _to { get; set; }
        public string[] SavedStatements { get; set; }
    }
}