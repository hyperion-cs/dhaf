using Dhaf.Core;
using RestSharp;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public static class Actions
    {
        private static IRestClient _restClient;

        static Actions()
        {
            _restClient = new RestClient();
        }


        public static async Task<int> ExecuteSwitchoverCandidatesAndReturnExitCode(SwitchoverCandidatesOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover/candidates");

            var response = await _restClient.GetAsync<RestApiResponse<IEnumerable<SwitchoverCandidate>>>(request);
            ThrowIfRestApiException(response);

            var table = new Table();
            table.Border(TableBorder.Ascii2);
            table.Width = 50;
            table.Title = new TableTitle("Switchover candidates");
            table.AddColumns("Priority", "Name");

            table.Columns[0].Centered();
            table.Columns[1].Centered();

            var candidates = response.Data;
            foreach (var c in candidates)
            {
                table.AddRow(c.Priority.ToString(), c.Name);
            }

            AnsiConsole.Render(table);
            return 0;
        }


        public static async Task<int> ExecuteSwitchoverToAndReturnExitCode(SwitchoverToOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover?ncId={opt.NcName}");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                var error = response.Errors.First();
                Console.WriteLine($"Error {error.Code}: {error.Message}");
                return -1;
            }

            Console.WriteLine($"OK. A request for a switchover to has been sent.");

            return 0;
        }

        public static async Task<int> ExecuteNodeDecommissionAndReturnExitCode(NodeDecommissionOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"dhaf/node/decommission?name={opt.NodeName}");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                var error = response.Errors.First();
                Console.WriteLine($"Error {error.Code}: {error.Message}");
                return -1;
            }

            Console.WriteLine($"OK. The dhaf node has been successfully decommissioned.");

            return 0;
        }

        public static async Task<int> ExecuteSwitchoverPurgeAndReturnExitCode(SwitchoverPurgeOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover/purge");

            var response = await _restClient.GetAsync<RestApiResponse>(request);
            if (!response.Success)
            {
                var error = response.Errors.First();
                Console.WriteLine($"Error {error.Code}: {error.Message}.");
                return -1;
            }

            Console.WriteLine("OK. The switchover requirement has been purged.");

            return 0;
        }

        public static async Task<int> ExecuteStatusDhafAndReturnExitCode(StatusDhafOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"dhaf/status");

            var response = await _restClient.GetAsync<RestApiResponse<DhafStatus>>(request);
            if (!response.Success)
            {
                var error = response.Errors.First();
                Console.WriteLine($"Error {error.Code}: {error.Message}");
            }

            var dhafStatus = response.Data;

            var table = new Table();
            table.Border(TableBorder.Ascii2);
            table.Width = 50;
            table.Title = new TableTitle("Dhaf cluster status");
            table.AddColumns("Node name", "Healthy", "Role");

            table.Columns[0].Centered();
            table.Columns[1].Centered();
            table.Columns[2].Centered();

            foreach (var node in dhafStatus.Nodes)
            {
                table.AddRow(node.Name,
                    node.Healthy ? "[green]Yes[/]" : "[red]No[/]",
                    dhafStatus.Leader == node.Name ? "[white]Leader[/]" : "Follower");
            }

            AnsiConsole.Render(table);
            return 0;
        }

        public static async Task<int> ExecuteStatusServiceAndReturnExitCode(StatusServiceOptions opt)
        {
            const int TABLE_WIDTH = 80;

            await PrepareRestClient(opt);
            var request = new RestRequest($"service/status");

            var response = await _restClient.GetAsync<RestApiResponse<ServiceStatus>>(request);
            if (!response.Success)
            {
                var error = response.Errors.First();
                Console.WriteLine($"Error {error.Code}: {error.Message}");
            }

            var serviceStatus = response.Data;
            var isUp = serviceStatus.NetworkConfigurations
                .Any(x => x.Healthy) ? "[green]UP[/]" : "[red]DOWN[/]";

            var sw = serviceStatus.SwitchoverRequirement is null ? "NO" : $"YES (to <{serviceStatus.SwitchoverRequirement}>)";

            var summaryTable = new Table
            {
                Width = TABLE_WIDTH
            };

            summaryTable.Border(TableBorder.Ascii2);
            summaryTable.Title = new TableTitle("Summary");
            summaryTable.AddColumns("Key", "Value");
            summaryTable.AddRow("Service status", isUp);
            summaryTable.AddRow("Domain", serviceStatus.Domain);
            summaryTable.AddRow("Switchover is required", sw);
            AnsiConsole.Render(summaryTable);

            Console.WriteLine();
            var ncTable = new Table
            {
                Width = TABLE_WIDTH
            };

            ncTable.Border(TableBorder.Ascii2);
            ncTable.Title = new TableTitle("Network configurations");
            ncTable.AddColumns("Priority", "Name", "Healthy", "Status");
            ncTable.Columns[0].Centered();
            ncTable.Columns[1].Centered();
            ncTable.Columns[2].Centered();
            ncTable.Columns[3].Centered();

            foreach (var nc in serviceStatus.NetworkConfigurations)
            {
                var status = nc.Healthy ? "READY" : "DISABLED";
                if (nc.Name == serviceStatus.CurrentNcName)
                {
                    status = "[white]CURRENT[/]";
                }

                ncTable.AddRow(nc.Priority.ToString(), nc.Name,
                    nc.Healthy ? "[green]Yes[/]" : "[red]No[/]", status);
            }

            AnsiConsole.Render(ncTable);
            return 0;
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

        private static void ThrowIfRestApiException(RestApiResponse response)
        {
            if (!response.Success)
            {
                var error = response.Errors.First();
                throw new RestApiException(error.Code, error.Message);
            }
        }
    }
}
