using Dhaf.Core;
using System;
using System.Threading.Tasks;

namespace Dhaf.Switchers.Cloudflare
{
    public class CloudflareSwitcher : ISwitcher
    {
        public string ExtensionName => "cloudflare";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public async Task Init(SwitcherInitOptions options)
        {
            Console.WriteLine("cloudflare switcher init...");
        }

        public async Task Switch(SwitcherSwitchOptions options)
        {
        }
    }
}
