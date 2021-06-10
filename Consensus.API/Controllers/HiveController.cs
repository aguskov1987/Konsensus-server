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

        [HttpPost, Route("search"), AuthorizeEntry]
        public async Task<IActionResult> FindPoints([FromBody] PointSearchModel model)
        {
            User user = (User)HttpContext.Items["User"];
            PointDto[] points = await _hive.FindPoints(model.Phrase, model.Identifier, user.Id, user.CurrentHiveId);
            return Ok(points);
        }

        [HttpPost, Route("point"), AuthorizeEntry]
        public async Task<IActionResult> PostNewPoint([FromBody] NewPointModel model)
        {
            User user = (User)HttpContext.Items["User"];
            PointDto point = await _hive.CreateNewPoint(user.Id, model.Point, model.SupportingLinks,
                model.HiveId, model.Identifier);
            return Ok(new {points = new [] {point}, synapses = new SynapseDto[]{}, origin = point});
        }
        
        [HttpPost, Route("synapse"), AuthorizeEntry]
        public async Task<IActionResult> ConnectWithSynapse([FromBody] NewSynapseModel model)
        {
            User user = (User)HttpContext.Items["User"];
            SynapseDto synapse = await _hive.CreateNewSynapse(model.FromId, model.ToId, model.HiveId, user.Id);
            
            return Ok(new
            {
                points = new PointDto []{},
                synapses = synapse == null ? new SynapseDto []{} : new[]{synapse}
            });
        }
        
        [HttpPost, Route("respond"), AuthorizeEntry]
        public async Task<IActionResult> Respond([FromBody] UserResponseModel model)
        {
            User user = (User)HttpContext.Items["User"];
            object item = await _hive.Respond(model.ItemId, model.HiveId, model.Agree, user.Id);

            if (item is PointDto dto)
            {
                return Ok(new {points = new[] {dto}, synapses = new SynapseDto[]{}});
            }
            return Ok(new {points = new PointDto []{}, synapses = new[]{item as SynapseDto}});
        }

        [HttpGet, Route("subgraph"), AuthorizeEntry, DecodeQueryParam]
        public async Task<IActionResult> LoadSubgraph([FromQuery(Name = "pointId")] string pointId)
        {
            User user = (User)HttpContext.Items["User"];
            SubGraph graph = await _hive.LoadSubgraph(pointId, user.Id, user.CurrentHiveId);
            return Ok(graph);
        }
    }
}
