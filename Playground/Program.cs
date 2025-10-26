using System.Collections.Immutable;
using Nettrace;
using Nettrace.HighLevel;

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
    Console.WriteLine(metadata.Header);
    Console.WriteLine(NettraceEventParser.ProcessEvent(metadata, blob));
}
