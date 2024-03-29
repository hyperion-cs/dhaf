﻿using System.Threading.Tasks;

namespace Dhaf.Core
{
    public interface IHealthChecker : IExtension
    {
        Task Init(HealthCheckerInitOptions config);
        Task<HealthStatus> Check(HealthCheckerCheckOptions options);

        Task<string> ResolveUnhealthinessReasonCode(int code);
    }
}
