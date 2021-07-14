using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArangoDBNetStandard.Serialization;
using ArangoDBNetStandard.Transport;
using ArangoDBNetStandard.ViewApi.Models;

namespace ArangoDBNetStandard.ViewApi
{
    public class ViewApiClient: ApiClientBase, IViewApiClient
    {
        protected IApiClientTransport _transport;
        protected string _collectionApiPath = "_api/view";
        
        public ViewApiClient(IApiClientTransport transport)
            : base(new JsonNetApiClientSerialization())
        {
            _transport = transport;
        }
        
        public ViewApiClient(IApiClientTransport transport, IApiClientSerialization serializer)
            : base(serializer)
        {
            _transport = transport;
        }
        
        public virtual async Task<PostViewResponse> PostView(PostViewBody body)
        {
            string name = $"\"name\": \"{body.Name}\"";
            string type = $"\"type\": \"arangosearch\"";
            string[] propsToIndex = body.FieldsToIndex
                .Select(f => $"\"{f}\": {{\"analyzers\": [\"text_en\"]}}")
                .ToArray();

            string links =
                $"\"links\": {{\"{body.CollectionName}\": {{\"analyzers\": [\"identity\"], \"fields\": {{{string.Join(", ", propsToIndex)}}}}}}}";
            string payload = $"{{{name}, {type}, {links}}}";
            
            byte[] bytes = Encoding.ASCII.GetBytes(payload);
            
            using (var response = await _transport.PostAsync(_collectionApiPath, bytes))
            {
                var stream = await response.Content.ReadAsStreamAsync();
                if (response.IsSuccessStatusCode)
                {
                    return DeserializeJsonFromStream<PostViewResponse>(stream);
                }
                throw await GetApiErrorException(response);
            }
        }
    }
}