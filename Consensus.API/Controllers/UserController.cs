using System.Threading.Tasks;
using Consensus.API.Auth;
using Consensus.API.Models.Incoming;
using Consensus.Backend.Models;
using Consensus.Backend.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consensus.API.Controllers
{
    [Route("user")]
    public class UserController : Controller
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost, Route("authenticate"), AllowAnonymous]
        public async Task<IActionResult> AuthenticateAsync([FromBody] LoginModel loginModel)
        {
            string token = await _userService.AuthenticateAsync(loginModel.Username, loginModel.Password);
            return Ok(token);
        }

        [HttpGet, Route("user"), AuthorizeEntry]
        public IActionResult GetUser()
        {
            return Ok((User)HttpContext.Items["User"]);
        }
    }
}