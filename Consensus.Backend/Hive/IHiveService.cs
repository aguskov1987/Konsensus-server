using System.Threading.Tasks;
using Consensus.Backend.DTOs.Outgoing;

namespace Consensus.Backend.Hive
{
    public interface IHiveService
    {
        Task<StatementDto> CreateNewStatement(string userId, string statement, string[] supportingLinks,
            string hiveId, string statementCollectionId);
        Task<StatementDto[]> FindStatements(string phrase, string statementViewId);
        Task<SubGraph> LoadSubgraph(string statementId, string graphId);
    }
}