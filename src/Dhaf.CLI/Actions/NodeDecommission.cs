using Dhaf.Node;
using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        public static async Task<int> ExecuteNodeDecommissionAndReturnExitCode(NodeDecommissionOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"dhaf/node/decommission?name={opt.NodeName}");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
            }

            Console.WriteLine($"OK. The dhaf node has been successfully decommissioned.");

            return 0;
        }
    }
}
