namespace Consensus.Backend.DTOs.Outgoing
{
    public class StatementDto
    {
        public string Id { get; set; }
        public string Label { get; set; }
        /// <summary>
        /// The requesting user's response - either 1 or -1 (agree or disagree)
        /// </summary>
        public int MyResponse { get; set; }
        /// <summary>
        /// Either a positive or negative fraction. Represents the response
        /// from all users who responded to the statement
        /// </summary>
        public float CommonResponse { get; set; }
        /// <summary>
        /// Number of responses divided by the number of total participants
        /// </summary>
        public float Penetration { get; set; }
    }
}