using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Exec
{
    /// <summary>
    /// The exec health checker can run any command, and expects an zero-value exit code on success.
    /// Non-zero exit codes are considered errors.
    /// </summary>
    public class ExecHealthChecker : IHealthChecker
    {
        private ILogger<IHealthChecker> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        public string ExtensionName => "exec";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string Sign => $"[{ExtensionName} hc]";

        public async Task Init(HealthCheckerInitOptions options)
        {
            _logger = options.Logger;
            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;
            _serviceConfig = options.ClusterServiceConfig;

            var execResults = Shell.Exec(_config.Init);

            if (!execResults.Success || execResults.ExitCode != 0)
            {
                _logger.LogCritical($"{Sign} The executable initialization file returned a non-zero return code.");
                throw new ExtensionInitFailedException(Sign);
            }

            _logger.LogDebug($"{Sign} Init output: <{execResults.Output}>");
            _logger.LogDebug($"{Sign} Init total exec time: {execResults.TotalExecuteTime} ms.");
            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            var nc = _serviceConfig.NetworkConfigurations.FirstOrDefault(x => x.Id == options.NcId);

            var args = $"{nc.Id} {nc.IP}";
            var execResults = Shell.Exec(_config.Check);

            if (execResults.Success && execResults.ExitCode == 0)
            {
                return new HealthStatus { Healthy = true };
            }

            return new HealthStatus { Healthy = false };
        }
    }
}
