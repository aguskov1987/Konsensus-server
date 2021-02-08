using System.Threading.Tasks;
using Consensus.API.Auth;
using Consensus.API.Models.Incoming;
using Consensus.Backend.DTOs.Outgoing;
using Consensus.Backend.Hive;
using Consensus.Backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace Consensus.API.Controllers
{
    [Route("hive")]
    public class HiveController : Controller
    {
        private readonly IHiveService _hive;

        public HiveController(IHiveService hive)
        {
            _hive = hive;
        }

        [HttpPost, Route("search")]
        [AuthorizeEntry]
        public async Task<IActionResult> FindStatements([FromBody] StatementSearchModel model)
        {
            StatementDto[] statements = await _hive.FindStatements(model.Phrase, model.ViewId);
            return Ok(statements);
        }

        [HttpPost, Route("statement")]
        [AuthorizeEntry]
        public async Task<IActionResult> PostNewStatement([FromBody] NewStatementModel model)
        {
            User user = (User)HttpContext.Items["User"];
            StatementDto statement = await _hive.CreateNewStatement(user._id, model.Statement, model.SupportingLinks,
                model.HiveId, model.StatementCollectionId);
            return Ok(new {statements = new [] {statement}});
        }

        [HttpGet, Route("subgraph/{graphId}/{statementId}")]
        [AuthorizeEntry]
        public async Task<IActionResult> LoadSubgraph(string statementId, string graphId)
        {
            statementId = statementId.Replace("_", "/");
            SubGraph graph = await _hive.LoadSubgraph(statementId, graphId);
            return Ok(graph);
        }

        [HttpPut, Route("respond")]
        [AuthorizeEntry]
        public async Task<IActionResult> RespondToStatement([FromBody] UserResponseModel model)
        {
            return Ok();
        }
        
        [HttpPut, Route("connect")]
        [AuthorizeEntry]
        public async Task<IActionResult> ConnectCauseToEffect([FromBody] ConnectStatementsModel model)
        {
            return Ok();
        }
    }
}
