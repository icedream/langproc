using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LanguageProcessing
{
    /// <summary>
    /// Represents an analyzed grammatic file with access to specific variables and rules.
    /// </summary>
    public class LanguageFile
    {
        // TODO: We have to only allow specific letters for terminals/non-terminals in the future.
        public LanguageFile(string filename)
        {
            MaximumLength = 5;

            // We will save all of the grammatic rules here before making them read-only
            var rules = new List<GrammaticRule>();

            using (var fs = File.OpenRead(filename))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        var currentLine = sr.ReadLine();
                        if (string.IsNullOrEmpty(currentLine))
                            continue;
                        
                        var line = currentLine
                            .Split('#').First() // filter out comments
                            .Trim();

                        if (!line.Any() /* empty line */)
                            continue;

                        if (line.Contains('='))
                        {
                            // set a variable (an option/a setting)
                            var vs = line.Split('=');
                            var n = vs[0].Trim();
                            var v = vs[1];
                            switch (n)
                            {
                                case "n": // maximum length is n
                                    MaximumLength = ushort.Parse(v);
                                    break;
                                case "L": // terminals
                                    // TODO: Make this more intelligent, this is the dumbest I've ever done to a string
                                    // "{a,b, c,   d   }" => { 'a', 'b', 'c', 'd' }
                                    Terminals = v.Where(char.IsLetter).ToArray();
                                    break;
                                default:
                                    Console.Error.WriteLine("Unknown variable \"{0}\" in grammatic file. Ignoring.", n);
                                    break;
                            }
                            continue;
                        }

                        if (line.Contains("->") || line.Contains("=>"))
                        {
                            // this is a grammatic rule
                            var ruleSplit = line.Split(new[]{"->","=>"}, StringSplitOptions.None).Select(s => s.Trim()).ToArray();
                            
                            if (ruleSplit.Length != 2 || !ruleSplit[0].Any())
                            {
                                Console.Error.WriteLine("Syntax error in rule \"{0}\". Needs to be in format \"Left->Right\". Ignoring.", line);
                                continue;
                            }

                            var left = ruleSplit[0];
                            var right = ruleSplit[1];

                            // Resolve "|" replacements
                            if (right.Contains('|'))
                            {
                                var outputs = right.Split('|').Select(o => o.Trim());
                                foreach (var output in outputs)
                                {
                                    rules.Add(new GrammaticRule(left, output));
                                    Debug.WriteLine("Added rule: {0}", rules.Last());
                                }
                            }
                            else
                            {
                                rules.Add(new GrammaticRule(left, right));
                                Debug.WriteLine("Added rule: {0}", rules.Last());
                            }
                            continue;
                        }

                        Console.Error.WriteLine("Syntax error in line \"{0}\". Neither a rule nor a setting. Ignoring.", line);
                    }
                }
            }

            // Make rules read only and accessible
            Rules = rules.ToArray();

            // Threads list which contains all the threads we're working on right now
            //CurrentThreads = new ConcurrentBag<Task>();
        }

        /// <summary>
        /// The maximum length of the outputs
        /// </summary>
        public ushort MaximumLength { get; private set; }

        /// <summary>
        /// Given terminals
        /// </summary>
        public char[] Terminals { get; private set; }

        /// <summary>
        /// Given grammatic rules
        /// </summary>
        public GrammaticRule[] Rules { get; private set; }

        /// <summary>
        /// Returns how many threads are currently used to process the language.
        /// </summary>
        public int CurrentThreadsCount
        {
            get { return _currentThreadsCount; }
        }

        private int _currentThreadsCount;

        //private ConcurrentBag<Task> CurrentThreads { get; set; }

        /// <summary>
        /// Processes an input string and invokes a callback function on every new word being generated.
        /// </summary>
        /// <param name="outputCallback">The callback method to invoke when new words have been found.
        /// The string parameter will be the most recent generated word.</param>
        /// <param name="input">The input word, "S" by default for start symbol only</param>
        /// <returns>All outputs of the applied grammatic file</returns>
        public void Process(Action<string> outputCallback, string input = "S" /* start symbol */)
        {
            var previousStages = new ConcurrentBag<string>();
            _currentThreadsCount = 0;

            var ptask = new Task(_process, new object[] { input, outputCallback, previousStages }, TaskCreationOptions.AttachedToParent);
            ptask.ContinueWith(pt => Interlocked.Decrement(ref _currentThreadsCount)); // remove task from task list after completion
            Interlocked.Increment(ref _currentThreadsCount);
            ptask.RunSynchronously();

            while (CurrentThreadsCount > 0)
            {
                // TODO: Handle waiting for all threads better
                Thread.Sleep(50);
            }
        }

        public void SingleProcess(Action<string> outputCallback, string input = "S" /* start symbol */)
        {
            var currentStage = new List<string> {input};
            var previousStages = new List<string>();
            _currentThreadsCount = 1;

            while (currentStage.Any())
            {
                var previousStage = currentStage.ToArray();
                previousStages.AddRange(currentStage);
                currentStage.Clear();

                // We have to apply every rule separately on each string
                foreach (var currentOutput in previousStage.SelectMany(
                    currentInput => Rules.Select(
                        rule => rule.Process(currentInput)
                    ).Where(
                        currentOutput => currentOutput != currentInput && currentOutput.Length <= MaximumLength + 3
                    )
                ))
                {
                    if (currentOutput.Length <= MaximumLength && currentOutput.Select(c => Terminals.Contains(c)).All(p => p))
                    {
                        // Found new word
                        Debug.WriteLine("Word: {0}", new object[] { currentOutput });
                        outputCallback.Invoke(currentOutput);
                    }
                    else if (!currentOutput.Select(c => Terminals.Contains(c)).All(p => p) // not a word that is too long
                        && !previousStages.Contains(currentOutput) // wasn't already processed
                    )
                    {
                        // Found new stage
                        Debug.WriteLine("Stage: {0}", new object[] { currentOutput });
                        currentStage.Add(currentOutput); // process in next stage since it contains non-terminals
                    }
                }
            }

            _currentThreadsCount--;
        }

        private void _process(object state)
        {
            if (!(state is object[]))
                throw new ArgumentException("state");

            var arguments = state as object[];

            if (!(arguments[0] is string))
                throw new ArgumentException("state[0]");
            var currentInput = arguments[0] as string;

            if (!(arguments[1] is Action<string>))
                throw new ArgumentException("state[1]");
            var outputCallback = arguments[1] as Action<string>;

            if (!(arguments[2] is ConcurrentBag<string>))
                throw new ArgumentException("state[2]");
            var previousStages = arguments[2] as ConcurrentBag<string>;

            _process(currentInput, outputCallback, previousStages);

        }

        private void _process(string currentInput, Action<string> outputCallback, ConcurrentBag<string> previousStages)
        {
            Debug.WriteLine("Thread started");

            var tasksToStart = new List<Task>();

            // We have to apply every rule separately on each string
            foreach (var currentOutput in Rules.Select(
                    rule => rule.Process(currentInput)
                ).Where(
                    currentOutput => currentOutput != currentInput
                        && currentOutput.Length <= MaximumLength + 3 /* TODO: Handle shortening grammatics better */
                        && !previousStages.Contains(currentOutput)
                )
            )
            {
                previousStages.Add(currentOutput);
                if (currentOutput.Length <= MaximumLength && currentOutput.Select(c => Terminals.Contains(c)).All(p => p))
                {
                    // Found new word
                    //Debug.WriteLine("Word: {0}", new object[] { currentOutput });
                    outputCallback.Invoke(currentOutput);
                }
                else if (!currentOutput.Select(c => Terminals.Contains(c)).All(p => p) /* not a word */)
                {
                    // Found new stage
                    //Debug.WriteLine("Stage: {0}", new object[] { currentOutput });
                    var ptask = new Task(_process, new object[] { currentOutput, outputCallback, previousStages }, TaskCreationOptions.AttachedToParent);
                    ptask.ContinueWith(pt => Interlocked.Decrement(ref _currentThreadsCount)); // remove task from task list after completion
                    Interlocked.Increment(ref _currentThreadsCount);
                    ptask.Start();
                }
            }

            Debug.WriteLine("Thread stopped");
        }

        /// <summary>
        /// Processes an input string and returns a list of all generated words.
        /// </summary>
        /// <param name="input">The input word, "S" by default for start symbol only</param>
        /// <returns>All outputs of the applied grammatic file</returns>
        public string[] Process(string input = "S" /* default: start symbol */)
        {
            var output = new List<string>();
            
            // We're gonna use the Add function as the callback
            // to instantly add new words to the list.
            Process(output.Add, input);

            // Avoid reenumeration
            return output.ToArray();
        }
    }
}
