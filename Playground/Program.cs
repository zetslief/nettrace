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
        Console.WriteLine($"\t{metadata.Header.EventName} {blob.SequenceNumber} {metadata.Payload}");
    }
}

/*
 TaskScheduled 70270 MetadataPayload { MetadataPayload (6 fields):
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 9, FieldName = CreatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskCreationOptions }
   FieldV1 { TypeCode = 9, FieldName = appDomain }
*/
record TaskScheduled(
