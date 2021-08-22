using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Dhaf.Extensions.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            var pluginPaths = new string[]
            {
                    @"D:\Repositories\cloudflare-dns-dhaf\src\Dhaf.Extensions.Hello\bin\Release\net5\Dhaf.Extensions.Hello.dll",
            };

            var commands = pluginPaths.SelectMany(pluginPath =>
            {
                Assembly pluginAssembly = LoadPlugin(pluginPath);
                return CreateCommands(pluginAssembly);
            }).ToList();

            foreach (var c in commands)
            {
                c.Execute();
            }
        }

        static Assembly LoadPlugin(string path)
        {
            Console.WriteLine($"Loading commands from: {path}");
            var loadContext = new ExtensionLoadContext(path);

            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
        }

        static IEnumerable<ICommand> CreateCommands(Assembly assembly)
        {
            var count = 0;

            foreach (var type in assembly.GetTypes())
            {
                if (typeof(ICommand).IsAssignableFrom(type))
                {
                    var result = Activator.CreateInstance(type) as ICommand;
                    if (result != null)
                    {
                        count++;
                        yield return result;
                    }
                }
            }

            if (count == 0)
            {
                string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
                throw new ApplicationException(
                    $"Can't find any type which implements ICommand in {assembly} from {assembly.Location}.\n" +
                    $"Available types: {availableTypes}");
            }
        }
    }
}
