using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageProcessing.CommandLine
{
    class CommandLineParser
    {
        public CommandLineParser(string[] args)
        {
            var options = new Dictionary<string, string>();
            var values = new List<string>();
            var switches = new List<string>();

            var a = new Queue<string>(args);
            while (a.Any())
            {
                var item = a.Dequeue();

                if (item.StartsWith("--"))
                {
                    if (item.Contains('='))
                    {
                        // long-named option
                        var itemsplit = item.Split('=');
                        var name = itemsplit.First();
                        var value = string.Join("=", itemsplit.Skip(1));
                        if (options.ContainsKey(name))
                        {
                            Console.Error.WriteLine("Warning: Command-line option \"{0}\" already used. Ignored this time.", name);
                            continue;
                        }
                        options.Add(name, value);
                    }
                    else
                    {
                        // long-named switch
                        if (switches.Contains(item))
                        {
                            Console.Error.WriteLine("Warning: Command-line switch \"{0}\" already used. Ignored this time.", item);
                            continue;
                        }
                        switches.Add(item);
                    }
                    continue;
                }

                // TODO: Implement short switches and options
                
                // Just a normal value seemingly
                values.Add(item);
            }

            Options = options.ToArray();
            Values = values.ToArray();
            Switches = switches.ToArray();
        }

        public string[] Switches { get; private set; }

        public KeyValuePair<string, string>[] Options { get; private set; }
        
        public string[] Values { get; private set; }
    }
}
