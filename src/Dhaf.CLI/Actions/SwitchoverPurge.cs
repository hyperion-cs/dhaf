using Dhaf.Node;
using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        public static async Task<int> ExecuteSwitchoverPurgeAndReturnExitCode(SwitchoverPurgeOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover/purge");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
            }

            Console.WriteLine("OK. The switchover requirement has been purged.");

            return 0;
        }
    }
}
