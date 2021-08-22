using Dhaf.Core;
using RestSharp;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Http
{
    public class HttpHealthChecker : IHealthChecker
    {
        private Config _config;
        private InternalConfig _internalConfig;

        private IRestClient _client;

        public string ExtensionName => "web";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(HealthCheckerInitOptions options)
        {
            Console.WriteLine("web switcher init...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            var x = 5;

            /*schema = PrepareAndCheckSchema(schema);
            port ??= (schema == _options.HttpSchema ? _options.DefHttpPort : _options.DefHttpsPort);

            var uri = new Uri($"{schema}://{host}:{port}");

            _client = new RestClient(uri)
            {
                FollowRedirects = FollowRedirects,
                Timeout = Timeout
            };*/
        }

        public Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            throw new NotImplementedException();
        }

        public async Task InitializeHttpClient(string schema, string host, int? port = null)
        {

        }

        public async Task<HealthStatus> Check()
        {
            /*if (_client == null)
            {
                throw new Exception("The HTTP client is not initialized.");
            }

            const int RETRYING_INIT_VALUE = 0;

            var request = new RestRequest(Path);
            request.AddHeaders(Headers);

            var retrying = RETRYING_INIT_VALUE;
            while (++retrying <= Retries)
            {
                var method = Enum.Parse<Method>(Method);
                var response = await _client.ExecuteAsync(request, method);

                if (response.ResponseStatus != ResponseStatus.Completed
                    || !CheckHttpCode(response.StatusCode, ExpectedCodes)
                    || !response.Content.Contains(ExpectedResponseBody))
                {
                    continue;
                }

                return new HealthStatus { Healthy = true };
            }*/

            return new HealthStatus { Healthy = true };
        }

        protected string PrepareAndCheckSchema(string schema)
        {
            /*schema = schema
                .Trim()
                .ToLower();

            if (schema != _internalOptions.HttpSchema && schema != _internalOptions.HttpsSchema)
            {
                throw new ArgumentException("Incorrect URI scheme is specified (only http/https allowed).");
            }*/

            return schema;
        }

        protected bool CheckHttpCode(HttpStatusCode code, string codeMasks)
        {
            /*
            // TODO: Do configuration checks and compile regular expressions beforehand and only once.

            var exceptedCodes = codeMasks
                .Split(_internalOptions.ExpectedCodesSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            var wildcard = _internalOptions.ExpectedCodesWildcard;

            // Only valid HTTP response codes are allowed.
            var correctCodeRegex = new Regex("^[1-5" + wildcard + "][0-9" + wildcard + "]{2}$");
            var isAllExpectedCodesOk = exceptedCodes.All(x => correctCodeRegex.IsMatch(x));

            if (!isAllExpectedCodesOk)
            {
                throw new ArgumentException("There is a syntax error in the list of allowed HTTP response codes.");
            }

            var checkCodeRegexes = exceptedCodes.Select(x => new Regex("^" + x.Replace(wildcard, "[0-9]") + "$"));
            var isCodeOk = checkCodeRegexes.Any(x => x.IsMatch(((int)code).ToString()));

            return isCodeOk;*/
            return true;
        }
    }
}
