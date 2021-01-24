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

        [HttpPost, Route("hive")]
        [AuthorizeEntry]
        public async Task<IActionResult> CreateNewHiveAsync([FromBody] NewHiveModel model)
        {
            User user = (User)HttpContext.Items["User"];
            HiveManifest hive = await _yard.CreateHive(model.Title, model.Description, user._id);
            if (hive != null)
            {
                await _yard.AddHiveToUserSavedHives(hive._id, user._id);
                await _yard.SetHiveAsUsersDefaultHive(hive._id, user._id);
                return Ok(hive);
            }

            return StatusCode(500);
        }
        
        [HttpGet, Route("hive/{id}")]
        [AuthorizeEntry]
        public async Task<IActionResult> LoadHiveAsync(string id)
        {
            User user = (User)HttpContext.Items["User"];
            HiveManifest hive = await _yard.GetHiveById(id.Replace("_", "/"));
            if (hive != null)
            {
                await _yard.SetHiveAsUsersDefaultHive(hive._id, user._id);
                return Ok(hive);
            }

            return StatusCode(500);
        }

        [HttpGet, Route("saved")]
        [AuthorizeEntry]
        public async Task<IActionResult> LoadSavedHives()
        {
            User user = (User)HttpContext.Items["User"];
            HiveManifest[] savedHives = await _yard.GetSavedHives(user._id);
            return Ok(savedHives);
        }
    }
}