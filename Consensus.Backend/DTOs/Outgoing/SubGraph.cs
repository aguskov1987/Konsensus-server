using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Consensus.Backend.DTOs.Outgoing
{
    public class SubGraph
    {
        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (Statements == null)
            {
                Statements = new List<StatementDto>();
            }

            if (Effects == null)
            {
                Effects = new List<EffectDto>();
            }
        }

        public List<StatementDto> Statements { get; set; }
        public List<EffectDto> Effects { get; set; }
    }
}