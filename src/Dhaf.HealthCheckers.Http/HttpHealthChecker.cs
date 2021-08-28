using Dhaf.Core;
using RestSharp;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Http
{
    public class HttpHealthChecker : IHealthChecker
    {
        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        private IRestClient _client;

        public string ExtensionName => "web";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(HealthCheckerInitOptions options)
        {
            Console.WriteLine("web switcher init...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;
            _serviceConfig = options.ClusterServiceConfig;

            _client = new RestClient()
            {
                FollowRedirects = _config.FollowRedirects ?? _internalConfig.DefFollowRedirects,
                Timeout = _config.Timeout ?? _internalConfig.DefTimeout
            };
        }

        public async Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            if (_client == null)
            {
                throw new Exception("The HTTP client is not initialized.");
            }

            var schema = PrepareAndCheckSchema(_config.Schema ?? _internalConfig.HttpSchema);
            var port = _config.Port
                ?? (schema == _internalConfig.HttpSchema
                       ? _internalConfig.DefHttpPort : _internalConfig.DefHttpsPort);

            var host = _serviceConfig.Hosts.FirstOrDefault(x => x.Name == options.HostName);

            var uri = new Uri($"{schema}://{host.IP}:{port}");

            const int RETRYING_INIT_VALUE = 0;

            var request = new RestRequest(uri);
            request.AddHeaders(_config.Headers ?? _internalConfig.DefHeaders);

            var retries = _config.Retries ?? _internalConfig.DefRetries;
            var expectedCodes = _config.ExpectedCodes ?? _internalConfig.DefExpectedCodes;
            var expectedResponseBody = _config.ExpectedResponseBody ?? _internalConfig.DefExpectedResponseBody;

            var retrying = RETRYING_INIT_VALUE;
            while (++retrying <= retries)
            {
                var method = Enum.Parse<Method>(_config.Method ?? _internalConfig.DefMethod);
                var response = await _client.ExecuteAsync(request, method);

                if (response.ResponseStatus != ResponseStatus.Completed
                    || !CheckHttpCode(response.StatusCode, expectedCodes)
                    || !response.Content.Contains(expectedResponseBody))
                {
                    Console.WriteLine($"{options.HostName} aka {uri} -> Unhealthy. Try again...");
                    continue;
                }
                Console.WriteLine($"{options.HostName} aka {uri} -> Healthy :)");
                return new HealthStatus { Healthy = true };
            }

            Console.WriteLine($"{options.HostName} aka {uri} -> Unhealthy.");
            return new HealthStatus { Healthy = false };
        }

        protected string PrepareAndCheckSchema(string schema)
        {
            schema = schema
                .Trim()
                .ToLower();

            if (schema != _internalConfig.HttpSchema && schema != _internalConfig.HttpsSchema)
            {
                throw new ArgumentException("Incorrect URI scheme is specified (only http/https allowed).");
            }

            return schema;
        }

        protected bool CheckHttpCode(HttpStatusCode code, string codeMasks)
        {
            // TODO: Do configuration checks and compile regular expressions beforehand and only once.

            var exceptedCodes = codeMasks
                .Split(_internalConfig.ExpectedCodesSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            var wildcard = _internalConfig.ExpectedCodesWildcard;

            // Only valid HTTP response codes are allowed.
            var correctCodeRegex = new Regex("^[1-5" + wildcard + "][0-9" + wildcard + "]{2}$");
            var isAllExpectedCodesOk = exceptedCodes.All(x => correctCodeRegex.IsMatch(x));

            if (!isAllExpectedCodesOk)
            {
                throw new ArgumentException("There is a syntax error in the list of allowed HTTP response codes.");
            }

            var checkCodeRegexes = exceptedCodes.Select(x => new Regex("^" + x.Replace(wildcard, "[0-9]") + "$"));
            var isCodeOk = checkCodeRegexes.Any(x => x.IsMatch(((int)code).ToString()));

            return isCodeOk;
        }
    }
}
