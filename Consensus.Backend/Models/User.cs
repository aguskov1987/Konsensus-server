namespace Consensus.Backend.Models
{
    public class User
    {
        public string _id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CurrentHiveId { get; set; }
    }
}