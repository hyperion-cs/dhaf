﻿using Dhaf.Core;
namespace Dhaf.Switchers.Cloudflare
{
    public class InternalConfig : ISwitcherInternalConfig
    {
        public string ExtensionName => "cloudflare";
        public int Jopa { get; set; }
    }
}
