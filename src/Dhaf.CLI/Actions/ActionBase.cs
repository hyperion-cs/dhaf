using Dhaf.Core;
using Dhaf.Node;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        private static IRestClient _restClient;

        static Actions()
        {
            _restClient = new RestClient();
        }

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
            _restClient.BaseUrl = uri;
        }

        private static void PrintErrors(IEnumerable<RestApiError> errors)
        {
            var error = errors.First();
            Console.WriteLine($"Error {error.Code}: {error.Message}");
        }
    }
}
