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
            _user = user;
            _client = db.GetClient();
        }

        public async Task<PointDto> CreateNewPoint(string userId, string point, string[] supportingLinks,
            string hiveId, string identifier)
        {
            if (supportingLinks != null && supportingLinks.Select(IsValidLink).Any(link => !link))
            {
                throw new UriFormatException();
            }

            string pointCollectionId = IdPrefix._pointCollection + identifier;

            PostDocumentResponse<Point> response = await _client.Document
                .PostDocumentAsync(
                    pointCollectionId,
                    new Point
                    {
                        Label = point,
                        Links = supportingLinks
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });

            await MarkUserAsParticipant(hiveId, userId);
            await BumpHivePointCount(hiveId);

            // TODO: add cached participation record

            return TransformPoint(response.New);
        }

        public async Task<PointDto[]> FindPoints(string phrase, string identifier)
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
            return result.Result.Select(TransformPoint).ToArray();
        }

        public async Task<SubGraph> LoadSubgraph(string pointId)
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
            var subGraph = new SubGraph
            {
                Points = st.Select(TransformPoint).ToList(),
                Synapses = ef.Select(TransformSynapse).ToList()
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

            ParticipationCount[] counts = hive.Participation.Where(p => p.Date == today).ToArray();
            if (counts.Length == 1)
            {
                counts[0].NumberOfParticipants++;
            }
            else
            {
                hive.Participation.Add(new ParticipationCount
                {
                    Date = today,
                    NumberOfParticipants = 1
                });
            }
            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        public async Task BumpHivePointCount(string hiveId)
        {
            string hiveKey = hiveId.Split("/")[1];
            DateTime today = DateTime.Now.Date;
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), hiveKey);
            
            var pointCounts = hive.PointCount.Where(p => p.Date == today).ToArray();
            if (pointCounts.Length == 1)
            {
                pointCounts[0].Count++;
            }
            else
            {
                hive.PointCount.Add(new PointCount
                {
                    Date = today,
                    Count = 1
                });
            }
                
            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        private PointDto TransformPoint(Point s)
        {
            var point = new PointDto {Id = s.Id, Label = s.Label};
            return point;
        }

        private SynapseDto TransformSynapse(Synapse e)
        {
            return new SynapseDto
            {
                Id = e.Id,
                From = e.From,
                To = e.To
            };
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
    }
}