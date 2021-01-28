using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using Consensus.Backend.Data;
using Consensus.Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace Consensus.Backend.User
{
    public class UserService : IUserService
    {
        private int _tokenExpiration = 7;
        private string _secret = "The authenticate request model defines the parameters for incoming requests";
        private readonly ArangoDBClient _client;

        public UserService(IArangoDb db)
        {
            _client = db.GetClient();
        }

        public async Task<string> AuthenticateAsync(string username, string password)
        {
            string query = $"FOR u IN {Collections.Users.ToString()} FILTER u.Username == \"{username}\" AND u.Password == \"{password}\" RETURN u";
            var cursor = await _client.Cursor.PostCursorAsync<Models.User>(query);
            var user = cursor.Result.FirstOrDefault();
            if (user != null)
            {
                return GenerateJwtToken(user);
            }

            throw new FileNotFoundException();
        }

        public async Task<Models.User> GetByIdAsync(string userId)
        {
            string query = $"FOR u IN {Collections.Users} FILTER u._id == \"{userId}\" RETURN u";
            var cursor = await _client.Cursor.PostCursorAsync<Models.User>(query);
            var user = cursor.Result.FirstOrDefault();
            if (user != null)
            {
                return user;
            }

            throw new FileNotFoundException();
        }

        private string GenerateJwtToken(Models.User user)
        {
            // generate token that is valid for 7 days
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", user._id) }),
                Expires = DateTime.UtcNow.AddDays(_tokenExpiration),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}