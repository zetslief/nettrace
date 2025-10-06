using System.Runtime.InteropServices;
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

Dictionary<long, List<NettraceReader.EventBlob<NettraceReader.Event>>> threadStorage = [];
foreach (var eventBlock in file.EventBlocks)
{
    foreach (var eventBlob in eventBlock.EventBlobs)
    {
        if (threadStorage.TryGetValue(eventBlob.CaptureThreadId, out var threadBlobs))
        {
            threadBlobs.Add(eventBlob);
        }
        else
        {
            threadStorage[eventBlob.CaptureThreadId] = [eventBlob];
        }
    }
}

foreach (var (thread, blobs) in threadStorage)
{
    Console.WriteLine($"Thread: {thread}");
    foreach (var blob in blobs)
    {
        var metadata = metadataStorage[blob.MetadataId].Payload;
        if (metadata.Header.EventName.Length == 0) continue;
        ReadOnlySpan<byte> payloadBytes = blob.Payload.Bytes.Span;
        IEvent @event = metadata.Header.EventName switch
        {
            var name when name == TaskWaitBegin.Name => TplParser.ParseTaskWaitBegin(payloadBytes),
            var name when name == AwaitTaskContinuationScheduled.Name => TplParser.ParseAwaitTaskContinuation(payloadBytes),
            var name when name == TaskScheduled.Name => TplParser.ParseTaskScheduled(payloadBytes),
            var name when name == TraceOperationRelation.Name => TplParser.ParseTraceOperationRelation(payloadBytes),
            var name when name == ProcessInfo.Name => ProcessInfoParser.ParseProcessInfo(payloadBytes),
            var other => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header.EventName} {blob.SequenceNumber} {metadata.Payload}")
        };
        Console.WriteLine(@event);
    }
}

