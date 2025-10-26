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
                _ => new UnknownEvent(metadata),
            },
            EventPipeProvider.Name => metadata.Header.EventId switch
            {
                var id when id == ProcessInfo.Id => ProcessInfoParser.ParseProcessInfo(payloadBytes),
                _ => new UnknownEvent(metadata),
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
                _ => new UnknownEvent(metadata),
            },
            _ => new UnknownEvent(metadata),
        };
    }
}

public record UnknownEvent(MetadataEvent Metadata) : IEvent
{
    public static int Id => -1;
    public static string Name => nameof(UnknownEvent);
}
