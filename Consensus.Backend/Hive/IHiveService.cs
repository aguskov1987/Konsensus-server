using System;
using System.Threading.Tasks;
using Consensus.Backend.DTOs.Outgoing;

namespace Consensus.Backend.Hive
{
    public interface IHiveService
    {
        Task<(PointDto, SynapseDto)> CreateNewPoint(string userId, string point, string[] supportingLinks,
            string hiveId, string identifier, string fromId, string toId);
        
        Task<PointDto[]> FindPoints(string phrase, string identifier, string userId, string hiveId);
        
        Task<SubGraph> LoadSubgraph(string pointId, string userId, string hiveId);
        
        /// <summary>
        /// Mark a user as a participant. A participant is somebody who:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Created a hive</term>
        ///     </item>
        ///     <item>
        ///         <term>Created a point or synapse</term>
        ///     </item>
        ///     <item>
        ///         <term>Responded to a point or synapse</term>
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
        /// Update the number of points in the hive manifest if a new point is added
        /// </summary>
        /// <param name="hiveId">Hive ID</param>
        Task BumpHivePointCount(string hiveId);

        Task<SynapseDto> CreateNewSynapse(string fromId, string toId, string hiveId, string userId);
        
        Task<object> Respond(string itemId, string hiveId, bool agree, string userId);
    }
}