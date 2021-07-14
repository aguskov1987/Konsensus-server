using Consensus.Backend.Models;

namespace Consensus.API.Models.Incoming
{

    public class YardRequestParams
    {
        public string Query { get; set; }
        public int Page { get; set; }
        public int HivesPerPage { get; set; }
        public HiveSortingOption Sort { get; set; }
        public HiveOrder Order { get; set; }
    }
}