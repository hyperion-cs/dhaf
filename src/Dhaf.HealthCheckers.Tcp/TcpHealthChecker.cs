using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Dhaf.HealthCheckers.Tcp
{
    public class TcpHealthChecker : IHealthChecker
    {
        private ILogger<IHealthChecker> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private ClusterServiceConfig _serviceConfig;

        public string ExtensionName => "tcp";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string Sign => $"[{_serviceConfig.Name}/{ExtensionName} hc]";

        public async Task Init(HealthCheckerInitOptions options)
        {
            _serviceConfig = options.ClusterServiceConfig;
            _logger = options.Logger;

            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            await ConfigCheck();
            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task<HealthStatus> Check(HealthCheckerCheckOptions options)
        {
            var entryPoint = _serviceConfig.EntryPoints.FirstOrDefault(x => x.Id == options.EntryPointId);

            try
            {
                const int MS_IN_SEC = 1000;

                using var tcpClient = new TcpClient();
                tcpClient.ReceiveTimeout = (_config.ReceiveTimeout ?? _internalConfig.DefReceiveTimeout) * MS_IN_SEC;

                await tcpClient.ConnectAsync(entryPoint.IP, _config.Port.Value);

                return new HealthStatus { Healthy = true };
            }
            catch (SocketException ex)
            {
                return new HealthStatus
                {
                    Healthy = false,
                    ReasonCode = (int)ex.SocketErrorCode
                };
            }
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }

        public async Task<string> ResolveUnhealthinessReasonCode(int code)
        {
            if (Enum.IsDefined(typeof(SocketError), code))
            {
                var socketError = (SocketError)code;
                return socketError.ToString();
            }

            return "Unexpected reason";
        }

        protected async Task ConfigCheck()
        {
            if (_config.Port is null)
            {
                throw new ConfigParsingException(1901, $"{Sign} The port is not specified.");
            }

            if (_config.ReceiveTimeout is not null
                && (_config.ReceiveTimeout < _internalConfig.MinReceiveTimeout
                    || _config.ReceiveTimeout > _internalConfig.MaxReceiveTimeout))
            {
                throw new ConfigParsingException(1902, $"{Sign} Receive timeout must be in the " +
                    $"range {_internalConfig.MinReceiveTimeout}-{_internalConfig.MaxReceiveTimeout} seconds.");
            }
        }
    }
}
