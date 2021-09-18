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
        public async Task<RestApiResponse> Switchover([QueryField(true)] string serviceName, [QueryField(true)] string ncId)
        {
            await _dhafNode.Switchover(serviceName, ncId);
            return new RestApiResponse { Success = true };
        }

        [Route(HttpVerbs.Get, "/switchover/purge")]
        public async Task<RestApiResponse> SwitchoverPurge([QueryField(true)] string serviceName)
        {
            await _dhafNode.PurgeSwitchover(serviceName);
            return new RestApiResponse { Success = true };
        }

        [Route(HttpVerbs.Get, "/switchover/candidates")]
        public async Task<RestApiResponse> SwitchoverСandidates([QueryField(true)] string serviceName)
        {
            var candidates = await _dhafNode.GetSwitchoverCandidates(serviceName);
            return new RestApiResponse<IEnumerable<SwitchoverCandidate>> { Success = true, Data = candidates };
        }

        [Route(HttpVerbs.Get, "/service/status")]
        public async Task<RestApiResponse> ServiceStatus([QueryField(true)] string serviceName)
        {
            var status = await _dhafNode.GetServiceStatus(serviceName);
            return new RestApiResponse<ServiceStatus> { Success = true, Data = status };
        }

        [Route(HttpVerbs.Get, "/services/status")]
        public async Task<RestApiResponse> ServicesStatus()
        {
            var statuses = await _dhafNode.GetServicesStatus();
            return new RestApiResponse<IEnumerable<ServiceStatus>> { Success = true, Data = statuses };
        }

        [Route(HttpVerbs.Get, "/dhaf/status")]
        public async Task<RestApiResponse> DhafStatus()
        {
            var status = await _dhafNode.GetDhafClusterStatus();
            return new RestApiResponse<DhafStatus> { Success = true, Data = status };
        }

        [Route(HttpVerbs.Get, "/dhaf/node/decommission")]
        public async Task<RestApiResponse> DhafNodeDecommission([QueryField(true)] string name)
        {
            await _dhafNode.DecommissionDhafNode(name);
            return new RestApiResponse { Success = true };
        }
    }
}
