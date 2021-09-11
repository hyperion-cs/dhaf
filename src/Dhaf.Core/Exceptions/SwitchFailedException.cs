using System;

namespace Dhaf.Core
{
    public class SwitchFailedException : Exception
    {
        public string ExtensionSign { get; }
        public override string Message { get => $"Failed to switch with {ExtensionSign}. See log for details."; }

        public SwitchFailedException(string extensionSign)
        {
            ExtensionSign = extensionSign;
        }
    }
}
