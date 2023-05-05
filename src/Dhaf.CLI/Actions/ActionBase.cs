using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dhaf.Core;
using Dhaf.Node;
using RestSharp;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        private static IRestClient _restClient;
    
        private static async Task<ClusterConfig> GetClusterConfig(IConfigPath opt)
        {
            var clusterConfigParser = new ClusterConfigParser(opt.Config);
            var config = await clusterConfigParser.Parse();

            return config;
        }

        private static async Task PrepareRestClient(IConfigPath opt)
        {
            var config = await GetClusterConfig(opt);
            var webApiEndpoint = config.Dhaf.WebApi;
            var uri = new Uri($"http://{webApiEndpoint.Host}:{webApiEndpoint.Port}/");

            var options = new RestClientOptions { BaseUrl = uri };
            _restClient = new RestClient(options);
        }

        private static void PrintErrors(IEnumerable<RestApiError> errors)
        {
            var error = errors.First();
            Console.WriteLine($"Error {error.Code}: {error.Message}");
        }
    }
}
