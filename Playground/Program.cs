using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Nettrace;
using Nettrace.HighLevel;
using Nettrace.PayloadParsers;
using static Nettrace.NettraceReader;

using var stream = new MemoryStream(File.ReadAllBytes("/home/cube/progs/nettrace/traces/tpleventsource_profileme.nettrace"));
var file = NettraceReader.Read(stream);

Dictionary<int, EventBlob<MetadataEvent>> metadataStorage = [];

foreach (var metadataBlock in file.MetadataBlocks)
{
    foreach (var metadataBlob in metadataBlock.EventBlobs)
    {
        metadataStorage[metadataBlob.Payload.Header.MetaDataId] = metadataBlob;
    }
}

var events = file.EventBlocks
    .SelectMany(b => b.EventBlobs)
    .Select(b => (b, (object)NettraceEventParser.ProcessEvent(metadataStorage[b.MetadataId].Payload, b)))
    .ToImmutableArray();

Dictionary<int, (EventBlob<Event>, TraceOperationBegin)> traceBegins = [];
Dictionary<int, (EventBlob<Event>, TraceOperationEnd)> traceEnds = [];

foreach (var (blob, @event) in events)
{
    if (@event is TraceOperationBegin traceBegin)
    {
        traceBegins[traceBegin.TaskId] = (blob, traceBegin);
    }
    if (@event is TraceOperationEnd traceEnd)
    {
        traceEnds[traceEnd.TaskId] = (blob, traceEnd);
    }
}

foreach (var (taskId, beginTuple) in traceBegins)
{
    var (beginBlob, begin) = beginTuple;
    if (traceEnds.TryGetValue(taskId, out var endTuple))
    {
        var (endBlob, enf) = endTuple;
        var duration = endBlob.TimeStamp - beginBlob.TimeStamp;
        Console.WriteLine($"TaskId {taskId} {begin.OperationName} Duration: {duration}");
    }
    else
    {
        Console.WriteLine($"WARNING: failed to find end for {taskId}.");
    }
}
