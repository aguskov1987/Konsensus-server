using System.Threading.Tasks;
using Consensus.API.Auth;
using Consensus.Backend.Models;
using Consensus.Backend.Saved;
using Microsoft.AspNetCore.Mvc;

namespace Consensus.API.Controllers
{
    public class SavedHivesController : Controller
    {
        private readonly ISavedHivesService _service;
        
        public SavedHivesController(ISavedHivesService service)
        {
            _service = service;
        }
        
        [HttpGet, Route("saved"), AuthorizeEntry]
        public async Task<IActionResult> LoadSavedHivesAsync()
        {
            User user = (User) HttpContext.Items["User"];
            HiveManifest[] savedHives = await _service.GetSavedHives(user.Id);
            return Ok(savedHives);
        }
    }
}