using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Switchers.Foo
{
    public class FooSwitcher : ISwitcher
    {
        private ILogger<ISwitcher> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        protected string _currentEntryPointId = string.Empty;

        public string ExtensionName => "foo";
        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} sw]";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(SwitcherInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger; // The dhaf extension system provides logging out of the box.

            _logger.LogTrace($"{Sign} Init process...");

            // The dhaf extension system will automatically populate instances of the configuration classes.
            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            // <- Other code to initialize your extension HERE.
            // <- Can be empty if you have nothing else to initialize.

            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);
            _logger.LogInformation($"{Sign} Switch to entry point <{entryPoint.Id}> requested...");

            // <- Your code for the switch is HERE.

            _currentEntryPointId = entryPoint.Id;
            _logger.LogInformation($"{Sign} Successfully switched to entry point <{entryPoint.Id}>.");
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role)
        {
            // <- Your code to react to the event of the current
            // <- dhaf cluster node role change (can be empty in most cases).
        }

        public async Task<string> GetCurrentEntryPointId()
        {
            // In some cases can be replaced by more complex logic.
            return _currentEntryPointId;
        }
    }
}
