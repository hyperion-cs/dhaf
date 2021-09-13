using Dhaf.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Notifiers.Telegram
{
    public partial class TelegramNotifier : INotifier
    {
        protected async Task PutSubscriber(long id, string type)
        {
            var key = $"{_internalConfig.StorageSubscribersPath}/{id}";
            await _storage.PutAsync(key, type);
        }

        protected async Task DeleteSubscriber(long id)
        {
            var key = $"{_internalConfig.StorageSubscribersPath}/{id}";
            await _storage.DeleteAsync(key);
        }

        protected async Task<long?> GetSubscriberOfDefault(long id)
        {
            var key = $"{_internalConfig.StorageSubscribersPath}/{id}";
            var value = await _storage.GetAsyncOfDefault(key);

            if (value is null)
            {
                return null;
            }

            return id;
        }

        protected async Task<IEnumerable<long>> GetSubscribers()
        {
            var subs = await _storage.GetRangeAsync(_internalConfig.StorageSubscribersPath);

            var subIds = subs.Select(x => long.Parse(x.Key));
            return subIds;
        }
    }
}
