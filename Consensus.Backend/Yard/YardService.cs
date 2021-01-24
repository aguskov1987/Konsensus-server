using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.DocumentApi.Models;
using ArangoDBNetStandard.GraphApi.Models;
using ArangoDBNetStandard.ViewApi.Models;
using Consensus.Backend.Data;
using Consensus.Backend.Models;

namespace Consensus.Backend.Yard
{
    // TODO: use bound variables in AQL queries
    public class YardService : IYardService
    {
        private readonly ArangoDBClient _client;

        public YardService(IArangoDb db)
        {
            _client = db.GetClient();
        }

        public async Task<HiveManifest> CreateHive(string title, string description, string userId)
        {
            // 1. generate ID
            string identifier = Guid.NewGuid().ToString();
            string collectionName = "St-" + identifier;
            string edgeCollectionName = "Sn-" + identifier;
            string graphName = "G-" + identifier;
            string viewName = "V-" + identifier;

            try
            {
                // 2. create a graph for the new collection
                PostGraphResponse graph = await _client.Graph.PostGraphAsync(new PostGraphBody
                {
                    Name = graphName,
                    OrphanCollections = new List<string>
                    {
                        collectionName
                    },
                    EdgeDefinitions = new List<EdgeDefinition>
                    {
                        new EdgeDefinition
                        {
                            Collection = edgeCollectionName,
                            From = new[] {collectionName},
                            To = new[] {collectionName}
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            try
            {
                // 3. make a new document in the HiveManifests collection
                PostDocumentResponse<HiveManifest> hiveManifest = await _client.Document.PostDocumentAsync(
                    Collections.HiveManifests.ToString(),
                    new HiveManifest
                    {
                        Title = title,
                        Description = description,
                        StatementCollection = collectionName,
                        SynapseCollection = edgeCollectionName
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });

                // 4. create a search view for the new collection
                PostViewResponse view = await _client.View.PostView(new PostViewBody
                {
                    Name = viewName,
                    CollectionName = collectionName,
                    FieldsToIndex = new[] {"label"}
                });

                return hiveManifest.New;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<HiveManifest> GetHiveById(string hiveId)
        {
            string key = hiveId.Split("/")[1];
            return await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);
        }

        public HiveManifest[] FindHivesByTitle(string searchPhrase)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SetHiveAsUsersDefaultHive(string hiveId, string userId)
        {
            string userKey = userId.Split("/")[1];
            Models.User user =
                await _client.Document.GetDocumentAsync<Models.User>(Collections.Users.ToString(), userKey);
            if (user != null)
            {
                user.CurrentHiveId = hiveId;
                await _client.Document.PutDocumentAsync(userId, user);
                return true;
            }

            throw new FileNotFoundException();
        }

        public async Task<bool> AddHiveToUserSavedHives(string hiveId, string userId)
        {
            PostDocumentResponse<UsersSavedHive> response = await _client.Document.PostDocumentAsync(
                Connections.UserHasSavedHive.ToString(), new UsersSavedHive
                {
                    _from = userId,
                    _to = hiveId
                });

            return true;
        }

        public async Task<bool> RemoveHiveFromUserSavedHives(string hiveId, string userId)
        {
            string query =
                $"FOR link IN {Connections.UserHasSavedHive.ToString()} FILTER _from == \"{userId}\" AND _to == \"{hiveId}\"";
            CursorResponse<UsersSavedHive> result = await _client.Cursor.PostCursorAsync<UsersSavedHive>(query);
            UsersSavedHive item = result.Result.FirstOrDefault();

            if (item != null)
            {
                string key = item._id.Split("/")[1];
                await _client.Document.DeleteDocumentAsync<UsersSavedHive>(Connections.UserHasSavedHive.ToString(),
                    key);
                return true;
            }

            throw new FileNotFoundException();
        }

        public async Task<HiveManifest[]> GetSavedHives(string userId)
        {
            string query = $"LET hiveIds = (FOR c IN {Connections.UserHasSavedHive.ToString()} FILTER c._from == \"{userId}\" RETURN c._to) ";
            query += $"FOR hive IN {Collections.HiveManifests.ToString()} FILTER hive._id IN hiveIds RETURN hive";
            CursorResponse<HiveManifest> result = await _client.Cursor.PostCursorAsync<HiveManifest>(query);
            return result.Result.ToArray();
        }
    }
}