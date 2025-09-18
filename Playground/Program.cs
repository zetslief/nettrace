using System.Runtime.InteropServices;
using Nettrace;

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
        int cursor = 0;
        IEvent @event = metadata.Header.EventName switch
        {
            var name when name == TaskWaitBegin.Name => new TaskWaitBegin(
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[..4]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[3..8]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[7..12]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[11..16]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[15..20])
            ),
            var name when name == AwaitTaskContinuationScheduled.Name => new AwaitTaskContinuationScheduled(
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[..4]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[3..8]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[7..12])
            ),
            var name when name == TaskScheduled.Name => new TaskScheduled(
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[..4]),
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[3..8]),
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[7..12]),
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[11..16]),
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[15..20]),
                    MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[19..])
            ),
            var name when name == TraceOperationRelation.Name => new TraceOperationRelation(
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[..4]),
                MemoryMarshal.Read<int>(blob.Payload.Bytes.Span[3..8])
            ),
            var name when name == ProcessInfo.Name => new ProcessInfo(
                NettraceReader.ReadUnicode(blob.Payload.Bytes.Span, ref cursor),
                NettraceReader.ReadUnicode(blob.Payload.Bytes.Span, ref cursor),
                NettraceReader.ReadUnicode(blob.Payload.Bytes.Span, ref cursor)
            ),
            var other => throw new NotImplementedException($"Blob parsing is not implemented for \n\t{metadata.Header.EventName} {blob.SequenceNumber} {metadata.Payload}")
        };
        Console.WriteLine(@event);
    }
}

interface IEvent
{
    static abstract string Name { get; }
}

/*
 TaskScheduled 70270 MetadataPayload (6 fields):
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 9, FieldName = CreatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskCreationOptions }
   FieldV1 { TypeCode = 9, FieldName = appDomain }
*/
record TaskScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int CreatingTaskId, int TaskCreationOptions, int AppDomain) : IEvent
{
    public static string Name => nameof(TaskScheduled);
}

/*
 TaskWaitBegin 1 MetadataPayload (5 fields):
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
    FieldV1 { TypeCode = 9, FieldName = TaskID }
    FieldV1 { TypeCode = 9, FieldName = Behavior }
    FieldV1 { TypeCode = 9, FieldName = ContinueWithTaskID }
*/
record TaskWaitBegin(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int Behavior, int ContinueWithTaskId) : IEvent
{
    public static string Name => nameof(TaskWaitBegin);
}

/*
 AwaitTaskContinuationScheduled 2 MetadataPayload (3 fields):
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
    FieldV1 { TypeCode = 9, FieldName = ContinueWithTaskId }
*/
record AwaitTaskContinuationScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int ContinueWithTaskId) : IEvent
{
    public static string Name => nameof(AwaitTaskContinuationScheduled);
}

/*
 TraceOperationRelation 6 (2 fields):
    FieldV1 { TypeCode = 9, FieldName = TaskID }
    FieldV1 { TypeCode = 9, FieldName = Relation }
 */
record TraceOperationRelation(int TaskId, int Relation) : IEvent
{
    public static string Name => nameof(TraceOperationRelation);
}

/*
  ProcessInfo 1 MetadataPayload (3 fields):
    FieldV1 { TypeCode = 18, FieldName = CommandLine }
    FieldV1 { TypeCode = 18, FieldName = OSInformation }
    FieldV1 { TypeCode = 18, FieldName = ArchInformation }
*/
record ProcessInfo(
    string CommandLine,
    string OsInformation,
    string ArchInformation
) : IEvent
{
    public static string Name => nameof(ProcessInfo);
}
