using System;

namespace Dhaf.Core
{
    public interface IExtension
    {
        string ExtensionName { get; }
        string Sign { get; }
        Type ConfigType { get; }
        Type InternalConfigType { get; }
    }
}
