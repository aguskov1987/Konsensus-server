using System;

namespace Consensus.Backend.Models
{
    public class Response
    {
        public DateTime Time { get; set; }
        public string UserId { get; set; }
        public bool Agrees { get; set; }
    }
}