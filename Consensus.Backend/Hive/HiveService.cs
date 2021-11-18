using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.DocumentApi.Models;
using Consensus.Backend.Data;
using Consensus.Backend.DTOs.Outgoing;
using Consensus.Backend.Models;
using Consensus.Backend.User;

namespace Consensus.Backend.Hive
{
    public class HiveService : IHiveService
    {
        private readonly ArangoDBClient _client;
        private readonly IUserService _user;

        public HiveService(IArangoDb db, IUserService user)
        {
            _client = db.GetClient();
            _user = user;
        }

        #region IHiveService Members

        public async Task<(PointDto, SynapseDto)> CreateNewPoint(string userId, string point, string[] supportingLinks,
            string hiveId, string identifier, string fromId, string toId, PointType type)
        {
            string key = hiveId.Split("/")[1];
            HiveManifest manifest =
                await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);

            if (!manifest.AllowDanglingPoints && string.IsNullOrEmpty(fromId) && string.IsNullOrEmpty(toId))
            {
                throw new InvalidOperationException();
            }

            if (supportingLinks != null && supportingLinks.Select(IsValidLink).Any(link => !link))
            {
                throw new UriFormatException();
            }

            string pointCollectionId = IdPrefix._pointCollection + identifier;

            PostDocumentResponse<Point> pointResponse = await _client.Document
                .PostDocumentAsync(
                    pointCollectionId,
                    new Point
                    {
                        Label = point,
                        Links = supportingLinks,
                        DateCreated = DateTime.Now,
                        Responses = new Response[] { },
                        Type = type
                    },
                    new PostDocumentsQuery {ReturnNew = true});

            await MarkUserAsParticipant(hiveId, userId);
            string lastItemStamp = await AddLastItemInfoToUser(hiveId, pointResponse._id, userId);
            await BumpHivePointCount(hiveId);

            if (string.IsNullOrEmpty(fromId) && string.IsNullOrEmpty(toId))
            {
                PointDto pointDto = TransformPoint(pointResponse.New, userId, manifest.TotalParticipation);
                pointDto.LastItemStamp = lastItemStamp;
                return (pointDto, null);
            }

            string synCollectionId = IdPrefix._synapseCollection + identifier;
            PostDocumentResponse<Synapse> synResponse = await _client.Document
                .PostDocumentAsync(
                    synCollectionId,
                    new Synapse
                    {
                        DateCreated = DateTime.Now,
                        From = !string.IsNullOrEmpty(fromId) ? fromId : pointResponse._id,
                        To = !string.IsNullOrEmpty(toId) ? toId : pointResponse._id,
                        Responses = new Response[] { }
                    },
                    new PostDocumentsQuery {ReturnNew = true});

            lastItemStamp = await AddLastItemInfoToUser(hiveId, synResponse._id, userId, true);

            PointDto connectedPointDto = TransformPoint(pointResponse.New, userId, manifest.TotalParticipation);
            connectedPointDto.LastItemStamp = lastItemStamp;
            SynapseDto synapseDto = TransformSynapse(synResponse.New, userId, manifest.TotalParticipation);
            synapseDto.LastItemStamp = lastItemStamp;
            
            return (connectedPointDto, synapseDto);
        }

        public async Task<SynapseDto> CreateNewSynapse(string fromId, string toId, string hiveId, string userId)
        {
            string identifier = fromId.Split('/')[0].Substring(3);
            string collection = IdPrefix._synapseCollection + identifier;

            // Check for existing
            string query = "FOR i IN @@col FILTER i._from == @from && i._to == @to RETURN i";
            var parameters = new Dictionary<string, object>
            {
                ["@col"] = collection,
                ["from"] = fromId,
                ["to"] = toId
            };
            CursorResponse<Synapse> result =
                await _client.Cursor.PostCursorAsync<Synapse>(query, parameters, null, true);
            if (result.Count > 0)
            {
                return null;
            }

            PostDocumentResponse<Synapse> response = await _client.Document
                .PostDocumentAsync(
                    collection,
                    new Synapse
                    {
                        From = fromId,
                        To = toId,
                        DateCreated = DateTime.Now
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });

            await MarkUserAsParticipant(hiveId, userId);
            string lastItemStamp = await AddLastItemInfoToUser(hiveId, response._id, userId);

            string key = hiveId.Split("/")[1];
            var manifest =
                await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);

