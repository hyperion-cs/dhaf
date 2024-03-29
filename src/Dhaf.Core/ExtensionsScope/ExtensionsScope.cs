﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Dhaf.Core
{
    public class DhafExtension<T>
    {
        public string ExtensionPath { get; set; }
        public T Instance { get; set; }
    }

    public class ExtensionsScope
    {
        public IEnumerable<DhafExtension<IHealthChecker>> HealthCheckers { get; set; }
        public IEnumerable<DhafExtension<ISwitcher>> Switchers { get; set; }
        public IEnumerable<DhafExtension<INotifier>> Notifiers { get; set; }
    }

    public class ExtensionMeta<T>
    {
        public string EntryPoint { get; set; }

        public T InternalConfiguration { get; set; }
    }

    public class ExtensionsScopeFactory
    {
        public static T CreateSuchAs<T>(T source)
        {
            var type = source.GetType();
            var destination = Activator.CreateInstance(type);

            return (T)destination;
        }

        public static ExtensionsScope GetExtensionsScope(IEnumerable<string> extensionsPath)
        {
            var healthCheckers = new List<DhafExtension<IHealthChecker>>();
            var switchers = new List<DhafExtension<ISwitcher>>();
            var notifiers = new List<DhafExtension<INotifier>>();

            foreach (var path in extensionsPath)
            {
                var extAssembly = LoadExtension(path);
                var impls = GetImplementationsFromAssembly(extAssembly, path);

                healthCheckers.AddRange(impls.HealthCheckers);
                switchers.AddRange(impls.Switchers);
                notifiers.AddRange(impls.Notifiers);
            }

            return new ExtensionsScope
            {
                HealthCheckers = healthCheckers,
                Switchers = switchers,
                Notifiers = notifiers,
            };
        }

        protected static Assembly LoadExtension(string path)
        {
            var extensionDir = $"extensions/{path}/";
            var metaRaw = File.ReadAllText(extensionDir + "extension.json");
            var meta = JsonSerializer.Deserialize<ExtensionMeta<object>>(metaRaw, DhafInternalConfig.JsonSerializerOptions);

            var loadContext = new ExtensionLoadContext(extensionDir + meta.EntryPoint);
            return loadContext.LoadFromAssemblyName(
                new AssemblyName(Path.GetFileNameWithoutExtension(extensionDir + meta.EntryPoint))
                );
        }

        protected static ExtensionsScope GetImplementationsFromAssembly(Assembly assembly, string extensionPath)
        {
            var healthCheckers = new List<DhafExtension<IHealthChecker>>();
            var switchers = new List<DhafExtension<ISwitcher>>();
            var notifiers = new List<DhafExtension<INotifier>>();

            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                var healthChecker = GetImplementationOrDefault<IHealthChecker>(type);
                if (healthChecker != null)
                {
                    if (!typeof(IHealthCheckerConfig).IsAssignableFrom(healthChecker.ConfigType))
                    {
                        throw new Exception($"The health checker config type <{healthChecker.ConfigType}> does not implement the dhaf health checker config interface.");
                    }

                    if (!typeof(IHealthCheckerInternalConfig).IsAssignableFrom(healthChecker.InternalConfigType))
                    {
                        throw new Exception($"The health checker internal config type <{healthChecker.InternalConfigType}> does not implement the dhaf health checker internal interface.");
                    }

                    healthCheckers.Add(new DhafExtension<IHealthChecker>
                    {
                        Instance = healthChecker,
                        ExtensionPath = extensionPath
                    });

                    continue;
                }

                var switcher = GetImplementationOrDefault<ISwitcher>(type);
                if (switcher != null)
                {
                    if (!typeof(ISwitcherConfig).IsAssignableFrom(switcher.ConfigType))
                    {
                        throw new Exception($"The switch config type <{switcher.ConfigType}> does not implement the dhaf switch config interface.");
                    }

                    if (!typeof(ISwitcherInternalConfig).IsAssignableFrom(switcher.InternalConfigType))
                    {
                        throw new Exception($"The switch internal config type <{switcher.InternalConfigType}> does not implement the dhaf switch internal config interface.");
                    }

                    switchers.Add(new DhafExtension<ISwitcher>
                    {
                        Instance = switcher,
                        ExtensionPath = extensionPath
                    });

                    continue;
                }

                var notifier = GetImplementationOrDefault<INotifier>(type);
                if (notifier != null)
                {
                    if (!typeof(INotifierConfig).IsAssignableFrom(notifier.ConfigType))
                    {
                        throw new Exception($"The notifier config type <{notifier.ConfigType}> does not implement the dhaf notifier config interface.");
                    }

                    if (!typeof(INotifierInternalConfig).IsAssignableFrom(notifier.InternalConfigType))
                    {
                        throw new Exception($"The notifier internal config type <{notifier.InternalConfigType}> does not implement the dhaf notifier internal config interface.");
                    }

                    notifiers.Add(new DhafExtension<INotifier>
                    {
                        Instance = notifier,
                        ExtensionPath = extensionPath
                    });
                }
            }

            return new ExtensionsScope
            {
                HealthCheckers = healthCheckers,
                Switchers = switchers,
                Notifiers = notifiers
            };
        }

        protected static T GetImplementationOrDefault<T>(Type type)
            where T : class
        {
            if (typeof(T).IsAssignableFrom(type)
                && Activator.CreateInstance(type) is T impl)
            {
                return impl;
            }

            return null;
        }
    }
}
