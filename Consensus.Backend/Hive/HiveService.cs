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

namespace Consensus.Backend.Hive
{
    public class HiveService : IHiveService
    {
        private readonly ArangoDBClient _client;

        public HiveService(IArangoDb db)
        {
            _client = db.GetClient();
        }

        public async Task<(PointDto, SynapseDto)> CreateNewPoint(string userId, string point, string[] supportingLinks,
            string hiveId, string identifier, string fromId, string toId)
        {
            string key = hiveId.Split("/")[1];
            HiveManifest manifest =
                await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);

            if (!manifest.AllowDanglingPoints && string.IsNullOrEmpty(fromId) && string.IsNullOrEmpty(toId))
            {
                throw new InvalidOperationException();
            }

            if (!string.IsNullOrEmpty(fromId) && !string.IsNullOrEmpty(toId))
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
                        Responses = new Response[] { }
                    },
                    new PostDocumentsQuery {ReturnNew = true});

            await MarkUserAsParticipant(hiveId, userId);
            await BumpHivePointCount(hiveId);

            if (string.IsNullOrEmpty(fromId) && string.IsNullOrEmpty(toId))
            {
                return (TransformPoint(pointResponse.New, userId, manifest.TotalParticipation), null);
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

            return (TransformPoint(pointResponse.New, userId, manifest.TotalParticipation),
                TransformSynapse(synResponse.New, userId, manifest.TotalParticipation));
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

            string key = hiveId.Split("/")[1];
            var manifest =
                await _client.Document.GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), key);

            return TransformSynapse(response.New, userId, manifest.TotalParticipation);
        }

        public async Task<object> Respond(string itemId, string hiveId, bool agree, string userId)
        {
            var collectionId = itemId.Split('/')[0];
            var key = itemId.Split('/')[1];
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

        public async Task<PointDto[]> FindPoints(string phrase, string identifier, string userId, string hiveId)
        {
            string query = @"
            FOR point IN @@view
                SEARCH ANALYZER(point.Label IN TOKENS(@phrase, 'text_en'), 'text_en')
                SORT BM25(point) DESC
            RETURN point";

            var parameters = new Dictionary<string, object>
            {
                ["@view"] = IdPrefix._viewCollection + identifier,
                ["phrase"] = phrase
            };

            CursorResponse<Point> result = await _client.Cursor.PostCursorAsync<Point>(query, parameters);

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
            var parameters = new Dictionary<string, object>
            {
                ["@collection"] = Connections.UserHasParticipated.ToString(),
                ["userId"] = userId,
                ["hiveId"] = hiveId
            };
            CursorResponse<string> existing = await _client.Cursor.PostCursorAsync<string>(query, parameters);
            var existingParticipant = !string.IsNullOrEmpty(existing.Result.FirstOrDefault());

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
    }
}