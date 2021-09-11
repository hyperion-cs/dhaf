using System;

namespace Dhaf.Core
{
    public class ExtensionInitFailedException : Exception
    {
        public string ExtensionSign { get; }
        public override string Message { get => $"Failed to initialize extension {ExtensionSign}. See log for details."; }

        public ExtensionInitFailedException(string extensionSign)
        {
            ExtensionSign = extensionSign;
        }
    }
}
