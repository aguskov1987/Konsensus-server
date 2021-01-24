using System;
using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.Extensions.Configuration;

namespace Consensus.Backend.Data
{
    public class ArangoDb : IArangoDb
    {
        private readonly IConfiguration _config;
        private readonly ArangoDBClient _client;

        public ArangoDb(IConfiguration config)
        {
            _config = config;
            var url = _config["Storage:Url"];
            var dbName = _config["Storage:DbName"];
            var username = _config["Storage:Username"];
            var password = _config["Storage:Password"];
            var transport = HttpApiTransport.UsingBasicAuth(new Uri(url), dbName, username, password);
            _client = new ArangoDBClient(transport);
        }

        public ArangoDBClient GetClient()
        {
            return _client;
        }
    }
}