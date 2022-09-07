using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace etlcmd
{
    public static class StringExtension
    {
        public static string Truncate(this string variable, int Length)
        {
            if (string.IsNullOrEmpty(variable)) return variable;
            return variable.Length <= Length ? variable : variable.Substring(0, Length);
        }
    }

    internal class EtlProcessorBaseHelpers
    {
        private static readonly string FORMAT_STRING = "{0,-8} {1,16:F4} {2,-30} {3,-13} ";//{4,-65}";
        private readonly string formatString;
        private readonly int remainingLength;

        public EtlProcessorBaseHelpers()
        {
            remainingLength = Console.LargestWindowWidth - string.Format(FORMAT_STRING, "", "", "", "").Length - 1;
            formatString = FORMAT_STRING + $"{{4,-{remainingLength}}}";
        }

        public string GetFormatString(out int remainingLength)
        {
            remainingLength = this.remainingLength;
            return formatString;
        }
    }

    internal abstract class EtlProcessorBase
    {
        private readonly FilterOptions options;
        private int eventsFiltered = 0;
        private int eventsProcessed = 0;
        private readonly int startingId = 0;
        private readonly int endingId = -1;
        private readonly int includedProviderCount;
        private readonly int includedEventCount;
        private readonly int includedActivityId;
        private EtlProcessorBaseHelpers helpers = new EtlProcessorBaseHelpers();

        public EtlProcessorBase(FilterOptions options)
        {
            this.options = options;

            if (options.Range.Count() > 0)
            {
                this.startingId = ParseRange(options.Range.First(), 0);
            }

            if (options.Range.Count() > 1)
            {
                this.endingId = ParseRange(options.Range.Last(), -1);
            }

            includedProviderCount = options.MatchProviderName.Count();
            includedEventCount = options.MatchEventName.Count();
            includedActivityId = options.MatchActivityId.Count();
        }

        public int EventsFiltered { get => eventsFiltered; }

        public int EventsProcessed { get => eventsProcessed; }

        public int ParseRange(string s, int defaultValue)
        {
            int result;
            s = s.ToUpper();
            if (s == "START" || s == "BEGIN" || s == "FIRST")
            {
                result = 0;
            }
            else if (s == "END" || s == "LAST")
            {
                result = -1;
            }
            else if (!int.TryParse(s, out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public abstract void Process();

        public void ProcessEvent(TraceEvent data)
        {
            Interlocked.Increment(ref eventsProcessed);
        }

        public void ProcessUnfilteredEvent(TraceEvent data, string payloadData)
        {
            if (options.Verbose)
            {
                int remainingLength;
                Console.WriteLine(helpers.GetFormatString(out remainingLength),
                    eventsProcessed,
                    data.TimeStampRelativeMSec,
                    data.EventName.Truncate(30),
                    data.Level.ToString(),
                    payloadData);
            }
        }

        public bool ShouldFilterEvent(TraceEvent data, out string payloadData)
        {
            List<string> payload = new List<string>();
            for (int i = 0; i < data.PayloadNames.Length; i++)
            {
                payload.Add(string.Format("{0}={1}", data.PayloadNames[i], data.PayloadString(i)));
            }
            payloadData = string.Join(", ", payload);

            bool filtered = false;
            if (data.Level > (TraceEventLevel)options.IncludeLevel ||
                eventsProcessed < startingId ||
                (endingId != -1 && eventsProcessed > endingId) ||
                (includedProviderCount > 0 && !options.MatchProviderName.Contains(data.ProviderName)) ||
                (includedEventCount > 0 && !options.MatchEventName.Contains(data.EventName)) ||
                (includedActivityId > 0 && !options.MatchActivityId.Contains(data.ActivityID.ToString())) ||
                (!string.IsNullOrEmpty(options.MatchPayload) && !payloadData.Contains(options.MatchPayload))
                )
            {
                filtered = true;
            }

            if (!filtered)
            {
                return false;
            }
            else
            {
                Interlocked.Increment(ref eventsFiltered);
                return true;
            }
        }
    }

    // Make IDisposable to clean up source?
    internal class EtlProcessor : EtlProcessorBase
    {
        private readonly ETWTraceEventSource source;

        public EtlProcessor(FilterOptions options)
            : base(options)
        {
            source = new ETWTraceEventSource(options.InputFile);
        }

        public override void Process()
        {
            source.Dynamic.All += delegate (TraceEvent data)
            {
                ProcessEvent(data);
                string payloadData;
                if (!ShouldFilterEvent(data, out payloadData))
                {
                    ProcessUnfilteredEvent(data, payloadData);
                }
            };
            source.Process();
        }
    }

    // Make IDisposable to clean up source?
    internal class EtlProcessorWithOutput : EtlProcessorBase
    {
        private readonly ETWReloggerTraceEventSource source;

        public EtlProcessorWithOutput(FilterOptions options)
            : base(options)
        {
            source = new ETWReloggerTraceEventSource(options.InputFile, options.OutputFile);
        }

        public override void Process()
        {
            source.OutputUsesCompressedFormat = true;
            source.Dynamic.All += delegate (TraceEvent data)
            {
                ProcessEvent(data);
                string payloadData;
                if (!ShouldFilterEvent(data, out payloadData))
                {
                    ProcessUnfilteredEvent(data, payloadData);
                    source.WriteEvent(data);
                }
            };
            source.Process();
        }
    }

    internal class EtlFilter
    {
        private readonly FilterOptions options;
        private EtlProcessorBaseHelpers helpers = new EtlProcessorBaseHelpers();

        public EtlFilter(FilterOptions options)
        {
            this.options = options;
        }

        public void Process()
        {
            EtlProcessorBase processor;
            if (string.IsNullOrEmpty(options.OutputFile))
            {
                processor = new EtlProcessor(options);
            }
            else
            {
                processor = new EtlProcessorWithOutput(options);
            }

            if (options.Verbose)
            {
                int remainingLength;
                var formatString = helpers.GetFormatString(out remainingLength);
                Console.WriteLine(formatString, "ID", "Timestamp-ms", "EventName", "Level", "Payload");
                Console.WriteLine(formatString, "--", "------------", "---------", "-----", "-------");
            }

            Stopwatch timer = new Stopwatch();
            timer.Start();

            processor.Process();

            timer.Stop();

            if (!options.Quiet)
            {
                Console.WriteLine("{3} Events Matched, {0} Events Filtered, {1} Events Processed in {2}", processor.EventsFiltered,
                    processor.EventsProcessed, timer.Elapsed, processor.EventsProcessed - processor.EventsFiltered);
            }
        }
    }
}
