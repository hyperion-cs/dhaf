using Microsoft.Extensions.Configuration;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Dhaf.Core.Tests
{
    public class ClusterConfigParserTest
    {
        [Fact]
        public async Task ParseTest()
        {
            var extensionsScope = GetExtensionsScope();
            var _configuration = GetConfiguration();

            var internalConfig = new DhafInternalConfig();
            _configuration.Bind(internalConfig);

            var configParser = new ClusterConfigParser("Data/test_config_1.dhaf", extensionsScope, internalConfig);
            var parsedConfig = await configParser.Parse();

            Assert.NotNull(parsedConfig.Dhaf);
            Assert.Equal("test-cr", parsedConfig.Dhaf.ClusterName);
            Assert.Equal("node-1", parsedConfig.Dhaf.NodeName);

            Assert.NotNull(parsedConfig.Dhaf.WebApi);
            Assert.Equal("localhost", parsedConfig.Dhaf.WebApi.Host);
            Assert.Equal(8128, parsedConfig.Dhaf.WebApi.Port);

            Assert.NotNull(parsedConfig.Etcd);
            Assert.Equal("http://11.22.33.44:2379", parsedConfig.Etcd.Hosts);

            Assert.NotNull(parsedConfig.Services);
            Assert.Equal(2, parsedConfig.Services.Count);

            var serv1 = parsedConfig.Services[0];
            Assert.NotNull(serv1);
            Assert.Equal("serv1", serv1.Name);
            Assert.Equal("site.com", serv1.Domain);
            Assert.NotNull(serv1.EntryPoints);
            Assert.Equal(3, serv1.EntryPoints.Count);

            var serv1nc1 = serv1.EntryPoints[0];
            Assert.Equal("nc1", serv1nc1.Id);
            Assert.Equal("100.1.1.1", serv1nc1.IP);

            var serv1nc2 = serv1.EntryPoints[1];
            Assert.Equal("nc2", serv1nc2.Id);
            Assert.Equal("100.1.1.2", serv1nc2.IP);

            Assert.NotNull(serv1.Switcher);
            Assert.Equal("a", serv1.Switcher.ExtensionName);

            Assert.NotNull(serv1.HealthChecker);
            Assert.Equal("a", serv1.HealthChecker.ExtensionName);

            var serv2 = parsedConfig.Services[1];
            Assert.NotNull(serv2);
            Assert.Equal("serv2", serv2.Name);
            Assert.Equal("foo.site.com", serv2.Domain);
            Assert.NotNull(serv2.EntryPoints);
            Assert.Equal(2, serv2.EntryPoints.Count);

            Assert.NotNull(parsedConfig.Notifiers);
            Assert.Single(parsedConfig.Notifiers);

            var ntf1 = parsedConfig.Notifiers[0];
            Assert.NotNull(ntf1);
            Assert.Equal("a", ntf1.ExtensionName);
            Assert.Equal("a", ntf1.Name);
        }

        [Fact]
        public async Task ParseTest_Issue_25()
        {
            var extensionsScope = GetExtensionsScope();
            var _configuration = GetConfiguration();

            var internalConfig = new DhafInternalConfig();
            _configuration.Bind(internalConfig);

            var configParser = new ClusterConfigParser("Data/test_config_issue_25.dhaf", extensionsScope, internalConfig);
            var parsedConfig = await configParser.Parse();

            Assert.NotNull(parsedConfig.Dhaf.WebApi);
            Assert.Equal("localhost", parsedConfig.Dhaf.WebApi.Host);
            Assert.Equal(8128, parsedConfig.Dhaf.WebApi.Port);
        }

        private ExtensionsScope GetExtensionsScope()
        {
            var switcher = new Mock<ISwitcher>();
            switcher.Setup(x => x.ExtensionName).Returns("cloudflare");
            switcher.Setup(x => x.ConfigType).Returns(typeof(SwitcherConfigMock));
            switcher.Setup(x => x.InternalConfigType).Returns(typeof(SwitcherInternalConfigMock));

            var healthChecker = new Mock<IHealthChecker>();
            healthChecker.Setup(x => x.ExtensionName).Returns("web");
            healthChecker.Setup(x => x.ConfigType).Returns(typeof(HealthCheckerConfigMock));
            healthChecker.Setup(x => x.InternalConfigType).Returns(typeof(HealthCheckerInternalConfigMock));

            var tgNotifier = new Mock<INotifier>();
            tgNotifier.Setup(x => x.ExtensionName).Returns("tg");
            tgNotifier.Setup(x => x.ConfigType).Returns(typeof(NotifierConfigMock));
            tgNotifier.Setup(x => x.InternalConfigType).Returns(typeof(NotifierInternalConfigMock));

            var extensionsScope = new ExtensionsScope
            {
                Switchers = new List<DhafExtension<ISwitcher>>()
                {
                    new DhafExtension<ISwitcher>
                    {
                        ExtensionPath = "sw/cloudflare", Instance = switcher.Object
                    }
                },
                HealthCheckers = new List<DhafExtension<IHealthChecker>>()
                {
                    new DhafExtension<IHealthChecker>
                    {
                        ExtensionPath = "hc/web", Instance = healthChecker.Object
                    }
                },
                Notifiers = new List<DhafExtension<INotifier>>()
                {
                    new DhafExtension<INotifier>
                    {
                        ExtensionPath = "ntf/tg", Instance = tgNotifier.Object
                    }
                }
            };

            return extensionsScope;
        }

        private IConfigurationRoot GetConfiguration()
        {
            // TODO: For performance reasons, it is better to do the configuration reading once before all the tests.
            // However, flexibility is lost in this way.

            return new ConfigurationBuilder()
                          .AddJsonFile("appsettings.json")
                          .Build();
        }
    }
}
