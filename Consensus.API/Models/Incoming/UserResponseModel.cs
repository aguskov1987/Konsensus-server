namespace Consensus.API.Models.Incoming
{
    public enum ResponseToType
    {
        Statement, Effect
    }
    public class UserResponseModel
    {
        public string Id { get; set; }
        public ResponseToType Type { get; set; }
        public string CollectionId { get; set; }
        public bool Agree { get; set; }
    }
}