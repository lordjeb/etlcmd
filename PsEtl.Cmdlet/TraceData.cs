using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;

namespace PsEtl.Cmdlet
{
    public class TraceData
    {
        public int Id { get; internal set; }
        public double TimeStampRelativeMSec { get; internal set; }
        public Guid ActivityId { get; internal set; }
        public Guid RelatedActivityId { get; internal set; }
        public string ProviderName { get; internal set; }
        public int ProcessId { get; internal set; }
        public int ThreadId { get; internal set; }
        public string EventName { get; internal set; }
        public TraceEventLevel Level { get; internal set; }
        public Dictionary<string, string> Payload { get; internal set; } = new Dictionary<string, string>();
    }
}
