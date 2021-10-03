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

        protected string _currentEntryPointId = string.Empty;

        public string ExtensionName => "exec";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} sw]";

        public async Task<string> GetCurrentEntryPointId()
        {
            return _currentEntryPointId;
        }

        public async Task Init(SwitcherInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger;

            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            var execResults = Shell.Exec(_config.Init);

            if (!execResults.Success || execResults.ExitCode != 0)
            {
                _logger.LogCritical($"{Sign} The executable initialization file returned a non-zero return code.");
                throw new ExtensionInitFailedException(Sign);
            }

            _logger.LogDebug($"{Sign} Init output: <{execResults.Output}>");
            _logger.LogDebug($"{Sign} Init total exec time: {execResults.TotalExecuteTime} ms.");

            // The exec switcher MUST return the ID of the current entry point
            // if it initializes successfully.
            var currentEpId = execResults.Output.Trim();
            _currentEntryPointId = currentEpId;

            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);
            _logger.LogInformation($"{Sign} Switch to entry point <{entryPoint.Id}> requested...");

            var args = $"{entryPoint.Id} {entryPoint.IP}";
            var execResults = Shell.Exec(_config.Switch, args);

            if (!execResults.Success || execResults.ExitCode != 0)
            {
                _logger.LogCritical($"{Sign} The executable for the switch returned a non-zero return code.");
                throw new SwitchFailedException(Sign);
            }

            _logger.LogTrace($"{Sign} Switch output: <{execResults.Output}>");
            _logger.LogTrace($"{Sign} Switch total exec time: {execResults.TotalExecuteTime} ms.");

            _currentEntryPointId = entryPoint.Id;
            _logger.LogInformation($"{Sign} Successfully switched to entry point <{entryPoint.Id}>.");
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }
    }
}
