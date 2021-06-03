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

        public async Task<StatementDto> CreateNewStatement(string userId, string label, string[] supportingLinks,
            string hiveId, string identifier)
        {
            if (supportingLinks != null && supportingLinks.Select(IsValidLink).Any(link => !link))
            {
                throw new UriFormatException();
            }

            string statementCollectionId = IdPrefix._statementCollection + identifier;

            PostDocumentResponse<Statement> response = await _client.Document
                .PostDocumentAsync(
                    statementCollectionId,
                    new Statement
                    {
                        Label = label,
                        Links = supportingLinks
                    },
                    new PostDocumentsQuery
                    {
                        ReturnNew = true
                    });

            await MarkUserAsParticipant(hiveId, userId);
            await BumpHiveStatementCount(hiveId);

            // TODO: add cached participation record

            return TransformStatement(response.New);
        }

        public async Task<StatementDto[]> FindStatements(string phrase, string identifier)
        {
            string query = @"
            FOR statement IN @@view
                SEARCH ANALYZER(statement.Label IN TOKENS(@phrase, 'text_en'), 'text_en')
                SORT BM25(statement) DESC
            RETURN statement";

            var parameters = new Dictionary<string, object>
            {
                ["@view"] = IdPrefix._viewCollection + identifier,
                ["phrase"] = phrase
            };

            CursorResponse<Statement> result = await _client.Cursor.PostCursorAsync<Statement>(query, parameters);
            return result.Result.Select(TransformStatement).ToArray();
        }

        public async Task<SubGraph> LoadSubgraph(string statementId)
        {
            string graphId = statementId.Split("/")[0]
                .Replace(IdPrefix._statementCollection, IdPrefix._graph);
            
            string query = @"
            FOR v, e IN 0..5 ANY @statementId
                GRAPH @graphId
                OPTIONS {uniqueVertices: 'path', }
                RETURN {statement: v, effect: e}";

            var parameters = new Dictionary<string, object>
            {
                ["statementId"] = statementId,
                ["graphId"] = graphId
            };

            CursorResponse<Subgraph> result =
                await _client.Cursor.PostCursorAsync<Subgraph>(query, parameters);

            (Statement[] st, Effect[] ef) = BreakSubgraphIntoStatementsAndEffects(result.Result);
            var subGraph = new SubGraph
            {
                Statements = st.Select(TransformStatement).ToList(),
                Effects = ef.Select(TransformEffect).ToList()
            };
            subGraph.Origin = subGraph.Statements.FirstOrDefault(s => s.Id == statementId);

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

        public async Task BumpHiveStatementCount(string hiveId)
        {
            string hiveKey = hiveId.Split("/")[1];
            DateTime today = DateTime.Now.Date;
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), hiveKey);
            
            var statementCounts = hive.NumberOfStatements.Where(p => p.Date == today).ToArray();
            if (statementCounts.Length == 1)
            {
                statementCounts[0].NumberOfStatements++;
            }
            else
            {
                hive.NumberOfStatements.Add(new StatementCount
                {
                    Date = today,
                    NumberOfStatements = 1
                });
            }
                
            await _client.Document.PutDocumentAsync(hiveId, hive);
        }

        private StatementDto TransformStatement(Statement s)
        {
            var statement = new StatementDto {Id = s._id, Label = s.Label};
            return statement;
        }

        private EffectDto TransformEffect(Effect e)
        {
            return new EffectDto
            {
                Id = e._id,
                Source = e._from,
                Target = e._to
            };
        }

        private bool IsValidLink(string link)
        {
            return Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out _);
        }

        private (Statement[], Effect[]) BreakSubgraphIntoStatementsAndEffects(IEnumerable<Subgraph> subgraph)
        {
            IEnumerable<Subgraph> subgraphObjects = subgraph as Subgraph[] ?? subgraph.ToArray();
            IEnumerable<Statement> statements = subgraphObjects.Select(obj => obj.Statement);
            IEnumerable<Effect> effects = subgraphObjects.Select(obj => obj.Effect);

            Statement[] uniqueStatements = statements
                .GroupBy(s => s._id)
                .Select(s => s.First())
                .ToArray();

            Effect[] uniqueEffects = effects
                .Where(e => e != null)
                .GroupBy(e => e._id)
                .Select(e => e.First())
                .ToArray();

            return (uniqueStatements, uniqueEffects);
        }
    }
}