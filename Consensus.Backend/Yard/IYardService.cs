using System.Threading.Tasks;
using Consensus.Backend.DTOs.Outgoing;
using Consensus.Backend.Models;

namespace Consensus.Backend.Yard
{
    public interface IYardService
    {
        Task<HiveManifest> CreateHive(string title, string description, string userId, string seed, PointType seedType);
        
        Task<HiveManifestDto> GetHiveById(string hiveId);
        
        Task<bool> SetHiveAsUsersDefaultHive(string hiveId, string userId);

        Task<HivesPagedSet> LoadYard(string query, int page, int perPage, HiveSortingOption sort, HiveOrder order);
    }
}