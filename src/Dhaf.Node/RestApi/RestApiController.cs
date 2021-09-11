using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

        [Route(HttpVerbs.Get, "/switchover/purge")]
        public async Task<RestApiResponse> SwitchoverPurge()
        {
            await _dhafNode.PurgeManualSwitchover();
            return new RestApiResponse { Success = true };
        }

        [Route(HttpVerbs.Get, "/switchover/candidates")]
        public async Task<RestApiResponse> SwitchoverСandidates()
        {
            var candidates = await _dhafNode.GetSwitchoverCandidates();
            return new RestApiResponse<IEnumerable<SwitchoverCandidate>> { Success = true, Data = candidates };
        }

        [Route(HttpVerbs.Get, "/service/status")]
        public async Task<RestApiResponse> ServiceStatus()
        {
            var status = await _dhafNode.GetServiceStatus();
            return new RestApiResponse<ServiceStatus> { Success = true, Data = status };
        }

        [Route(HttpVerbs.Get, "/dhaf/status")]
        public async Task<RestApiResponse> DhafStatus()
        {
            var status = await _dhafNode.GetDhafStatus();
            return new RestApiResponse<DhafStatus> { Success = true, Data = status };
        }
    }
}
