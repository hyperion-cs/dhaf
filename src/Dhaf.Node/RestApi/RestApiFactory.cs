using EmbedIO;
using EmbedIO.WebApi;
using Swan.Logging;
using System.Text;

namespace Dhaf.Node

{
    public class RestApiFactory
    {
        public WebServer CreateWebServer(string url)
        {
            Logger.NoLogging();

            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithWebApi("/", m => m.WithController<RestApiController>());

            server.HandleHttpException(async (context, exception) =>
            {
                await context.SendStringAsync(string.Empty, "text/html", Encoding.UTF8);
            });

            return server;
        }
    }
}
