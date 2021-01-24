using System.Threading.Tasks;
using ArangoDBNetStandard.ViewApi.Models;

namespace ArangoDBNetStandard.ViewApi
{
    public interface IViewApiClient
    {
        Task<PostViewResponse> PostView(PostViewBody body);
    }
}