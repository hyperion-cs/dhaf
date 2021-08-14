using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers
{
    public class HttpChecker
    {
        private readonly IRestClient _client;

        protected const int HTTP_DEF_PORT = 80;
        protected const int HTTPS_DEF_PORT = 443;
        protected const string HTTP_SCHEMA = "http";
        protected const string HTTPS_SCHEMA = "https";

        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public bool FollowRedirects { get; set; } = false;
        public int Timeout { get; set; } = 5 * 1000;
        public int Retries { get; set; } = 2;
        public string ExpectedCodes { get; set; } = "200";
        public string ExpectedResponseBody { get; set; } = string.Empty;

        public HttpChecker(string schema, string host, int? port = null)
        {
            schema = PrepareAndCheckSchema(schema);
            port ??= (schema == HTTP_SCHEMA ? HTTP_DEF_PORT : HTTPS_DEF_PORT);

            var uri = new Uri($"{schema}://{host}:{port}");

            _client = new RestClient(uri)
            {
                FollowRedirects = FollowRedirects,
                Timeout = Timeout
            };
        }

        protected static string PrepareAndCheckSchema(string schema)
        {
            schema = schema
                .Trim()
                .ToLower();

            if (schema != HTTP_SCHEMA && schema != HTTPS_SCHEMA)
            {
                throw new ArgumentException("Incorrect URI scheme is specified (only http/https allowed).");
            }

            return schema;
        }

        public async Task<Status> Check()
        {
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

                return new Status { Healthy = true, AttemptsCount = retrying };
            }

            return new Status { Healthy = false, AttemptsCount = Retries };
        }

        protected bool CheckHttpCode(HttpStatusCode code, string codeMasks)
        {
            // TODO: Do configuration checks and compile regular expressions beforehand and only once.

            const string CODES_SEPARATOR = ",";
            const string WILDCARD = "X";

            var exceptedCodes = codeMasks
                .Split(CODES_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            // Only valid HTTP response codes are allowed.
            var correctCodeRegex = new Regex("^[1-5" + WILDCARD + "][0-9" + WILDCARD + "]{2}$");
            var isAllExpectedCodesOk = exceptedCodes.All(x => correctCodeRegex.IsMatch(x));

            if (!isAllExpectedCodesOk)
            {
                throw new ArgumentException("There is a syntax error in the list of allowed HTTP response codes.");
            }

            var checkCodeRegexes = exceptedCodes.Select(x => new Regex("^" + x.Replace(WILDCARD, "[0-9]") + "$"));
            var isCodeOk = checkCodeRegexes.Any(x => x.IsMatch(((int)code).ToString()));

            return isCodeOk;
        }
    }
}
