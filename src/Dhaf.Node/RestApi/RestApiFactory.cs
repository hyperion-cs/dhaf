using EmbedIO;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using Swan.Logging;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dhaf.Node

{
    public class RestApiFactory
    {
        protected static async Task SerializationCallback(IHttpContext context, object data)
        {
            Validate.NotNull(nameof(context), context).Response.ContentType = MimeType.Json;
            using var text = context.OpenResponseText(new UTF8Encoding(false));
            await text.WriteAsync(JsonSerializer.Serialize(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })).ConfigureAwait(false);
        }

        public WebServer CreateWebServer(string url, IDhafNode dhafNode, ILogger<IDhafNode> logger)
        {
            Logger.NoLogging();

            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithWebApi("/", SerializationCallback,
                    m => m.WithController(() => new RestApiController(dhafNode, logger))
            );

            server.HandleUnhandledException(async (context, exception) =>
            {
                context.Response.StatusCode = 500;
                var errors = new List<RestApiError>()
                {
                    new RestApiError { Code = -1, Message = "Internal error." }
                };

                if (exception is RestApiException restApiExp)
                {
                    context.Response.StatusCode = 400;
                    errors = new List<RestApiError>()
                    {
                        new RestApiError { Code = restApiExp.Code, Message = restApiExp.Message }
                    };
                }

                await context.SendDataAsync(SerializationCallback, new RestApiResponse
                {
                    Success = false,
                    Errors = errors
                });
            });

            server.HandleHttpException(async (context, exception) =>
            {
                context.Response.StatusCode = 400;

                var msg = exception.StatusCode == 404 ? "Endpoint not found." : exception.Message;
                var errors = new List<RestApiError>()
                {
                    new RestApiError { Code = exception.StatusCode, Message = msg }
                };

                await context.SendDataAsync(SerializationCallback, new RestApiResponse
                {
                    Success = false,
                    Errors = errors
                });
            });

            return server;
        }
    }
}
