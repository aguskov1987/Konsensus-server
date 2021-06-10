namespace Consensus.Backend.DTOs.Outgoing
{
    public class SynapseDto
    {
        public string Id { get; set; }
        
        public string From { get; set; }
        
        public string To { get; set; }
        
        public int UserResponse { get; set; }
        
        public float CommonResponse { get; set; }
        
        public float Penetration { get; set; }
    }
}