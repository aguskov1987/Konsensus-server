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
        /// From -1 (all disagree) to 1 (all agree). 
        /// </summary>
        public float CommonResponse { get; set; }
        /// <summary>
        /// # of responses / # of total participants. A participant is a user who:
        /// - create the hive
        /// - left a response
        /// - created a statement
        /// - created an effect
        /// - saved the hive
        /// - saved a statement from the hive
        /// </summary>
        public float Penetration { get; set; }
    }
}