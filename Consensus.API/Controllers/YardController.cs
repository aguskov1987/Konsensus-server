using System.Threading.Tasks;
using Consensus.API.Auth;
using Consensus.API.Models.Incoming;
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

        [HttpPost, Route("hive"), AuthorizeEntry]
        public async Task<IActionResult> CreateNewHiveAsync([FromBody] NewHiveModel model)
        {
            User user = (User) HttpContext.Items["User"];
            await _yard.CreateHive(model.Title, model.Description, user._id);

            return StatusCode(500);
        }

        [HttpGet, Route("hive/{id}"), AuthorizeEntry, DecodeQueryParam]
        public async Task<IActionResult> LoadHiveAsync(string id)
        {
            User user = (User) HttpContext.Items["User"];
            HiveManifest hive = await _yard.GetHiveById(id);
            if (hive != null)
            {
                await _yard.SetHiveAsUsersDefaultHive(hive._id, user._id);
                return Ok(hive);
            }

            return StatusCode(500);
        }

        [HttpGet, Route("saved"), AuthorizeEntry]
        public async Task<IActionResult> LoadSavedHivesAsync()
        {
            User user = (User) HttpContext.Items["User"];
            HiveManifest[] savedHives = await _yard.GetSavedHives(user._id);
            return Ok(savedHives);
        }

        [HttpGet, Route("start"), AuthorizeEntry]
        public async Task<IActionResult> LoadMostActiveHivesAsync()
        {
            HiveManifest[] hives = await _yard.LoadMostActiveHives();
            return Ok(hives);
        }

        [HttpPost, Route("search"), AuthorizeEntry]
        public async Task<IActionResult> SearchYardAsync([FromBody] SearchYardModel model)
        {
            HiveManifest[] hives = await _yard.FindHivesByTitle(model.Phrase);
            return Ok(hives);
        }
    }
}