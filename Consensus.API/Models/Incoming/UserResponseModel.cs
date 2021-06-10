namespace Consensus.API.Models.Incoming
{
    public enum ResponseToType
    {
        Point,
        Synapse
    }

    public class UserResponseModel
    {
        public string HiveId { get; set; }
        public string ItemId { get; set; }
        public bool Agree { get; set; }
    }
}