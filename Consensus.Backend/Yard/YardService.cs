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
using Consensus.Backend.Hive;
using Consensus.Backend.Models;

namespace Consensus.Backend.Yard
{
    public class YardService : IYardService
    {
        private readonly ArangoDBClient _client;
        private readonly IHiveService _hive;

        public YardService(IArangoDb db, IHiveService hive)
        {
            _client = db.GetClient();
            _hive = hive;
        }

        public async Task<HiveManifest> CreateHive(string title, string description, string userId)
        {
            // 1. generate ID
            string identifier = Guid.NewGuid().ToString();
            string collectionName = IdPrefix._statementCollection + identifier;
            string edgeCollectionName = IdPrefix._synapseCollection + identifier;
            string graphName = IdPrefix._graph + identifier;
            string viewName = IdPrefix._viewCollection + identifier;

            try
            {
                // 2. create a graph for the new collection
                await _client.Graph.PostGraphAsync(new PostGraphBody
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
                        CollectionId = identifier
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });
                
                // 4. register user's participation
                await _hive.MarkUserAsParticipant(hiveManifest.New._id, userId);

                // 5. create a search view for the new collection
                await _client.View.PostView(new PostViewBody
                {
                    Name = viewName,
                    CollectionName = collectionName,
                    FieldsToIndex = new[] {"Label"}
                });
                
                await AddHiveToUserSavedHives(hiveManifest.New._id, userId, SavedHiveOwnershipType.UserCreatedHive);
                await SetHiveAsUsersDefaultHive(hiveManifest.New._id, userId);
                await _hive.MarkUserAsParticipant(hiveManifest.New._id, userId);

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

        public async Task<HiveManifest[]> FindHivesByTitle(string searchPhrase)
        {
            string query = @"
            FOR doc IN HiveManifests_View
                SEARCH ANALYZER(doc.Title IN TOKENS(@phrase, 'text_en'), 'text_en')
                SORT BM25(doc) DESC
            RETURN doc";

            var parameters = new Dictionary<string, object>
            {
                ["phrase"] = searchPhrase
            };

            var result = await _client.Cursor.PostCursorAsync<HiveManifest>(query, parameters);
            return result.Result.ToArray();
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

        public async Task<bool> AddHiveToUserSavedHives(string hiveId, string userId, SavedHiveOwnershipType ownership)
        {
            await _client.Document.PostDocumentAsync(
                Connections.UserHasSavedHive.ToString(), new UsersSavedHive
                {
                    _from = userId,
                    _to = hiveId,
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
                string key = item._id.Split("/")[1];
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

        public async Task<HiveManifest[]> LoadMostActiveHives()
        {
            string query = @"
            LET calculated = (
                FOR manifest IN HiveManifests
                FILTER manifest.Participation != null AND length(manifest.Participation) > 1
                LET sorted = (FOR p IN manifest.Participation SORT p.Date DESC RETURN p)
                LET today = sorted[0]
                LET yesterday = sorted[1]
                LET dynamic = today.NumberOfParticipants/yesterday.NumberOfParticipants
                RETURN {manifest, dynamic}
            )
    
            FOR c in calculated
                SORT c.dynamic DESC
                LIMIT 20
                RETURN c.manifest";
            
            // TODO: cache the result for future use
            
            CursorResponse<HiveManifest> result = await _client.Cursor.PostCursorAsync<HiveManifest>(query);
            return result.Result.ToArray();
        }
    }
}