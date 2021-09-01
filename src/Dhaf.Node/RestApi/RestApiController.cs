using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public class RestApiController : WebApiController
    {
        [Route(HttpVerbs.Get, "/")]
        public async Task<string> GetIndex()
        {
            return "OK";
        }
    }
}
