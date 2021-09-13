using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dhaf.Core
{
    public interface IExtensionStorageProvider
    {
        Task PutAsync(string key, string value);

        Task DeleteAsync(string key);
        Task DeleteRangeAsync(string keyPrefix);

        Task<string> GetAsyncOfDefault(string key);
        Task<IDictionary<string, string>> GetRangeAsync(string keyPrefix);
    }
}
