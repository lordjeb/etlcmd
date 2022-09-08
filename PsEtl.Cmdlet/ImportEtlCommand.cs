using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace PsEtl.Cmdlet
{
    [Cmdlet(VerbsData.Import, "Etl")]
    [OutputType(typeof(TraceData))]
    public class ImportEtlCommand : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = false,
            ValueFromPipelineByPropertyName = false)]
        public string Path { get; set; }

        protected override void ProcessRecord()
        {
            var source = new ETWTraceEventSource(Path);
            int eventId = 0;

            source.Dynamic.All += delegate (TraceEvent data)
            {
                var traceData = new TraceData
                {
                    Id = Interlocked.Increment(ref eventId),
                    ActivityId = data.ActivityID,
                    RelatedActivityId = data.RelatedActivityID,
                    EventName = data.EventName,
                    Level = data.Level,
                    ProviderName = data.ProviderName,
                    ProcessId = data.ProcessID,
                    ThreadId = data.ThreadID,
                    TimeStampRelativeMSec = data.TimeStampRelativeMSec
                };

                for (int i = 0; i < data.PayloadNames.Length; i++)
                {
                    traceData.Payload.Add(data.PayloadNames[i], data.PayloadString(i));
                }

                WriteObject(traceData);
            };
            source.Process();

        }
    }
}
