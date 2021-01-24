using System;
using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;

namespace Consensus.Backend.Data
{
    public class ArangoDb : IArangoDb
    {
        private string _uri = "http://127.0.0.1:8529";
        private string _dbName = "consensus";
        private string _username = "root";
        private string _password = "gskv9988";
        private ArangoDBClient _client;

        public ArangoDb()
        {
            var transport = HttpApiTransport.UsingBasicAuth(new Uri(_uri), _dbName, _username, _password);
            _client = new ArangoDBClient(transport);
        }

        public ArangoDBClient GetClient()
        {
            return _client;
        }
    }
}