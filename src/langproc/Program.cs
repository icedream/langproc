using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageProcessing.CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LanguageProcessing
{
    class Program
    {
        // Command line arguments here
        private static IEnumerable<KeyValuePair<string, string>> _cmdOptOutputFile;
        private static IEnumerable<KeyValuePair<string, string>> _cmdOptStartString;

        private static bool _cmdVerbose;
        //private static bool _cmdInstantOutput = true;
        private static bool _cmdDisplayWords;
        private static bool _cmdMultiThreading;
        private static string _cmdOutputFile = string.Empty;
        private static string _cmdInputFile = string.Empty;
        private static string _cmdStartString = "S";

        /// <summary>
        /// Prints the command-line usage of this tool.
        /// </summary>
        static void Usage()
        {
            //TODO
            Console.WriteLine("Usage: {0} [--output-file=...] [--no-display] [--start=...] [--time] [--verbose] <filepath>", Process.GetCurrentProcess().ProcessName);
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("\t{0}{1}\t\t{2}", "--output-file=<filename>", Environment.NewLine, "Output found words to a separate file called <filename>.");
            Console.WriteLine("\t{0}{1}\t\t{2}", "--no-display", Environment.NewLine, "Do not display the words on the console.");
            Console.WriteLine("\t{0}{1}\t\t{2}", "--start=<word>", Environment.NewLine, "Start with a the word <word>. Default is S for start symbol only.");
            Console.WriteLine("\t{0}{1}\t\t{2}", "--time", Environment.NewLine, "Display processing time");
            Console.WriteLine("\t{0}{1}\t\t{2}", "--verbose", Environment.NewLine, "Do verbose output including grammatic rules");
            Console.WriteLine("\t{0}{1}\t\t{2}", "<filepath>", Environment.NewLine, "The path to the file which contains the language/grammatic rules and the settings to use.");
        }

        /// <summary>
        /// Entry point of the tool.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>
        /// Exit code of the tool.
        /// 
        /// -2 = Not enough required arguments
        /// -1 = Input file does not exist
        /// 0 = Success
        /// </returns>
        static int Main(string[] args)
        {
            // Validate command-line arguments
            var opts = new CommandLineParser(args);

            // Did we get any proper command-line arguments?
            if (!opts.Values.Any() /* no arguments given? */)
            {
                Usage();
                return -2;
            }

            // Parse arguments
            _cmdOptOutputFile = opts.Options.Where(o => o.Key == "--output-file").ToArray();
            _cmdOptStartString = opts.Options.Where(o => o.Key == "--start").ToArray();
            _cmdVerbose = opts.Switches.Contains("--verbose");
            _cmdDisplayWords = !opts.Switches.Contains("--no-display");
            //_cmdInstantOutput = opts.Switches.Contains("--instant");
            _cmdMultiThreading = !opts.Switches.Contains("--single");
            _cmdOutputFile = _cmdOptOutputFile.Any() ? _cmdOptOutputFile.First().Value : _cmdOutputFile;
            _cmdStartString = _cmdOptStartString.Any() ? _cmdOptStartString.First().Value : _cmdStartString;
            _cmdInputFile = opts.Values.First();

            // Check if we got the required stuff
            if (string.IsNullOrEmpty(_cmdInputFile))
            {
                Console.Error.WriteLine("ERROR: Input file required but not given. Please check your command-line arguments.");
                Usage();
                return -2;
            }

            // Check if input file exists
            if (!File.Exists(_cmdInputFile))
            {
                Console.Error.WriteLine("ERROR: Input file does not exist. Please check your command-line arguments.");
                return -1;
            }

            /*
             * Language file analysis
             */

            var gf = new LanguageFile(opts.Values.First());

            // List grammatic rules on verbose
            if (_cmdVerbose)
            {
                Console.WriteLine("Grammatic rules:");
                foreach (var rule in gf.Rules)
                {
                    Console.WriteLine("\t{0}", rule);
                }
                Console.WriteLine();
            }

            /*
             * Generation
             */

            // Process grammatic rules
            var stopwatch = new Stopwatch();
            var displaylock = new ManualResetEventSlim(true);
            var displaystatline = string.Empty;

            uint wordsFound = 0;

            var task = Task.Factory.StartNew(() =>
            {
                // File stream if output file is given
                StreamWriter fileWriter = null;
                if (!string.IsNullOrEmpty(_cmdOutputFile))
                {
                    fileWriter = new StreamWriter(_cmdOutputFile, false)
                    {
                        AutoFlush = true
                    };
                }

                stopwatch.Start();

                var process = _cmdMultiThreading ? new Action<Action<string>, string>(gf.Process) : new Action<Action<string>, string>(gf.SingleProcess);

                process(word =>
                {
                    // We got a new calculated word
                    if (_cmdDisplayWords)
                    {
                        if (_cmdVerbose)
                        {
                            displaylock.Wait();
                            displaylock.Reset();

                            Console.WriteLine(word + new string(' ', Math.Max(displaystatline.Length - word.Length, 0)));
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(displaystatline); // display old stats line
                            Console.ResetColor();

                            displaylock.Set();
                        }
                        else
                            Console.WriteLine(word);
                    }

                    if (fileWriter != null)
                        fileWriter.WriteLine(word);

                    wordsFound++;
                }, _cmdStartString);

                stopwatch.Stop();

                if (fileWriter == null)
                    return;
                fileWriter.Close();
                fileWriter.Dispose();
            });

            var statsTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!_cmdVerbose)
                        return;

                    displaylock.Reset();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(displaystatline = "Processing...\r");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    displaylock.Set();

                    while (true)
                    {
                        if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                            throw new OperationCanceledException(); // Simulate task cancellation
                        Thread.Sleep(500);
                        // Wait for any other output to finish
                        displaylock.Wait();
                        displaylock.Reset();
                        // TODO: Handle low-width console line-breaking or avoid it at least
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(displaystatline = string.Format("Processing: Found {0} words in {1} [using {2} threads]\r", wordsFound, stopwatch.Elapsed.ToString("g"), gf.CurrentThreadsCount));
                        Console.ResetColor();
                        displaylock.Set();
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    // Clean verbose stats line before adding the word to stdout
                    Console.WriteLine(new string(' ', displaystatline.Length));
                    Console.CursorTop--;
                    Console.Error.WriteLine("ERROR: Statistics thread crashed.");
                    Console.Error.WriteLine("Message: {0}", e.Message);
                    Debug.WriteLine("Statistics thread crashed.{0}{1}", Environment.NewLine, e);
                }
            });

            statsTask.Wait();
            task.Wait();

            if (_cmdVerbose)
            {
                Console.WriteLine();
                Console.WriteLine("Processing done, took {0}.", stopwatch.Elapsed);
                Console.WriteLine("Found {0} entries.", wordsFound);
            }

            return 0;
        }
    }
}
