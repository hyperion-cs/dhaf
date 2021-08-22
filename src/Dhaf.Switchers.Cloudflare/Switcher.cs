using RestSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Switchers.Cloudflare
{
    public class Switcher
    {
        private readonly IRestClient _client;

        protected const string BaseUrl = "https://api.cloudflare.com/client/v4/";
        protected string ApiToken { get; set; }

        public Switcher(string apiToken)
        {
            ApiToken = apiToken;
            _client = new RestClient(BaseUrl);
        }

        public async Task<ResultDto<DnsRecordDto>> CreateDnsRecord(string zoneId, string domainName,
            string type, string content, bool isProxied = true)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records");
            request.AddHeader("Authorization", $"Bearer {ApiToken}");
            request.AddJsonBody(new { type, name = domainName, content, proxied = isProxied });

            var response = await _client.PostAsync<ResultDto<DnsRecordDto>>(request);
            return response;
        }

        public async Task<ResultCollectionDto<DnsRecordDto>>
            GetDnsRecords(string zoneId, string domainName, string type)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records?name={domainName}&type={type}");
            request.AddHeader("Authorization", $"Bearer {ApiToken}");

            var dnsRecords = await _client.GetAsync<ResultCollectionDto<DnsRecordDto>>(request);
            return dnsRecords;
        }

        public async Task EditDnsRecord(string zoneId, string recordId,
            string type, string domainName, string content)
        {
            var request = new RestRequest($"zones/{zoneId}/dns_records/{recordId}");
            request.AddHeader("Authorization", $"Bearer {ApiToken}");
            request.AddJsonBody(new { type, name = domainName, content });

            var response = await _client.PatchAsync<ResultDto<DnsRecordDto>>(request);

            if (!response.Success)
            {
                Console.WriteLine(response.PrettyErrors($"Update the <{type}> record for the domain {domainName}"));
                throw new Exception($"Failed to update an <{type}> record for the domain {domainName}.");
            }

            Console.WriteLine($"<{type}> record for the domain {domainName} has been successfully updated.");
        }

        public async Task<ZoneDto> GetZoneOrDefault(string zoneName)
        {
            var request = new RestRequest($"zones?name={zoneName}");
            request.AddHeader("Authorization", $"Bearer {ApiToken}");

            var response = await _client.GetAsync<ResultCollectionDto<ZoneDto>>(request);

            var zone = response.Result.FirstOrDefault();
            return zone;
        }

        public async Task<string> GetAndCheckIdOfDnsRecordA(string zoneId, string domainName)
        {
            var masterIp = "8.8.8.8";
            var dnsRecords = await GetDnsRecords(zoneId, domainName, "A");

            if (dnsRecords.Result.Count > 1)
            {
                throw new Exception($"The {domainName} domain name has more than one <A> record.");
            }

            var dnsRecord = dnsRecords.Result.FirstOrDefault();

            if (dnsRecord == null)
            {
                Console.WriteLine($"The {domainName} domain name has no <A> record.\nAutomatically insert the necessary <A> record...");

                var addRecordResponse = await CreateDnsRecord(zoneId, domainName, "A", masterIp);

                if (!addRecordResponse.Success)
                {
                    Console.WriteLine(dnsRecords.PrettyErrors($"Adding the <A> record for the domain {domainName}"));
                    throw new Exception($"Failed to automatically add an <A> record for the domain {domainName}.");
                }

                Console.WriteLine($"<A> record for the domain {domainName} has been successfully added.");

                dnsRecord = addRecordResponse.Result;
            }

            return dnsRecord.Id;
        }

        public async Task<string> GetAndCheckZoneId(string zoneName)
        {
            var zone = await GetZoneOrDefault(zoneName);

            if (zone == null)
            {
                throw new Exception($"The zone named {zoneName} was not found in Cloudflare.");
            }

            if (zone.Status != "active" || zone.Paused)
            {
                throw new Exception($"The zone named {zoneName} is not active and/or paused in Cloudflare.");
            }

            return zone.Id;
        }
    }
}
