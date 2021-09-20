using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Consensus.Backend.Models
{
    public class ParticipationCount
    {
        public DateTime Date { get; set; }
        public int NumberOfParticipants { get; set; }
    }

    public class PointCount
    {
        public int Count { get; set; }
        public DateTime Date { get; set; }
    }

    public class HiveManifest
    {
        public bool AllowDanglingPoints { get; set; }
        public string CollectionId { get; set; }
        public List<ParticipationCount> DailyParticipation { get; set; }
        public List<PointCount> DailyPointCount { get; set; }
        public DateTime DateCreated { get; set; }
        public string Description { get; set; }

        [JsonProperty("_id")]
        public string Id { get; set; }
        public DateTime TimeOfLastParticipation { get; set; }
        public string Title { get; set; }
        public int Total { get; set; }
        public int TotalParticipation { get; set; }
        public int TotalPoints { get; set; }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (DailyParticipation == null)
            {
                DailyParticipation = new List<ParticipationCount>();
            }

            if (DailyPointCount == null)
            {
                DailyPointCount = new List<PointCount>();
            }
        }
    }
}