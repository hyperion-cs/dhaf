using Dhaf.Extensions.Base;
using System;
using Newtonsoft.Json;

namespace Dhaf.Extensions.Hello
{
    public class HelloCommand : ICommand
    {
        public string Name { get => "hello"; }
        public string Description { get => "Displays hello message."; }

        public int Execute()
        {
            Console.WriteLine("Hello !!!");
            Console.WriteLine(JsonConvert.SerializeObject(new { a = 20 }));
            return 0;
        }
    }
}
