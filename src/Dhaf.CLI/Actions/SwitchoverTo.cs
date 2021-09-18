using Dhaf.Node;
using RestSharp;
using System;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        public static async Task<int> ExecuteSwitchoverToAndReturnExitCode(SwitchoverToOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover?ncId={opt.NcName}&serviceName={opt.ServiceName}");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
            }

            Console.WriteLine($"OK. A request for a switchover to has been sent.");

            return 0;
        }
    }
}
