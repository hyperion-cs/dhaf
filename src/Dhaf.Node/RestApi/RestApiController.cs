using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public class RestApiController : WebApiController
    {
        protected readonly IDhafNode _dhafNode;
        protected readonly ILogger<IDhafNode> _logger;

        public RestApiController()
        {
            throw new Exception("The default constructor is not possible.");
        }

        public RestApiController(IDhafNode dhafNode, ILogger<IDhafNode> logger)
        {
            _dhafNode = dhafNode;
            _logger = logger;
        }


        [Route(HttpVerbs.Get, "/ping")]
        public async Task<string> Ping()
        {
            return "pong";
        }

        [Route(HttpVerbs.Get, "/switchover")]
        public async Task<RestApiResponse> Switchover([QueryField] string ncId)
        {
            await _dhafNode.Switchover(ncId);
            return new RestApiResponse { Success = true };
        }
    }
}
