using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Dhaf.Core
{
    class ExtensionLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ExtensionLoadContext(string extensionPath)
        {
            _resolver = new AssemblyDependencyResolver(extensionPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
