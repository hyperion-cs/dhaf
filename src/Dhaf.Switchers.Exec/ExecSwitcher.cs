using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Switchers.Exec
{
    public class ExecSwitcher : ISwitcher
    {
        private ILogger<ISwitcher> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        protected string _currentNetworkConfigurationId = string.Empty;

        public string ExtensionName => "exec";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string LoggerSign => $"[{ExtensionName} sw]";

        public async Task<string> GetCurrentNetworkConfigurationId()
        {
            return _currentNetworkConfigurationId;
        }

        public async Task Init(SwitcherInitOptions options)
        {
            _logger = options.Logger;
            _logger.LogInformation($"{LoggerSign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;
            _serviceConfig = options.ClusterServiceConfig;

            var execResults = Shell.Exec(_config.Init);

            if (!execResults.Success || execResults.ExitCode != 0)
            {
                throw new Exception($"{LoggerSign} Init failed.");
            }

            _logger.LogDebug($"{LoggerSign} Init output: <{execResults.Output}>");
            _logger.LogDebug($"{LoggerSign} Init total exec time: {execResults.TotalExecuteTime} ms.");

            // The exec switcher MUST return the ID of the current network configuration
            // if it initializes successfully.
            var currentNcId = execResults.Output.Trim();
            _currentNetworkConfigurationId = currentNcId;

            _logger.LogInformation($"{LoggerSign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var nc = _serviceConfig.NetworkConfigurations.FirstOrDefault(x => x.Id == options.NcId);
            _logger.LogInformation($"{LoggerSign} Switch to NC <{nc.Id}> requested...");

            var args = $"{nc.Id} {nc.IP}";
            var execResults = Shell.Exec(_config.Switch, args);

            if (!execResults.Success || execResults.ExitCode != 0)
            {
                throw new Exception($"{LoggerSign} Switch failed.");
            }

            _logger.LogTrace($"{LoggerSign} Switch output: <{execResults.Output}>");
            _logger.LogTrace($"{LoggerSign} Switch total exec time: {execResults.TotalExecuteTime} ms.");

            _currentNetworkConfigurationId = nc.Id;
            _logger.LogInformation($"{LoggerSign} Successfully switched to NC <{nc.Id}>.");
        }
    }
}
