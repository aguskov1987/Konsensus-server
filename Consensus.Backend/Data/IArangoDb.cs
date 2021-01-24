using ArangoDBNetStandard;

namespace Consensus.Backend.Data
{
    public interface IArangoDb
    {
        ArangoDBClient GetClient();
    }
}