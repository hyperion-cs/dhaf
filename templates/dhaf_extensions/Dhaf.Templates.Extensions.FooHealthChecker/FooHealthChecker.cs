using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Foo
{
    public class FooHealthChecker : IHealthChecker
    {
        private ILogger<IHealthChecker> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        public string ExtensionName => "foo";
        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} hc]";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(HealthCheckerInitOptions options)
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

        public async Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);

            // <- Your health check code is HERE.

            // This is the test data for the return. It must be replaced with real data.
            var reasonCode = 1; // The code for the cause of unhealthiness (for illustration purposes only).
            return new HealthStatus { Healthy = false, ReasonCode = reasonCode };
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role)
        {
            // <- Your code to react to the event of the current
            // <- dhaf cluster node role change (can be empty in most cases).
        }


        public async Task<string> ResolveUnhealthinessReasonCode(int code)
        {
            // The dhaf cluster state store stores the reason of unhealthiness as an integer code.
            // This method serves to resolve such codes into human-readable text.
            // How to do it is up to you.
            // For example, you can store a Dictionary<int,string> that resolves codes into text.
            // And you can also use enums.

            return "Test reason";
        }
    }
}
