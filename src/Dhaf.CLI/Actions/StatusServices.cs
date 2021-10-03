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
        public static async Task<int> ExecuteStatusServicesAndReturnExitCode(StatusServicesOptions opt)
        {
            const int TABLE_WIDTH = 80;

            await PrepareRestClient(opt);
            var request = new RestRequest($"services/status");

            var response = await _restClient.GetAsync<RestApiResponse<List<ServiceStatus>>>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
            }

            foreach (var serviceStatus in response.Data)
            {
                var isUp = serviceStatus.EntryPoints
                    .Any(x => x.Healthy) ? "[green]UP[/]" : "[red]DOWN[/]";

                var sw = serviceStatus.SwitchoverRequirement is null ? "NO" : $"YES (to <{serviceStatus.SwitchoverRequirement}>)";

                var summaryTable = new Table
                {
                    Width = TABLE_WIDTH
                };

                summaryTable.Border(TableBorder.Ascii2);
                summaryTable.Title = new TableTitle($"Service \"{serviceStatus.Name}\" -> Summary");
                summaryTable.AddColumns("Key", "Value");
                summaryTable.AddRow("Service status", isUp);
                summaryTable.AddRow("Domain", serviceStatus.Domain);
                summaryTable.AddRow("Switchover is required", sw);
                AnsiConsole.Render(summaryTable);

                Console.WriteLine();
                var epTable = new Table
                {
                    Width = TABLE_WIDTH
                };

                epTable.Border(TableBorder.Ascii2);
                epTable.Title = new TableTitle($"Service \"{serviceStatus.Name}\" -> Entry points");
                epTable.AddColumns("Priority", "Name", "Healthy", "Status");
                epTable.Columns[0].Centered();
                epTable.Columns[1].Centered();
                epTable.Columns[2].Centered();
                epTable.Columns[3].Centered();

                foreach (var nc in serviceStatus.EntryPoints)
                {
                    var status = nc.Healthy ? "READY" : "DISABLED";
                    if (nc.Name == serviceStatus.CurrentEntryPointName)
                    {
                        status = "[white]CURRENT[/]";
                    }

                    epTable.AddRow(nc.Priority.ToString(), nc.Name,
                        nc.Healthy ? "[green]Yes[/]" : "[red]No[/]", status);
                }

                AnsiConsole.Render(epTable);

                const char SEP_CHAR = '=';
                Console.WriteLine(new string(SEP_CHAR, TABLE_WIDTH) + "\n");
            }

            return 0;
        }
    }
}
