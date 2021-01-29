using System.Threading.Tasks;
using Consensus.Backend.DTOs.Outgoing;

namespace Consensus.Backend.Hive
{
    public interface IHiveService
    {
        Task<StatementDto> CreateNewStatement(string userId, string statement, string hiveId, string statementCollectionId);
    }
}