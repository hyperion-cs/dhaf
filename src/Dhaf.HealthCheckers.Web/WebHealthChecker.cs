using Dhaf.Core;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Web
{
    public class WebHealthChecker : IHealthChecker
    {
        private IRestClient _client;
        private ILogger<IHealthChecker> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        protected string _requestSchema;

        public string ExtensionName => "web";
        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} hc]";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);


        public async Task Init(HealthCheckerInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger;

            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            const int MS_IN_SEC = 1000;

            _client = new RestClient()
            {
                FollowRedirects = _config.FollowRedirects ?? _internalConfig.DefFollowRedirects,
                Timeout = (_config.Timeout ?? _internalConfig.DefTimeout) * MS_IN_SEC
            };

            var ignoreSslErrors = _config.IgnoreSslErrors ?? _internalConfig.DefIgnoreSslErrors;
            if (ignoreSslErrors)
            {
                _client.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            _requestSchema = PrepareSchema(_config.Schema ?? _internalConfig.HttpSchema);

            await ConfigCheck();
            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            if (_client == null)
            {
                throw new Exception("The HTTP client is not initialized.");
            }

            const int RETRYING_INIT_VALUE = 0;

            var port = _config.Port
                ?? (_requestSchema == _internalConfig.HttpSchema
                       ? _internalConfig.DefHttpPort : _internalConfig.DefHttpsPort);

            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);

            var path = string.IsNullOrEmpty(_config.Path) ? _internalConfig.DefPath : _config.Path;
            var uri = new Uri($"{_requestSchema}://{entryPoint.IP}:{port}/{path}");

            DownReason? downReason = null;
            var request = new RestRequest(uri);

            var headers = _config.Headers ?? _internalConfig.DefHeaders;
            request.AddHeaders(headers);

            var hostHeader = headers.Keys.FirstOrDefault(x => x.ToUpper() == _internalConfig.HostHeader.ToUpper());
            if (hostHeader is null && (_config.DomainForwarding ?? _internalConfig.DefDomainForwarding))
            {
                request.AddHeader(_internalConfig.HostHeader, _serviceConfig.Domain);
            }

            var retries = _config.Retries ?? _internalConfig.DefRetries;
            var expectedCodes = _config.ExpectedCodes ?? _internalConfig.DefExpectedCodes;
            var expectedResponseBody = _config.ExpectedResponseBody ?? _internalConfig.DefExpectedResponseBody;

            var retrying = RETRYING_INIT_VALUE;
            while (++retrying <= retries)
            {
                var method = Enum.Parse<Method>(_config.Method ?? _internalConfig.DefMethod);
                var response = await _client.ExecuteAsync(request, method);

                if (response.ErrorException is not null)
                {
                    downReason = GetSslPolicyErrors(response.ErrorException).Any()
                        ? DownReason.SslPolicyErrors : DownReason.NetworkOrHttpFrameworkException;
                    continue;
                }

                if (response.ResponseStatus == ResponseStatus.TimedOut)
                {
                    downReason = DownReason.Timeout;
                    continue;
                }

                if (response.ResponseStatus != ResponseStatus.Completed)
                {
                    downReason = DownReason.NotCompleted;
                    continue;
                }

                if (!CheckHttpCode(response.StatusCode, expectedCodes))
                {
                    downReason = DownReason.UnexpectedHttpCode;
                    continue;
                }

                if (!response.Content.Contains(expectedResponseBody))
                {
                    downReason = DownReason.UnexpectedResponseBody;
                    continue;
                }

                return new HealthStatus { Healthy = true };
            }

            return new HealthStatus { Healthy = false, ReasonCode = (int)downReason };
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }

        public async Task<string> ResolveUnhealthinessReasonCode(int code)
        {
            return DownReasonResolver.Resolve(code);
        }

        protected IEnumerable<string> GetSslPolicyErrors(Exception sourceExp)
        {
            var e = sourceExp;
            while (e.InnerException is not null)
            {
                e = e.InnerException;
            }

            if (e is System.Security.Authentication.AuthenticationException)
            {
                var speNames = Enum.GetNames<SslPolicyErrors>();
                var speErrors = speNames.Where(x => e.Message.Contains(x));

                return speErrors;
            }

            return new List<string>();
        }

        protected bool CheckHttpCode(HttpStatusCode code, string codeMasks)
        {
            var exceptedCodes = codeMasks
                .Split(_internalConfig.ExpectedCodesSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            var wildcard = _internalConfig.ExpectedCodesWildcard;
            var checkCodeRegexes = exceptedCodes.Select(x => new Regex("^" + x.Replace(wildcard, "[0-9]") + "$"));
            var isCodeOk = checkCodeRegexes.Any(x => x.IsMatch(((int)code).ToString()));

            return isCodeOk;
        }

        protected async Task ConfigCheck()
        {
            if (_config.ExpectedCodes is not null)
            {
                await CheckHttpCodesMask(_config.ExpectedCodes);
            }

            await CheckHttpCodesMask(_internalConfig.DefExpectedCodes);

            if (_config.Schema is not null)
            {
                var preparedSchema = PrepareSchema(_config.Schema);

                if (preparedSchema != _internalConfig.HttpSchema && preparedSchema != _internalConfig.HttpsSchema)
                {
                    throw new ConfigParsingException(1802, $"{Sign} Incorrect URI scheme is specified (only http/https allowed).");
                }
            }

            if (_config.Timeout is not null
                && (_config.Timeout < _internalConfig.MinTimeout || _config.Timeout > _internalConfig.MaxTimeout))
            {
                throw new ConfigParsingException(1803, $"{Sign} Timeout must be in the " +
                    $"range {_internalConfig.MinTimeout}-{_internalConfig.MaxTimeout} seconds.");
            }
        }

        protected string PrepareSchema(string schema)
        {
            schema = schema
                .Trim()
                .ToLower();

            return schema;
        }

        protected async Task CheckHttpCodesMask(string codes)
        {
            var exceptedCodes = codes
                .Split(_internalConfig.ExpectedCodesSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            var wildcard = _internalConfig.ExpectedCodesWildcard;

            // Only valid HTTP response codes are allowed.
            var correctCodeRegex = new Regex("^[1-5" + wildcard + "][0-9" + wildcard + "]{2}$");
            var isAllExpectedCodesOk = exceptedCodes.All(x => correctCodeRegex.IsMatch(x));

            if (!isAllExpectedCodesOk)
            {
                throw new ConfigParsingException(1801, $"{Sign} There is a syntax error in the list of allowed HTTP response codes.");
            }
        }
    }
}
