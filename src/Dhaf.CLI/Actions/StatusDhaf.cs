using Dhaf.Node;
using RestSharp;
using Spectre.Console;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    public static partial class Actions
    {
        public static async Task<int> ExecuteStatusDhafAndReturnExitCode(StatusDhafOptions opt)
        {
            await PrepareRestClient(opt);
            var request = new RestRequest($"dhaf/status");

            var response = await _restClient.GetAsync<RestApiResponse<DhafStatus>>(request);
            if (!response.Success)
            {
                PrintErrors(response.Errors);
                return -1;
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

            AnsiConsole.Write(table);
            return 0;
        }
    }
}
