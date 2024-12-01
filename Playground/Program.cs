using Nettrace;
using System.Diagnostics;
using static System.Console;

var filepath = args.Length > 0 ? args[0] : "perf.nettrace";

var stopwatch = Stopwatch.StartNew();
var trace = NettraceReader.Read(File.OpenRead(filepath));
stopwatch.Stop();

Dictionary<int, NettraceReader.MetadataHeader>  metadataStorage = [];

foreach (var metadataBlock in trace.MetadataBlocks)
{
    foreach (var blob in metadataBlock.EventBlobs)
    {
        metadataStorage[blob.Payload.Header.MetaDataId] = blob.Payload.Header;
    }
}

WriteLine("---------");

foreach (var eventBlock in trace.EventBlocks)
{
    foreach (var blob in eventBlock.EventBlobs)
    {
        WriteLine($"{metadataStorage.ContainsKey(blob.MetadataId)} | {blob}");
    }
}

foreach (var header in metadataStorage.Values)
{
    WriteLine(header);
}

WriteLine($"File was read in {stopwatch.Elapsed}");
