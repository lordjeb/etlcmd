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

    internal static class EtlProcessorBaseHelpers
    {
        public static readonly string FORMAT_STRING = "{0,-8} {1,16:F4} {2,-30} {3,-8} {4,-65}";
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

            includedProviderCount = options.IncludeProviderName.Count();
            includedEventCount = options.IncludeEventName.Count();
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
                Console.WriteLine(EtlProcessorBaseHelpers.FORMAT_STRING,
                    eventsProcessed,
                    data.TimeStampRelativeMSec,
                    data.EventName.Truncate(30),
                    data.Level.ToString(),
                    payloadData.Truncate(65));
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

            // Move some of these tests outside into the ctor?
            bool included = false;
            if (
                (includedProviderCount == 0 && includedEventCount == 0) ||
                (includedProviderCount > 0 && options.IncludeProviderName.Contains(data.ProviderName)) ||
                (includedEventCount > 0 && options.IncludeEventName.Contains(data.EventName))
                )
            {
                included = true;
            }

            bool filtered = false;
            if (data.Level > (TraceEventLevel)options.IncludeLevel ||
                eventsProcessed < startingId ||
                (endingId != -1 && eventsProcessed > endingId) ||
                (!string.IsNullOrEmpty(options.MatchPayload) && !payloadData.Contains(options.MatchPayload))
                )
            {
                filtered = true;
            }

            if (included && !filtered)
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
                Console.WriteLine(EtlProcessorBaseHelpers.FORMAT_STRING, "ID", "RelativeTime", "EventName", "Level", "Payload");
                Console.WriteLine(EtlProcessorBaseHelpers.FORMAT_STRING, "--", "------------", "---------", "-----", "-------");
            }

            Stopwatch timer = new Stopwatch();
            timer.Start();

            processor.Process();

            timer.Stop();

            if (!options.Quiet)
            {
                Console.WriteLine("{0} Events Filtered, {1} Events Processed in {2}", processor.EventsFiltered, processor.EventsProcessed, timer.Elapsed);
            }
        }
    }
}
