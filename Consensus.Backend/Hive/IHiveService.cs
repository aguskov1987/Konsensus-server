using System.Threading.Tasks;
using Consensus.Backend.DTOs.Outgoing;

namespace Consensus.Backend.Hive
{
    public interface IHiveService
    {
        Task<StatementDto> CreateNewStatement(string userId, string statement, string[] supportingLinks,
            string hiveId, string statementCollectionId);
        
        Task<StatementDto[]> FindStatements(string phrase, string identifier);
        
        Task<SubGraph> LoadSubgraph(string statementId);
        
        /// <summary>
        /// Mark a user as a participant. A participant is somebody who:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Created a hive</term>
        ///     </item>
        ///     <item>
        ///         <term>Created a statement or aa effect</term>
        ///     </item>
        ///     <item>
        ///         <term>Responded to a statement or an effect</term>
        ///     </item>
        /// </list>
        /// Participation is tracked on per-day basis. That is, every day there is a new set which
        /// holds participation records so trends can later be analyzed.
        /// </summary>
        /// <param name="hiveId">Hive ID</param>
        /// <param name="userId">User ID</param>
        /// <returns></returns>
        Task MarkUserAsParticipant(string hiveId, string userId);
        
        /// <summary>
        /// Update the number of statements in the hive manifest if a new statement is added
        /// </summary>
        /// <param name="hiveId">Hive ID</param>
        Task BumpHiveStatementCount(string hiveId);
    }
}