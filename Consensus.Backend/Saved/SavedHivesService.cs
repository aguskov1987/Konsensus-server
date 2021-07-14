using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using Consensus.Backend.Data;
using Consensus.Backend.Models;

namespace Consensus.Backend.Saved
{
    public class SavedHivesService : ISavedHivesService
    {
        private readonly ArangoDBClient _client;

        public SavedHivesService(IArangoDb db)
        {
            _client = db.GetClient();
        }

        #region ISavedHivesService Members

        public async Task<bool> AddHiveToUserSavedHives(string hiveId, string userId, SavedHiveOwnershipType ownership)
        {
            await _client.Document.PostDocumentAsync(
                Connections.UserHasSavedHive.ToString(), new UsersSavedHive
                {
                    From = userId,
                    To = hiveId,
                    OwnershipOwnershipType = ownership
                });

            return true;
        }

        public async Task<bool> RemoveHiveFromUserSavedHives(string hiveId, string userId)
        {
            string query = "FOR link IN @@collection FILTER _from == @userId AND _to == @hiveId";
            var parameters = new Dictionary<string, object>
            {
                ["@collection"] = Connections.UserHasSavedHive.ToString(),
                ["userId"] = userId,
                ["hiveId"] = hiveId
            };
            CursorResponse<UsersSavedHive> result = await _client.Cursor.PostCursorAsync<UsersSavedHive>(query, parameters);
            UsersSavedHive item = result.Result.FirstOrDefault();

            if (item != null)
            {
                string key = item.Id.Split("/")[1];
                await _client.Document.DeleteDocumentAsync<UsersSavedHive>(Connections.UserHasSavedHive.ToString(),
                    key);
                return true;
            }

            throw new FileNotFoundException();
        }

        public async Task<HiveManifest[]> GetSavedHives(string userId)
        {
            string query = @"
            LET hiveIds = (FOR c IN @@userHasSavedHives
                               FILTER c._from == @userId
                               RETURN c._to)

            FOR hive IN @@manifests
                FILTER hive._id IN hiveIds
                RETURN hive";
            
            var parameters = new Dictionary<string, object>
            {
                ["@userHasSavedHives"] = Connections.UserHasSavedHive.ToString(),
                ["@manifests"] = Collections.HiveManifests.ToString(),
                ["userId"] = userId
            };
            
            CursorResponse<HiveManifest> result = await _client.Cursor.PostCursorAsync<HiveManifest>(query, parameters);
            return result.Result.ToArray();
        }

        #endregion
    }
}