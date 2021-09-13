using System.Threading.Tasks;

namespace Dhaf.Core
{
    public interface INotifier : IExtension
    {
        Task Init(NotifierInitOptions options);
        Task Push(NotifierPushOptions options);
    }
}