            SynapseDto synapse = TransformSynapse(response.New, userId, manifest.TotalParticipation);
            synapse.LastItemStamp = lastItemStamp;
            return synapse;
        }

        public async Task<object> Respond(string itemId, string hiveId, bool agree, string userId)
        {
            var collectionId = itemId.Split('/')[0];
            var key = itemId.Split('/')[1];
            // TODO: what is 't'?
            bool isPoint = collectionId[1] == 't';

            object item;

            string manifestKey = hiveId.Split("/")[1];
            var manifest = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), manifestKey);

            if (isPoint)
            {
                Point result = await _client.Document
                    .GetDocumentAsync<Point>(collectionId, key);
                result.Responses = UpdateResponse(result.Responses, agree, userId);
                var newItem =
                    await _client.Document.PutDocumentAsync(result.Id, result, new PutDocumentQuery {ReturnNew = true});
                item = TransformPoint(newItem.New, userId, manifest.TotalParticipation);
            }
            else
            {
                Synapse result = await _client.Document
                    .GetDocumentAsync<Synapse>(collectionId, key);
                result.Responses = UpdateResponse(result.Responses, agree, userId);
                var newItem =
                    await _client.Document.PutDocumentAsync(result.Id, result, new PutDocumentQuery {ReturnNew = true});
                item = TransformSynapse(newItem.New, userId, manifest.TotalParticipation);
            }

            await MarkUserAsParticipant(hiveId, userId);
            return item;
        }

        public async Task<DeletionResult> TryDeleteItem(string stamp, string userId)
        {
            var user = await _user.GetByIdAsync(userId);
            if (string.IsNullOrEmpty(user.LastCreatedItem) || user.LastCreatedItem != stamp)
            {
                return DeletionResult.Missing;
            }

            (string hiveId, string pointId, string synapseId) = ExtractItemsForDeletion(stamp);

            // trying to delete a point
            if (pointId != null && synapseId == null)
            {
                DeletionResult checkPoint = await CheckIfCanDeletePoint(pointId, false);
                if (checkPoint == DeletionResult.Success)
                {
                    await DeletePoint(hiveId, pointId);
                    return DeletionResult.Success;
                }

                return checkPoint;
            }

            // trying to delete a synapse
            if (pointId == null && synapseId != null)
            {
                DeletionResult checkSynapse = await CheckIfCanDeleteSynapse(synapseId);
                if (checkSynapse == DeletionResult.Success)
                {
                    await DeleteSynapse(hiveId, synapseId);
                    return DeletionResult.Success;
                }

                return checkSynapse;
            }
            
            // trying to delete both point and synapse
            DeletionResult checkPoint2 = await CheckIfCanDeletePoint(pointId, true);
            DeletionResult checkSynapse2 = await CheckIfCanDeleteSynapse(synapseId);
            if (checkPoint2 == DeletionResult.Success && checkSynapse2 == DeletionResult.Success)
            {
                await DeletePoint(hiveId, pointId);
                await DeleteSynapse(hiveId, synapseId);
                return DeletionResult.Success;
            }

            return checkPoint2 != DeletionResult.Success ? checkPoint2 : checkSynapse2;
        }

        public Task<PointDto[]> FindPointsFromQuantQuery(string query, string identifier, string userId, string currentHiveId)
        {
            List<QuantSearchClause> clauses = null;
            bool parsed = ParseQuantQuery(query, out clauses);
            if (!parsed)
            {
                throw new FormatException();
            }
            
            throw new NotImplementedException();
        }

        public async Task<PointDto[]> FindPoints(string query, string identifier, string userId, string hiveId)
        {
            string aql = @"
            FOR point IN @@view
                SEARCH ANALYZER(point.Label IN TOKENS(@phrase, 'text_en'), 'text_en')
                SORT BM25(point) DESC
            RETURN point";

            var parameters = new Dictionary<string, object>
            {
                ["@view"] = IdPrefix._viewCollection + identifier,
                ["phrase"] = query
            };

            CursorResponse<Point> result = await _client.Cursor.PostCursorAsync<Point>(aql, parameters);

            string manifestKey = hiveId.Split("/")[1];
            var manifest = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), manifestKey);
            return result.Result.Select((p) => TransformPoint(p, userId, manifest.TotalParticipation)).ToArray();
        }

        public async Task<SubGraph> LoadSubgraph(string pointId, string userId, string hiveId)
        {
            string graphId = pointId.Split("/")[0]
                .Replace(IdPrefix._pointCollection, IdPrefix._graph);

            string query = @"
            FOR v, e IN 0..5 ANY @pointId
                GRAPH @graphId
                OPTIONS {uniqueVertices: 'path', }
                RETURN {point: v, synapse: e}";

            var parameters = new Dictionary<string, object>
            {
                ["pointId"] = pointId,
                ["graphId"] = graphId
            };

            CursorResponse<Subgraph> result =
                await _client.Cursor.PostCursorAsync<Subgraph>(query, parameters);

            (Point[] st, Synapse[] ef) = BreakSubgraph(result.Result);

            string manifestKey = hiveId.Split("/")[1];
            var manifest = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), manifestKey);
            var subGraph = new SubGraph
            {
                Points = st.Select((p) => TransformPoint(p, userId, manifest.TotalParticipation)).ToList(),
                Synapses = ef.Select((s) => TransformSynapse(s, userId, manifest.TotalParticipation)).ToList()
            };
            subGraph.Origin = subGraph.Points.FirstOrDefault(s => s.Id == pointId);

            return subGraph;
        }

        public async Task MarkUserAsParticipant(string hiveId, string userId)
        {
            string query = @"FOR relation IN @@collection
                                FILTER relation._from == @userId AND relation._to == @hiveId
                                RETURN relation._to";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["@collection"] = Connections.UserHasParticipated.ToString(),
                ["userId"] = userId,
                ["hiveId"] = hiveId
            };
            CursorResponse<string> existing = await _client.Cursor.PostCursorAsync<string>(query, parameters);
            bool existingParticipant = !string.IsNullOrEmpty(existing.Result.FirstOrDefault());

            // if this is the first participation, add a record
            if (!existingParticipant)
            {
                await _client.Document.PostDocumentAsync(Connections.UserHasParticipated.ToString(),
                    new {_from = userId, _to = hiveId});
            }

            // also add a participation remark to the hive manifest
            string hiveKey = hiveId.Split("/")[1];
            DateTime today = DateTime.Now.Date;
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), hiveKey);

            ParticipationCount[] counts = hive.DailyParticipation.Where(p => p.Date == today).ToArray();
            if (counts.Length == 1)
            {
                counts[0].NumberOfParticipants++;
            }
            else
            {
                hive.DailyParticipation.Add(new ParticipationCount
                {
                    Date = today,
                    NumberOfParticipants = 1
                });
            }

            hive.TotalParticipation++;
            hive.TimeOfLastParticipation = DateTime.Now;

            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        public async Task BumpHivePointCount(string hiveId)
        {
            string hiveKey = hiveId.Split("/")[1];
            DateTime today = DateTime.Now.Date;
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), hiveKey);

            var pointCounts = hive.DailyPointCount.Where(p => p.Date == today).ToArray();
            if (pointCounts.Length == 1)
            {
                pointCounts[0].Count++;
            }
            else
            {
                hive.DailyPointCount.Add(new PointCount
                {
                    Date = today,
                    Count = 1
                });
            }

            hive.TotalPoints++;

            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        #endregion

        private async Task<DeletionResult> CheckIfCanDeletePoint(string pointId, bool connected)
        {
            var collectionId = pointId.Split('/')[0];
            var key = pointId.Split('/')[1];
            Point point = await _client.Document
                .GetDocumentAsync<Point>(collectionId, key);
            if (point.Responses.Length > 1)
            {
                return DeletionResult.RespondedTo;
            }

            string graph = collectionId.Replace(IdPrefix._pointCollection, IdPrefix._graph);
            string queryForSynapses = @"
            FOR v, e IN 0..1 ANY @point
                GRAPH @graph
                OPTIONS {uniqueVertices: 'path', }
                RETURN e";
            var parameters = new Dictionary<string, object>
            {
                ["point"] = pointId,
                ["graph"] = graph
            };

            CursorResponse<Subgraph> result =
                await _client.Cursor.PostCursorAsync<Subgraph>(queryForSynapses, parameters, count: true);

            if (!connected && result.Count == 1 && result.Result.FirstOrDefault() == null)
            {
                return DeletionResult.Success;
            }

            if (connected && result.Count == 2)
            {
                return DeletionResult.Success;
            }
            return DeletionResult.ConnectedTo;
        }

        private async Task<DeletionResult> CheckIfCanDeleteSynapse(string synapseId)
        {
            var collectionId = synapseId.Split('/')[0];
            var key = synapseId.Split('/')[1];
            Synapse synapse = await _client.Document
                .GetDocumentAsync<Synapse>(collectionId, key);
            if (synapse.Responses != null && synapse.Responses.Length > 1)
            {
                return DeletionResult.RespondedTo;
            }

            return DeletionResult.Success;
        }

        private (string ,string, string) ExtractItemsForDeletion(string stamp)
        {
            string[] parts = stamp.Split('+');
            
            if (parts.Length == 1)
            {
                // point/synapse only
                string[] ids = parts[0].Split(':');
                if (ids[1].Substring(0, 3) == IdPrefix._pointCollection)
                {
                    return (ids[0], ids[1], null);
                }
                if (ids[1].Substring(0, 3) == IdPrefix._synapseCollection)
                {
                    return (ids[0], null, ids[1]);
                }

                throw new InvalidOperationException();
            }

            if (parts.Length == 2)
            {
                string[] pointsIds = parts[0].Split(':');
                var hiveId = pointsIds[0];
                var pointId = pointsIds[1];

                string[] synapseIds = parts[1].Split(':');
                var synapseId = synapseIds[1];

                return (hiveId, pointId, synapseId);
            }
            
            throw new InvalidOperationException();
        }

        private async Task<string> AddLastItemInfoToUser(string hiveId, string itemId, string userId, bool addToExisting = false)
        {
            // stamp format: {hive ID}:{point ID} or {hive ID}:{point ID}+{hive ID}:{synapse ID}
            
            string userKey = userId.Split("/")[1];
            var user = await _client.Document
                .GetDocumentAsync<Models.User>(Collections.Users.ToString(), userKey);
            
            string info = $"{hiveId}:{itemId}";
            user.LastCreatedItem = addToExisting ? user.LastCreatedItem + "+" + info : info;
            await _client.Document.PutDocumentAsync(userId, user);
            
            return user.LastCreatedItem;
        }

        private bool ParseQuantQuery(string query, out List<QuantSearchClause> clauses)
        {
            if (query[0] != '!' || query[1] != '!')
            {
                clauses = null;
                return false;
            }

            try
            {
                clauses = new List<QuantSearchClause>();
                query = query.Substring(2).ToLower();
                string[] parts = query.Split(';');
                
                foreach (string part in parts)
                {
                    string[] tokens = part.Split(' ');
                    if (tokens[0].Trim() == "most")
                    {
                        switch (tokens[1].Trim())
                        {
                            case "active":
                                clauses.Add(QuantSearchClause.MostActive);
                                break;
                            case "connected":
                                clauses.Add(QuantSearchClause.MostConnected);
                                break;
                            case "recently-responded":
                                clauses.Add(QuantSearchClause.MostFresh);
                                break;
                            case "positive":
                                clauses.Add(QuantSearchClause.MostPositive);
                                break;
                            case "old":
                                clauses.Add(QuantSearchClause.MostOld);
                                break;
                        }
                    } else if (tokens[0].Trim() == "least")
                    {
                        switch (tokens[1].Trim())
                        {
                            case "active":
                                clauses.Add(QuantSearchClause.LeastActive);
                                break;
                            case "connected":
                                clauses.Add(QuantSearchClause.LeastConnected);
                                break;
                            case "recently-responded":
                                clauses.Add(QuantSearchClause.LeastFresh);
                                break;
                            case "positive":
                                clauses.Add(QuantSearchClause.LeastPositive);
                                break;
                            case "old":
                                clauses.Add(QuantSearchClause.LeastOld);
                                break;
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                clauses = null;
                return false;
            }
            
        }

        private PointDto TransformPoint(Point s, string userId, int total)
        {
            var point = new PointDto
            {
                Id = s.Id,
                Label = s.Label
            };

            (int userResponse, float commonResponse, float penetration) =
                CalculateResponses(s.Responses, userId, total);
            point.UserResponse = userResponse;
            point.CommonResponse = commonResponse;
            point.Penetration = penetration;
            point.Type = s.Type;
            return point;
        }

        private SynapseDto TransformSynapse(Synapse e, string userId, int total)
        {
            var synapse = new SynapseDto
            {
                Id = e.Id,
                From = e.From,
                To = e.To
            };

            (int userResponse, float commonResponse, float penetration) =
                CalculateResponses(e.Responses, userId, total);
            synapse.UserResponse = userResponse;
            synapse.CommonResponse = commonResponse;
            synapse.Penetration = penetration;

            return synapse;
        }

        private (int, float, float) CalculateResponses(Response[] responses, string userId, int totalParticipation)
        {
            int userResponse = 0; // -1 <<< 0 >>> +1
            float commonResponse = 0; // -1.0 <<< 0 >>> +1.0
            float penetration = 0; // 0 >>> 1

            if (responses == null || responses.Length < 1)
            {
                return (userResponse, commonResponse, penetration);
            }

            var userResponseRecord = responses.FirstOrDefault(r => r.UserId == userId);
            if (userResponseRecord != null)
            {
                userResponse = userResponseRecord.Agrees ? 1 : -1;
            }

            int positiveResponses = responses.Count(r => r.Agrees);
            int negativeResponses = responses.Length - positiveResponses;
            commonResponse = (positiveResponses - negativeResponses) / (float) responses.Length;

            penetration = responses.Length / (float) totalParticipation;

            return (userResponse, commonResponse, penetration);
        }

        private bool IsValidLink(string link)
        {
            return Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out _);
        }

        private (Point[], Synapse[]) BreakSubgraph(IEnumerable<Subgraph> subgraph)
        {
            IEnumerable<Subgraph> subgraphObjects = subgraph as Subgraph[] ?? subgraph.ToArray();
            IEnumerable<Point> points = subgraphObjects.Select(obj => obj.Point);
            IEnumerable<Synapse> synapses = subgraphObjects.Select(obj => obj.Synapse);

            Point[] uniquePoints = points
                .GroupBy(s => s.Id)
                .Select(s => s.First())
                .ToArray();

            Synapse[] uniqueSynapses = synapses
                .Where(e => e != null)
                .GroupBy(e => e.Id)
                .Select(e => e.First())
                .ToArray();

            return (uniquePoints, uniqueSynapses);
        }

        private Response[] UpdateResponse(Response[] original, bool agree, string userId)
        {
            if (original == null || !original.Any())
            {
                return new[] {new Response {Agrees = agree, Time = DateTime.Now, UserId = userId}};
            }

            var existing = original.FirstOrDefault(r => r.UserId == userId);
            if (existing != null)
            {
                existing.Agrees = agree;
                existing.Time = DateTime.Now;
                return original;
            }

            var newItem = new Response {Agrees = agree, Time = DateTime.Now, UserId = userId};
            var updated = new Response[original.Length + 1];
            original.CopyTo(updated, 0);
            updated[updated.Length - 1] = newItem;

            return updated;
        }

        private async Task DeletePoint(string hiveId, string pointId)
        {
            string hiveKey = hiveId.Split('/')[1];
            await _client.Document
                .DeleteDocumentAsync<Point>(pointId.Split('/')[0], pointId.Split('/')[1]);
                    
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(),
                    hiveKey);
                    
            hive.TotalParticipation--;
            hive.TotalPoints--;
            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        private async Task DeleteSynapse(string hiveId, string synapseId)
        {
            string hiveKey = hiveId.Split('/')[1];
            await _client.Document.
                DeleteDocumentAsync<Point>(synapseId.Split('/')[0], synapseId.Split('/')[1]);
            
            HiveManifest hive = await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(),
                hiveKey);
            
            hive.TotalParticipation--;
            await _client.Document.PutDocumentAsync(hiveId, hive);
        }
    }
}