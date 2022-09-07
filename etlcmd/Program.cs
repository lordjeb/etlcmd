using CommandLine;
using System;
using System.Collections.Generic;

namespace etlcmd
{
    [Verb("filter", HelpText = "Filter etl file")]
    public class FilterOptions
    {
        [Option('i', "input", Required = true, HelpText = "ETL file from which to read trace data.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = false, HelpText = "ETL file to which to write (modified) trace data.")]
        public string OutputFile { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Output records to console.")]
        public bool Verbose { get; set; }

        [Option('q', "quiet", Required = false, HelpText = "Quiet output.")]
        public bool Quiet { get; set; }

        [Option('p', "include-provider", Required = false, HelpText = "Include trace data from providers.")]
        public IEnumerable<string> MatchProviderName { get; set; }

        [Option('e', "include-event", Required = false, HelpText = "Include trace data matching event names.")]
        public IEnumerable<string> MatchEventName { get; set; }

        [Option('l', "level", Required = false, HelpText = "Include trace data of level or more important.", Default = 5)]
        public int IncludeLevel { get; set; }

        [Option('r', "range", Required = false, HelpText = "Include trace data between the provided ids.", Separator = ':')]
        public IEnumerable<string> Range { get; set; }

        [Option('m', "match-payload", Required = false, HelpText = "Include trace data whose payload contains match string.")]
        public string MatchPayload { get; set; }

        [Option('a', "activityid", Required = false, HelpText = "Include trace data matching activity id.")]
        public IEnumerable<string> MatchActivityId { get; set; }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Type[] types = { typeof(FilterOptions) };

            Parser.Default.ParseArguments(args, types)
                .WithParsed(Run);
        }

        private static void Run(object obj)
        {
            switch (obj)
            {
                case FilterOptions options:
                    EtlFilter processor = new EtlFilter(options);
                    processor.Process();
                    break;
            }
        }
    }
}
