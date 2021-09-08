using System;
using Newtonsoft.Json;

namespace Consensus.Backend.Models
{
    public enum PointType
    {
        Statement,
        Question
    }
    public class Point
    {
        [JsonProperty("_id")]
        public string Id { get; set; }
        public string Label { get; set; }
        public string[] Links { get; set; }
        public DateTime DateCreated { get; set; }
        public Response[] Responses { get; set; }
        public PointType Type { get; set; }
    }
}