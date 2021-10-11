using Dhaf.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Compute.v1;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Switchers.GoogleCloud
{
    public class GoogleCloudSwitcher : ISwitcher
    {
        private ILogger<ISwitcher> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        protected string _currentEntryPointId = string.Empty;

        protected ComputeService _gcComputeService;
        protected ProjectsResource _gcProjectsResource;

        public string ExtensionName => "google-cloud";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} sw]";

        public async Task Init(SwitcherInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger;

            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            var credential = GoogleCredential.FromFile(_config.CredentialsPath);
            _gcComputeService = new ComputeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });

            _gcProjectsResource = new ProjectsResource(_gcComputeService);

            var gcProject = await _gcProjectsResource
                .Get(_config.Project)
                .ExecuteAsync();

            var gcMetadataItem = gcProject.CommonInstanceMetadata.Items
                .FirstOrDefault(x => x.Key == _config.MetadataKey);

            if (gcMetadataItem is null)
            {
                _logger.LogCritical($"Project <{_config.Project}> does not have an initialization value in the metadata for key <{_config.MetadataKey}>.");
                throw new ExtensionInitFailedException(Sign);
            }

            var currentEntryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.IP == gcMetadataItem.Value);
            if (currentEntryPoint is null)
            {
                _logger.LogCritical($"In project <{_config.Project}>, the initialization value in the metadata for key <{_config.MetadataKey}> " +
                    $"is incorrect because it does not match any of the entry points in the dhaf configuration." +
                    $"On the other hand, it may be a mistake in the dhaf configuration.");

                throw new ExtensionInitFailedException(Sign);
            }

            _currentEntryPointId = currentEntryPoint.Id;
            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);
            _logger.LogInformation($"{Sign} Switch to entry point <{entryPoint.Id}> requested...");

            var gcProject = await _gcProjectsResource
                .Get(_config.Project)
                .ExecuteAsync();

            var gcMetadata = gcProject.CommonInstanceMetadata;
            var currValue = gcMetadata.Items.FirstOrDefault(x => x.Key == _config.MetadataKey);

            if (currValue is null)
            {
                gcMetadata.Items.Add(new Google.Apis.Compute.v1.Data.Metadata.ItemsData
                {
                    Key = _config.MetadataKey,
                    Value = entryPoint.IP
                });
            }
            else
            {
                currValue.Value = entryPoint.IP;
            }

            await _gcProjectsResource
                .SetCommonInstanceMetadata(gcMetadata, _config.Project)
                .ExecuteAsync();

            _currentEntryPointId = entryPoint.Id;
            _logger.LogInformation($"{Sign} Successfully switched to entry point <{entryPoint.Id}>.");
        }

        public async Task<string> GetCurrentEntryPointId()
        {
            return _currentEntryPointId;
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }
    }
}
