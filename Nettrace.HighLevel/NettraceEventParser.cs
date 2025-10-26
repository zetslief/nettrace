using Nettrace.PayloadParsers;
using static Nettrace.NettraceReader;

namespace Nettrace.HighLevel;

public static class NettraceEventParser
{
    public static IEvent ProcessEvent(MetadataEvent metadata, EventBlob<Event> @event)
    {
        ReadOnlySpan<byte> payloadBytes = @event.Payload.Bytes.Span;
        return metadata.Header.ProviderName switch
        {
            TplProvider.Name => metadata.Header.EventId switch
            {
                var id when id == NewId.Id => TplParser.ParseNewId(payloadBytes),
                var id when id == TraceSynchronousWorkBegin.Id => TplParser.ParseTraceSynchronousWorkBegin(payloadBytes),
                var id when id == AwaitTaskContinuationScheduled.Id => TplParser.ParseAwaitTaskContinuationScheduled(payloadBytes),
                var id when id == TraceSynchronousWorkEnd.Id => TplParser.ParseTraceSynchronousWorkEnd(payloadBytes),
                var id when id == TaskWaitContinuationStarted.Id => TplParser.ParseTaskWaitContinuationStarted(payloadBytes),
                var id when id == TraceOperationEnd.Id => TplParser.ParseTraceOperationEnd(payloadBytes),
                var id when id == TraceOperationBegin.Id => TplParser.ParseTraceOperationBegin(payloadBytes),
                var id when id == TaskWaitContinuationComplete.Id => TplParser.ParseTaskWaitContinuationComplete(payloadBytes),
                var id when id == TaskWaitEnd.Id => TplParser.ParseTaskWaitEnd(payloadBytes),
                var id when id == TaskWaitBegin.Id => TplParser.ParseTaskWaitBegin(payloadBytes),
                var id when id == TaskScheduled.Id => TplParser.ParseTaskScheduled(payloadBytes),
                var id when id == TraceOperationRelation.Id => TplParser.ParseTraceOperationRelation(payloadBytes),
                _ => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header} {metadata.Payload}")
            },
            EventPipeProvider.Name => metadata.Header.EventId switch
            {
                var id when id == ProcessInfo.Id => ProcessInfoParser.ParseProcessInfo(payloadBytes),
                _ => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header} {metadata.Payload}")
            },
            RuntimeRundownProvider.Name => metadata.Header.EventId switch
            {
                var id when id == GCSettingsRundown.Id => RuntimeRundownEvents.ParseGCSettingsRundown(payloadBytes),
                var id when id == MethodDCEndVerbose.Id => RuntimeRundownEvents.ParseMethodDCEndVerbose(payloadBytes),
                var id when id == DCEndComplete.Id => RuntimeRundownEvents.ParseDCEndComplete(payloadBytes),
                var id when id == DCEndInit.Id => RuntimeRundownEvents.ParseDCEndInit(payloadBytes),
                var id when id == MethodDCEndILToNativeMap.Id => RuntimeRundownEvents.ParseMethodDCEndILToNativeMap(payloadBytes),
                var id when id == DomainModuleDCEnd.Id => RuntimeRundownEvents.ParseDomainModuleDCEnd(payloadBytes),
                var id when id == ModuleDCEnd.Id => RuntimeRundownEvents.ParseModuleDCEnd(payloadBytes),
                var id when id == AssemblyDCEnd.Id => RuntimeRundownEvents.ParseAssemblyDCEnd(payloadBytes),
                var id when id == AppDomainDCEnd.Id => RuntimeRundownEvents.ParseAppDomainDCEnd(payloadBytes),
                var id when id == RuntimeInformationRundown.Id => RuntimeRundownEvents.ParseRuntimeInformationRundown(payloadBytes),
                _ => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header} {metadata.Payload}")
            },
            var unknownProvider => throw new NotImplementedException($"Parser for {unknownProvider} is not implemented."),
        };
    }
}
