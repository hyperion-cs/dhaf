using System.Threading.Tasks;

namespace Dhaf.Core
{
    public interface ISwitcher : IExtension
    {
        Task Init(SwitcherInitOptions options);
        Task Switch(SwitcherSwitchOptions options);
    }
}
