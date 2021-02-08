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
            string hiveId, string statementCollectionId)
        {
            if (supportingLinks.Select(IsValidLink).Any(link => !link))
            {
                throw new UriFormatException();
            }

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

            await _user.AddUserAsParticipant(userId, hiveId, true);

            // TODO: add cached participation record

            return TransformStatement(response.New);
        }

        public async Task<StatementDto[]> FindStatements(string phrase, string statementViewId)
        {
            string query = @"
            FOR statement IN @@view
                SEARCH ANALYZER(statement.Label IN TOKENS(@phrase, 'text_en'), 'text_en')
                SORT BM25(statement) DESC
            RETURN statement";

            var parameters = new Dictionary<string, object>
            {
                ["@view"] = statementViewId,
                ["phrase"] = phrase
            };

            CursorResponse<Statement> result = await _client.Cursor.PostCursorAsync<Statement>(query, parameters);
            return result.Result.Select(TransformStatement).ToArray();
        }

        class SubgraphObject
        {
            public Statement Statement { get; set; }
            public Effect Effect { get; set; }
        }

        public async Task<SubGraph> LoadSubgraph(string statementId, string graphId)
        {
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

            CursorResponse<SubgraphObject> result =
                await _client.Cursor.PostCursorAsync<SubgraphObject>(query, parameters);

            (Statement[] st, Effect[] ef) = BreakSubgraphIntoStatementsAndEffects(result.Result);
            var subGraph = new SubGraph
            {
                Statements = st.Select(TransformStatement).ToList(),
                Effects = ef.Select(TransformEffect).ToList()
            };

            return subGraph;
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

        private (Statement[], Effect[]) BreakSubgraphIntoStatementsAndEffects(IEnumerable<SubgraphObject> subgraph)
        {
            IEnumerable<SubgraphObject> subgraphObjects = subgraph as SubgraphObject[] ?? subgraph.ToArray();
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