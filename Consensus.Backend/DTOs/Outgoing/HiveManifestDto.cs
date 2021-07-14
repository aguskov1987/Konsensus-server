using System;

namespace Consensus.Backend.DTOs.Outgoing
{
    public class HiveManifestDto
    {
        public string Id { get; set; }
        public string CollectionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public int[] ParticipationCount { get; set; }
        public int[] PointCount { get; set; }
        public int TotalParticipation { get; set; }
        public int TotalPoints { get; set; }
        public bool AllowDanglingPoints { get; set; }
    }
}