using System;
using System.Threading.Tasks;
using Consensus.API.Auth;
using Consensus.API.Models.Incoming;
using Consensus.Backend.DTOs.Outgoing;
using Consensus.Backend.Models;
using Consensus.Backend.Yard;
using Microsoft.AspNetCore.Mvc;

namespace Consensus.API.Controllers
{
    [Route("yard")]
    public class YardController : Controller
    {
        private readonly IYardService _yard;

        public YardController(IYardService yard)
        {
            _yard = yard;
        }
        
        [HttpGet, Route(""), AuthorizeEntry]
        public async Task<IActionResult> LoadYard([FromQuery] YardRequestParams parameters)
        {
            HivesPagedSet hives = await _yard.LoadYard(parameters.Query, parameters.Page, parameters.HivesPerPage,
                parameters.Sort, parameters.Order);
            return Ok(hives);
        }

        [HttpPost, Route("hive"), AuthorizeEntry]
        public async Task<IActionResult> CreateNewHiveAsync([FromBody] NewHiveModel model)
        {
            User user = (User) HttpContext.Items["User"];

            try
            {
                HiveManifest manifest = await _yard.CreateHive(model.Title, model.Description, user.Id, model.SeedLabel);
                return Ok(manifest);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return StatusCode(500);
            }
        }

        [HttpGet, Route("hive"), AuthorizeEntry, DecodeQueryParam]
        public async Task<IActionResult> LoadHiveAsync([FromQuery] string hiveId)
        {
            User user = (User) HttpContext.Items["User"];
            HiveManifestDto hive = await _yard.GetHiveById(hiveId);
            if (hive != null)
            {
                await _yard.SetHiveAsUsersDefaultHive(hive.Id, user.Id);
                return Ok(hive);
            }

            return StatusCode(500);
        }
    }
}