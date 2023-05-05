using Dhaf.Node;
using RestSharp;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        public static async Task<int> ExecuteSwitchoverCandidatesAndReturnExitCode(SwitchoverCandidatesOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"switchover/candidates?serviceName={opt.ServiceName}");

            var response = await _restClient.GetAsync<RestApiResponse<IEnumerable<SwitchoverCandidate>>>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
            }

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

            AnsiConsole.Write(table);
            return 0;
        }
    }
}
