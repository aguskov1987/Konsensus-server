using System.Threading.Tasks;
using Consensus.Backend.Models;

namespace Consensus.Backend.Yard
{
    public interface IYardService
    {
        Task<HiveManifest> CreateHive(string title, string description, string userId);
        Task<HiveManifest> GetHiveById(string hiveId);
        Task<HiveManifest[]> FindHivesByTitle(string searchPhrase);
        Task<bool> SetHiveAsUsersDefaultHive(string hiveId, string userId);
        Task<bool> AddHiveToUserSavedHives(string hiveId, string userId);
        Task<bool> RemoveHiveFromUserSavedHives(string hiveId, string userId);
        Task<HiveManifest[]> GetSavedHives(string userId);
        Task<HiveManifest[]> LoadMostActiveHives();
    }
}