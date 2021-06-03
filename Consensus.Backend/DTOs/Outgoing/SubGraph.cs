using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Consensus.Backend.DTOs.Outgoing
{
    public class SubGraph
    {
        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (Points == null)
            {
                Points = new List<PointDto>();
            }

            if (Synapses == null)
            {
                Synapses = new List<SynapseDto>();
            }
        }

        public List<PointDto> Points { get; set; }
        public List<SynapseDto> Synapses { get; set; }
        public PointDto Origin { get; set; }
    }
}