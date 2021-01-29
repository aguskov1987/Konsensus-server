using System;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
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
        public async Task<StatementDto> CreateNewStatement(string userId, string label, string hiveId, string statementCollectionId)
        {
            PostDocumentResponse<Statement> response = await _client.Document
                .PostDocumentAsync(
                    statementCollectionId,
                    new Statement {Label = label},
                    new PostDocumentsQuery{ReturnNew = true});
            
            await _user.AddUserAsParticipant(userId, hiveId, true);
            
            // TODO: add cached participation record
            
            var statement = new StatementDto
            {
                Id = response._id,
                Label = response.New.Label
            };
            return statement;
        }
    }
}