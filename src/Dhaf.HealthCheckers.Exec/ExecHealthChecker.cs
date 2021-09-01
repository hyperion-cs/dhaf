using Dhaf.Core;
using System;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Exec
{
    /// <summary>
    /// The exec health checker can run any command, and expects an zero-value exit code on success.
    /// Non-zero exit codes are considered errors.
    /// </summary>
    public class ExecHealthChecker : IHealthChecker
    {
        public string ExtensionName => "exec";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string LoggerSign => $"[{ExtensionName} hc]";

        public Task Init(HealthCheckerInitOptions config)
        {
            throw new NotImplementedException();
        }

        public Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
