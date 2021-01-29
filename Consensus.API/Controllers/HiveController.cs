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

        [HttpPost, Route("statement")]
        [AuthorizeEntry]
        public async Task<IActionResult> PostNewStatement([FromBody] NewStatementModel model)
        {
            User user = (User)HttpContext.Items["User"];
            StatementDto statement = await _hive.CreateNewStatement(user._id, model.Statement, model.HiveId, model.StatementCollectionId);
            return Ok(new {statements = new [] {statement}});
        }
    }
}