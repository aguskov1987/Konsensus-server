using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
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

        // ! this method might be too long
        public async Task AddUserAsParticipant(string userId, string hiveId, bool newStatement = false)
        {
            string query = @"FOR relation IN @@collection
                                FILTER relation._from == @userId AND relation._to == @hiveId
                                RETURN relation._to";
            var parameters = new Dictionary<string, object>
            {
                ["@collection"] = Connections.UserHasParticipated.ToString(),
                ["userId"] = userId,
                ["hiveId"] = hiveId
            };
            bool existingParticipant = false;
            CursorResponse<string> existing = await _client.Cursor.PostCursorAsync<string>(query, parameters);
            if (!string.IsNullOrEmpty(existing.Result.FirstOrDefault()))
            {
                existingParticipant = true;
            }
            
            // if this is the first participation, add a record
            if (!existingParticipant)
            {
                await _client.Document.PostDocumentAsync(Connections.UserHasParticipated.ToString(),
                    new {_from = userId, _to = hiveId});
            }
            
            // also add a participation remark to the hive manifest
            string hiveKey = hiveId.Split("/")[1];
            DateTime today = DateTime.Now.Date;
            HiveManifest hive = await _client.Document
                .GetDocumentAsync<HiveManifest>(Collections.HiveManifests.ToString(), hiveKey);
            if (hive != null && !existingParticipant)
            {
                ParticipationCount[] counts = hive.Participation.Where(p => p.Date == today).ToArray();
                if (counts.Length == 1)
                {
                    counts[0].NumberOfParticipants++;
                }
                else
                {
                    hive.Participation.Add(new ParticipationCount
                    {
                        Date = today,
                        NumberOfParticipants = 1
                    });
                }

                if (newStatement)
                {
                    var statementCounts = hive.NumberOfStatements.Where(p => p.Date == today).ToArray();
                    if (statementCounts.Length == 1)
                    {
                        statementCounts[0].NumberOfStatements++;
                    }
                    else
                    {
                        hive.NumberOfStatements.Add(new StatementCount
                        {
                            Date = today,
                            NumberOfStatements = 1
                        });
                    }
                }

                await _client.Document.PutDocumentAsync(hiveId, hive);
            }
            else if (hive != null && newStatement)
            {
                var statementCounts = hive.NumberOfStatements.Where(p => p.Date == today).ToArray();
                if (statementCounts.Length == 1)
                {
                    statementCounts[0].NumberOfStatements++;
                }
                else
                {
                    hive.NumberOfStatements.Add(new StatementCount
                    {
                        Date = today,
                        NumberOfStatements = 1
                    });
                }
                
                await _client.Document.PutDocumentAsync(hiveId, hive);
            }
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