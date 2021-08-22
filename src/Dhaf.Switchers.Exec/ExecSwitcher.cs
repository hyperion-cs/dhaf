using Dhaf.Core;
using System;
using System.Threading.Tasks;

namespace Dhaf.Switchers.Exec
{
    public class ExecSwitcher : ISwitcher
    {
        public string ExtensionName => "exec";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public Task Init(SwitcherInitOptions options)
        {
            throw new NotImplementedException();
        }

        public Task Switch(SwitcherSwitchOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
