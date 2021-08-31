using Dhaf.Core;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        protected string _currentNetworkConfigurationId;

        public string ExtensionName => "cloudflare";
        public string LoggerSign => $"[{ExtensionName} sw]";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(SwitcherInitOptions options)
        {
            _logger = options.Logger;
            _logger.LogInformation($"{LoggerSign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;
            _serviceConfig = options.ClusterServiceConfig;

            _client = new RestClient(_internalConfig.BaseUrl);

            var zone = await GetZoneOrDefault(_config.Zone);

            if (zone == null)
            {
                throw new Exception($"The zone <{_config.Zone}> was not found in Cloudflare.");
            }

            if (zone.Status != "active" || zone.Paused)
            {
                throw new Exception($"The zone <{_config.Zone}> is not active and/or paused in Cloudflare.");
            }

            _zoneId = zone.Id;
            _logger.LogDebug($"{LoggerSign} Zone <{_config.Zone}> id: {_zoneId}");

            var dnsRecords = await GetDnsRecords(_zoneId, _serviceConfig.Domain, "A");

            if (dnsRecords.Result.Count > 1)
            {
                throw new Exception($"The <{_serviceConfig.Domain}> domain name has more than one <A> record.");
            }

            var dnsRecord = dnsRecords.Result.FirstOrDefault();
            if (dnsRecord == null)
            {
                _logger.LogWarning($"{LoggerSign} The <{_serviceConfig.Domain}> domain name has no <A> record.\nAutomatically insert the necessary <A> record...");

                var primaryHost = _serviceConfig.Hosts.FirstOrDefault();
                var addRecordResponse = await CreateDnsRecord(_zoneId, _serviceConfig.Domain, "A", primaryHost.IP);

                if (!addRecordResponse.Success)
                {
                    _logger.LogError($"{LoggerSign} Failed to automatically add an <A> record for the domain <{_serviceConfig.Domain}>.");

                    throw new Exception($"Failed to automatically add an <A> record for the domain <{_serviceConfig.Domain}>.");
                }

                _logger.LogInformation($"{LoggerSign} <A> record for the domain <{_serviceConfig.Domain}> has been successfully added.");

                dnsRecord = addRecordResponse.Result;
                _currentNetworkConfigurationId = primaryHost.Id;
            }
            else
            {
                var currentHost = _serviceConfig.Hosts.FirstOrDefault(x => x.IP == dnsRecord.Content);
                if (currentHost == null)
                {
                    _logger.LogWarning($"{LoggerSign} <A> record for the domain <{_serviceConfig.Domain}> contains an unknown IP address. Automatic replacement with the highest-priority IP...");

                    var primaryHost = _serviceConfig.Hosts.FirstOrDefault();
                    await EditDnsRecord(_zoneId, _dnsRecordAId, "A", _serviceConfig.Domain, primaryHost.IP);
                    _currentNetworkConfigurationId = primaryHost.Id;
                }
                else
                {
                    _currentNetworkConfigurationId = currentHost.Id;
                }
            }

            if (!dnsRecord.Proxied)
            {
                throw new Exception($"<A> record for the domain <{_serviceConfig.Domain}> is not proxied through cloudflare.");
            }

            _dnsRecordAId = dnsRecord.Id;

            _logger.LogDebug($"{LoggerSign} <A> record for the domain <{_serviceConfig.Domain}> id: {_dnsRecordAId}");
            _logger.LogInformation($"{LoggerSign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var host = _serviceConfig.Hosts.FirstOrDefault(x => x.Id == options.HostId);

            _logger.LogInformation($"{LoggerSign} Switch to NC <{host.Id}> requested...");
            _logger.LogDebug($"{LoggerSign} Failover: {options.Failover}");
            _logger.LogDebug($"{LoggerSign} New IP: {host.IP}");

            await EditDnsRecord(_zoneId, _dnsRecordAId, "A", _serviceConfig.Domain, host.IP);
            _currentNetworkConfigurationId = host.Id;

            _logger.LogInformation($"{LoggerSign} Successfully switched to NC <{host.Id}>.");
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
                _logger.LogError($"{LoggerSign} Failed to update an <{type}> record for the domain {domainName}.");
                throw new Exception($"Failed to update an <{type}> record for the domain {domainName}.");
            }

            _logger.LogDebug($"{LoggerSign} <{type}> record for the domain {domainName} has been successfully updated.");
        }

        public async Task<string> GetCurrentNetworkConfigurationId()
        {
            return _currentNetworkConfigurationId;
        }
    }
}
