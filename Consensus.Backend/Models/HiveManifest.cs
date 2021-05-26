using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Consensus.Backend.Models
{
    public class ParticipationCount
    {
        public DateTime Date { get; set; }
        public int NumberOfParticipants { get; set; }
    }
    
    public class StatementCount
    {
        public DateTime Date { get; set; }
        public int NumberOfStatements { get; set; }
    }
    
    public class HiveManifest
    {
        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (Participation == null)
            {
                Participation = new List<ParticipationCount>();
            }

            if (NumberOfStatements == null)
            {
                NumberOfStatements = new List<StatementCount>();
            }
        }
        public string _id { get; set; }
        public string CollectionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<ParticipationCount> Participation { get; set; }
        public List<StatementCount> NumberOfStatements { get; set; }
    }
}