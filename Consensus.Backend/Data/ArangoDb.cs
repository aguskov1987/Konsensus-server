using System;
using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.Extensions.Configuration;

namespace Consensus.Backend.Data
{
    public class ArangoDb : IArangoDb
    {
        private readonly ArangoDBClient _client;

        public ArangoDb(IConfiguration config)
        {
            var url = config["Storage:Url"];
            var dbName = config["Storage:DbName"];
            var username = config["Storage:Username"];
            var password = config["Storage:Password"];
            var transport = HttpApiTransport.UsingBasicAuth(new Uri(url), dbName, username, password);
            _client = new ArangoDBClient(transport);
        }

        public ArangoDBClient GetClient()
        {
            return _client;
        }
    }
}