namespace Consensus.Backend.Models
{
    public class UsersSavedHive
    {
        public string _id { get; set; }
        public string _from { get; set; }
        public string _to { get; set; }
        public SavedHiveOwnershipType OwnershipOwnershipType { get; set; }
    }
}