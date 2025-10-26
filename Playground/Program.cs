using System.Collections.Immutable;
using System.Text;
using Nettrace;
using Nettrace.PayloadParsers;

using var stream = new MemoryStream(File.ReadAllBytes("./traces/tpleventsource_profileme.nettrace"));
var file = NettraceReader.Read(stream);

Dictionary<int, NettraceReader.EventBlob<NettraceReader.MetadataEvent>> metadataStorage = [];

foreach (var metadataBlock in file.MetadataBlocks)
{
    foreach (var metadataBlob in metadataBlock.EventBlobs)
    {
        metadataStorage[metadataBlob.Payload.Header.MetaDataId] = metadataBlob;
    }
}

var eventBlobs = file.EventBlocks.SelectMany(b => b.EventBlobs).ToImmutableArray();

foreach (var eventBlob in eventBlobs)
{
    var metadata = metadataStorage[eventBlob.MetadataId].Payload;
    Console.Write($"{eventBlob.SequenceNumber} - Thread {eventBlob.ThreadId} - ");
    PrintEventBlob(eventBlob, metadata);
}

return;

static void PrintEventBlob(
    NettraceReader.EventBlob<NettraceReader.Event> blob,
    NettraceReader.MetadataEvent metadata)
{
    ReadOnlySpan<byte> payloadBytes = blob.Payload.Bytes.Span;
    Console.WriteLine(metadata.Header);
    IEvent @event = metadata.Header.ProviderName switch
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
            var id when id == MethodDCEndVerbose.Id => RuntimeRundownEvents.ParseMethodDCEndVerbose(payloadBytes),
            var id when id == DCEndInit.Id => RuntimeRundownEvents.ParseDCEndInit(payloadBytes),
            var id when id == MethodDCEndILToNativeMap.Id => RuntimeRundownEvents.ParseMethodDCEndILToNativeMap(payloadBytes),
            var id when id == DomainModuleDCEnd.Id => RuntimeRundownEvents.ParseDomainModuleDCEnd(payloadBytes),
            var id when id == ModuleDCEnd.Id => RuntimeRundownEvents.ParseModuleDCEnd(payloadBytes),
            var id when id == RuntimeInformationRundown.Id => RuntimeRundownEvents.ParseRuntimeInformationRundown(payloadBytes),
            _ => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header} {metadata.Payload}")
        },
        var unknownProvider => throw new NotImplementedException($"Parser for {unknownProvider} is not implemented."),
    };
    Console.WriteLine(@event);
}
