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
using Consensus.Backend.DTOs.Outgoing;
using Consensus.Backend.Hive;
using Consensus.Backend.Models;
using Consensus.Backend.Saved;
using Microsoft.Extensions.Configuration;

namespace Consensus.Backend.Yard
{
    public class YardService : IYardService
    {
        private readonly ArangoDBClient _client;
        private readonly IHiveService _hive;
        private readonly ISavedHivesService _savedHives;
        private readonly int _historyDays;

        public YardService(IArangoDb db, IHiveService hive, ISavedHivesService savedHives, IConfiguration config)
        {
            _client = db.GetClient();
            _hive = hive;
            _savedHives = savedHives;
            _historyDays = Convert.ToInt32(config["TimeToStoreHiveHistory"]);
        }

        #region IYardService Members

        public async Task<HiveManifest> CreateHive(string title, string description, string userId, string seed, PointType seedType)
        {
            // 1. generate ID
            string identifier = Guid.NewGuid().ToString();
            string collectionName = IdPrefix._pointCollection + identifier;
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
                        CollectionId = identifier,
                        DateCreated = DateTime.Now,
                        AllowDanglingPoints = string.IsNullOrEmpty(seed)
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });
                

                // 4. create a search view for the new collection
                await _client.View.PostView(new PostViewBody
                {
                    Name = viewName,
                    CollectionName = collectionName,
                    FieldsToIndex = new[] {"Label"}
                });

                // 5. if seed point is supplied, populate it
                if (!string.IsNullOrEmpty(seed))
                {
                    await _hive.CreateNewPoint(userId, seed, null,
                        hiveManifest.New.Id, hiveManifest.New.CollectionId, null, null, seedType);
                }
                
                await _savedHives.AddHiveToUserSavedHives(hiveManifest.New.Id, userId, SavedHiveOwnershipType.UserCreatedHive);
                await SetHiveAsUsersDefaultHive(hiveManifest.New.Id, userId);
                await _hive.MarkUserAsParticipant(hiveManifest.New.Id, userId);

                return hiveManifest.New;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<HiveManifestDto> GetHiveById(string hiveId)
        {
            string key = hiveId.Split("/")[1];
            var manifest = await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);

            // Every hive manifest has a list of history records which show how many points and how many actions
            // (participants) were created/performed each day. Going back infinitely does not make sense so
            // there is a limiter (_historyDays) - go back this many days from today (hence -> Reverse());
            // if there is more, clip the data and re-save the manifest record.
            bool save = false;
            if (manifest.DailyPointCount.Count > _historyDays)
            {
                manifest.DailyPointCount = manifest.DailyPointCount
                    .OrderBy(c => c.Date)
                    .Reverse()
                    .Take(_historyDays)
                    .ToList();
                save = true;
            }

            if (manifest.DailyParticipation.Count > _historyDays)
            {
                manifest.DailyParticipation = manifest.DailyParticipation
                    .OrderBy(c => c.Date)
                    .Reverse()
                    .Take(_historyDays)
                    .ToList();
                save = true;
            }

            if (save)
            {
                await _client.Document.PutDocumentAsync(manifest.Id, manifest);
            }
            
            return TransformManifest(manifest);
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

        public async Task<HivesPagedSet> LoadYard(string query, int page, int perPage, HiveSortingOption sort, HiveOrder order)
        {
            var parameters = new Dictionary<string, object>
            {
                ["perPage"] = perPage,
                ["page"] = page
            };
            
            string aql;
            string orderText = order == HiveOrder.Ascending ? "ASC" : "DESC";
            if (string.IsNullOrEmpty(query))
            {
                string sortText;
                switch (sort)
                {
                    case HiveSortingOption.ByActivity:
                        sortText = "TimeOfLastParticipation";
                        break;
                    case HiveSortingOption.ByPointCount:
                        sortText = "TotalPoints";
                        break;
                    case HiveSortingOption.ByDate:
                        sortText = "DateCreated";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sort), sort, null);
                }
                aql = $@"
                LET total = 
                    (FOR h in HiveManifests
                    COLLECT WITH COUNT INTO l
                    RETURN l)

                FOR hive IN HiveManifests
                    SORT hive.{sortText} {orderText}
                    LIMIT (@perPage * (@page - 1)), @perPage
                    RETURN MERGE(hive, {{Total: total[0]}})";
            }
            else
            {
                aql = $@"
                LET total = 
                    (FOR h in HiveManifests
                    SEARCH ANALYZER(h.Title IN TOKENS(@phrase, 'text_en'), 'text_en')
                    COLLECT WITH COUNT INTO l
                    RETURN l)

                FOR hive IN HiveManifests_View
                    SEARCH ANALYZER(hive.Title IN TOKENS(@phrase, 'text_en'), 'text_en')
                    SORT BM25(doc) {orderText}
                    LIMIT (@perPage * (@page - 1)), @perPage
                    RETURN MERGE(hive, {{Total: total[0]}})";
                
                parameters.Add("phrase", query);
            }

            CursorResponse<HiveManifest> result = await _client.Cursor.PostCursorAsync<HiveManifest>(aql, parameters);
            int total;
            int remainder = result.Result.First().Total % perPage;
            if (remainder == 0)
            {
                total = result.Result.First().Total / perPage;
            }
            else
            {
                total = result.Result.First().Total / perPage + 1;
            }
            
            return new HivesPagedSet
            {
                Hives = result.Result.Select(TransformManifest).ToArray(),
                TotalPages = total
            };
        }

        #endregion

        private HiveManifestDto TransformManifest(HiveManifest manifest)
        {
            // Normalize the history to make sure missing days are accounted for
            int[] pointsCount = new int[_historyDays];
            int[] partCount = new int[_historyDays];
            DateTime day = DateTime.Now.Date;
            
            for (int i = 0; i < _historyDays; i++)
            {
                var existingPoints = manifest.DailyPointCount.FirstOrDefault(c => c.Date == day);
                pointsCount[i] = existingPoints?.Count ?? 0;
                
                var existingParticipation = manifest.DailyParticipation.FirstOrDefault(c => c.Date == day);
                partCount[i] = existingParticipation?.NumberOfParticipants ?? 0;

                day = day.Subtract(TimeSpan.FromDays(1));
            }
            
            return new HiveManifestDto
            {
                Id = manifest.Id,
                Description = manifest.Description,
                Title = manifest.Title,
                CollectionId = manifest.CollectionId,
                DateCreated = manifest.DateCreated,
                PointCount = pointsCount.Reverse().ToArray(),
                ParticipationCount = partCount.Reverse().ToArray(),
                TotalParticipation = manifest.TotalParticipation,
                TotalPoints = manifest.TotalPoints,
                AllowDanglingPoints = manifest.AllowDanglingPoints
            };
        }
    }
}