using System.Threading.Tasks;
using Consensus.Backend.Models;

namespace Consensus.Backend.Saved
{
    public interface ISavedHivesService
    {
        Task<bool> AddHiveToUserSavedHives(string hiveId, string userId, SavedHiveOwnershipType ownership);

        Task<bool> RemoveHiveFromUserSavedHives(string hiveId, string userId);

        Task<HiveManifest[]> GetSavedHives(string userId);
    }
}