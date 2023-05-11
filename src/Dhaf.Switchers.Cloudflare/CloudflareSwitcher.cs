using System;
using System.Linq;
using System.Threading.Tasks;
using Dhaf.Core;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace Dhaf.Switchers.Cloudflare
{
    public class CloudflareSwitcher : ISwitcher
    {
        private IRestClient _client;
        private ILogger<ISwitcher> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        protected string _zoneId;
        protected string _dnsRecordAId;
        protected string _currentEntryPointId;

        public string ExtensionName => "cloudflare";
        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} sw]";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(SwitcherInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger;

            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            var restOptions = new RestClientOptions
            {
                BaseUrl = new Uri(_internalConfig.BaseUrl),

            };

            _client = new RestClient(restOptions, configureSerialization: s => s.UseNewtonsoftJson());

            await AssertToken();

            var zone = await GetZoneOrDefault(_config.Zone);

            if (zone is null)
            {
                _logger.LogCritical($"{Sign} The zone <{_config.Zone}> is not found in Cloudflare.");
                throw new ExtensionInitFailedException(Sign);
            }

            if (zone.Status != "active" || zone.Paused)
            {
                _logger.LogCritical($"The zone <{_config.Zone}> is not active and/or paused in Cloudflare.");
                throw new ExtensionInitFailedException(Sign);
            }

            _zoneId = zone.Id;
            _logger.LogDebug($"{Sign} Zone <{_config.Zone}> id: {_zoneId}");

            var dnsRecords = await GetDnsRecords(_zoneId, _serviceConfig.Domain, "A");
            if (dnsRecords.Result.Count > 1)
            {
                _logger.LogCritical($"The <{_serviceConfig.Domain}> domain name has more than one <A> record.");
                throw new ExtensionInitFailedException(Sign);
            }

            var dnsRecord = dnsRecords.Result.FirstOrDefault();
            if (dnsRecord == null)
            {
                _logger.LogWarning($"{Sign} The <{_serviceConfig.Domain}> domain name has no <A> record.\nAutomatically insert the necessary <A> record...");

                var primaryEntryPoint = _serviceConfig.EntryPoints.FirstOrDefault();
                var addRecordResponse = await CreateDnsRecord(_zoneId, _serviceConfig.Domain, "A", primaryEntryPoint.IP);

                if (!addRecordResponse.Success)
                {
                    _logger.LogCritical($"{Sign} Failed to automatically add an <A> record for the domain <{_serviceConfig.Domain}>.");

                    throw new ExtensionInitFailedException(Sign);
                }

                _logger.LogInformation($"{Sign} <A> record for the domain <{_serviceConfig.Domain}> has been successfully added.");

                dnsRecord = addRecordResponse.Result;
                _currentEntryPointId = primaryEntryPoint.Id;
            }
            else
            {
                var currentEntryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.IP == dnsRecord.Content);
                if (currentEntryPoint == null)
                {
                    _logger.LogWarning($"{Sign} <A> record for the domain <{_serviceConfig.Domain}> contains an unknown IP address. Automatic replacement with the highest-priority IP...");

                    var primaryEntryPoint = _serviceConfig.EntryPoints.FirstOrDefault();
                    await EditDnsRecord(_zoneId, _dnsRecordAId, "A", _serviceConfig.Domain, primaryEntryPoint.IP);
                    _currentEntryPointId = primaryEntryPoint.Id;
                }
                else
                {
                    _currentEntryPointId = currentEntryPoint.Id;
                }
            }

            if (!dnsRecord.Proxied)
            {
                _logger.LogCritical($"<A> record for the domain <{_serviceConfig.Domain}> is not proxied through cloudflare.");

                throw new ExtensionInitFailedException(Sign);
            }

            _dnsRecordAId = dnsRecord.Id;

            _logger.LogDebug($"{Sign} <A> record for the domain <{_serviceConfig.Domain}> id: {_dnsRecordAId}");
            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);

            _logger.LogInformation($"{Sign} Switch to entry point <{entryPoint.Id}> requested...");
            _logger.LogDebug($"{Sign} Failover: {options.Failover}");
            _logger.LogDebug($"{Sign} New IP: {entryPoint.IP}");

            await EditDnsRecord(_zoneId, _dnsRecordAId, "A", _serviceConfig.Domain, entryPoint.IP);
            _currentEntryPointId = entryPoint.Id;

            _logger.LogInformation($"{Sign} Successfully switched to entry point <{entryPoint.Id}>.");
        }

        public async Task<string> GetCurrentEntryPointId()
        {
            return _currentEntryPointId;
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }

        protected async Task AssertToken()
        {
            var request = new RestRequest($"zones");
            request.AddHeader("Authorization", $"Bearer {_config.ApiToken}");
            try
            {
                var response = await _client.GetAsync<ResultDto<object>>(request);
                if (response.Errors.Any())
                {
                    var error = response.Errors.First();
                    _logger.LogCritical($"{Sign} Error {error.Code}: {error.Message}.");

                    throw new ExtensionInitFailedException(Sign);
                }
            }
            catch (System.Net.WebException e)
            {
                _logger.LogCritical($"{Sign} WebException: {e.Message}");
            }
        }

        protected async Task<ZoneDto> GetZoneOrDefault(string zoneName)
        {
            var request = new RestRequest($"zones?name={zoneName}");
            request.AddHeader("Authorization", $"Bearer {_config.ApiToken}");

            var response = await _client.GetAsync<ResultCollectionDto<ZoneDto>>(request);

            var zone = response.Result.FirstOrDefault();
            return zone;
        }

        protected async Task<ResultCollectionDto<DnsRecordDto>>
            GetDnsRecords(string zoneId, string domainName, string type)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records?name={domainName}&type={type}");
            request.AddHeader("Authorization", $"Bearer {_config.ApiToken}");

            var dnsRecords = await _client.GetAsync<ResultCollectionDto<DnsRecordDto>>(request);
            return dnsRecords;
        }

        protected async Task<ResultDto<DnsRecordDto>> CreateDnsRecord(string zoneId, string domainName,
            string type, string content, bool isProxied = true)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records");
            request.AddHeader("Authorization", $"Bearer {_config.ApiToken}");
            request.AddJsonBody(new { type, name = domainName, content, proxied = isProxied });

            var response = await _client.PostAsync<ResultDto<DnsRecordDto>>(request);
            return response;
        }

        protected async Task EditDnsRecord(string zoneId, string recordId,
            string type, string domainName, string content)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records/{recordId}");
            request.AddHeader("Authorization", $"Bearer {_config.ApiToken}");
            request.AddJsonBody(new { type, name = domainName, content });

            var response = await _client.PatchAsync<ResultDto<DnsRecordDto>>(request);

            if (!response.Success)
            {
                _logger.LogError($"{Sign} Failed to update an <{type}> record for the domain {domainName}.");
                throw new SwitchFailedException(Sign);
            }

            _logger.LogDebug($"{Sign} <{type}> record for the domain {domainName} has been successfully updated.");
        }
    }
}
