using Nettrace;
using Nettrace.PayloadParsers;
using System.Collections.Immutable;

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
    if (metadata.Header.EventName.Length == 0) continue;
    Console.Write($"{eventBlob.SequenceNumber} - Thread {eventBlob.ThreadId} - ");
    PrintEventBlob(eventBlob, metadata);
}

return;

static void PrintEventBlob(
    NettraceReader.EventBlob<NettraceReader.Event> blob,
    NettraceReader.MetadataEvent metadata)
{
    if (metadata.Header.EventName.Length == 0)
    {
        Console.WriteLine($"No event name in {metadata}");
        return;
    }

    ReadOnlySpan<byte> payloadBytes = blob.Payload.Bytes.Span;
    IEvent @event = metadata.Header.EventName switch
    {
        var name when name == NewId.Name => TplParser.ParseNewId(payloadBytes),
        var name when name == TraceSynchronousWorkBegin.Name => TplParser.ParseTraceSynchronousWorkBegin(payloadBytes),
        var name when name == TraceSynchronousWorkEnd.Name => TplParser.ParseTraceSynchronousWorkEnd(payloadBytes),
        var name when name == TaskWaitContinuationStarted.Name => TplParser.ParseTaskWaitContinuationStarted(payloadBytes),
        var name when name == TraceOperationEnd.Name => TplParser.ParseTraceOperationEnd(payloadBytes),
        var name when name == TraceOperationBegin.Name => TplParser.ParseTraceOperationBegin(payloadBytes),
        var name when name == TaskWaitContinuationComplete.Name => TplParser.ParseTaskWaitContinuationComplete(payloadBytes),
        var name when name == TaskWaitEnd.Name => TplParser.ParseTaskWaitEnd(payloadBytes),
        var name when name == TaskWaitBegin.Name => TplParser.ParseTaskWaitBegin(payloadBytes),
        var name when name == AwaitTaskContinuationScheduled.Name => TplParser.ParseAwaitTaskContinuation(payloadBytes),
        var name when name == TaskScheduled.Name => TplParser.ParseTaskScheduled(payloadBytes),
        var name when name == TraceOperationRelation.Name => TplParser.ParseTraceOperationRelation(payloadBytes),
        var name when name == ProcessInfo.Name => ProcessInfoParser.ParseProcessInfo(payloadBytes),
        var other => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header.EventName} {blob.SequenceNumber} {metadata.Payload}")
    };
    Console.WriteLine(@event);
}
