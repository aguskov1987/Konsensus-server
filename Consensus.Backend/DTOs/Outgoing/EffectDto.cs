namespace Consensus.Backend.DTOs.Outgoing
{
    public class EffectDto
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        
        public int MyResponse { get; set; }
        public float CommonResponse { get; set; }
        public float Penetration { get; set; }
    }
}