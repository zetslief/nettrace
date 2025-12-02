using System.Collections.Immutable;
using Nettrace;
using Nettrace.HighLevel;
using Nettrace.PayloadParsers;

using var stream = new MemoryStream(File.ReadAllBytes("/home/cube/progs/nettrace/traces/tpleventsource_profileme.nettrace"));
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
var stacks = file.StackBlocks.Select(s => s.BuildStackInfos(file.Trace.PointerSize)).ToImmutableArray();

foreach (var eventBlob in eventBlobs)
{
    Console.WriteLine(eventBlob);
    switch (NettraceEventParser.ProcessEvent(metadataStorage[eventBlob.MetadataId].Payload, eventBlob))
    {
        case UnknownEvent unknownEvent:
            break;
        case MethodDCEndVerbose method:
            Console.WriteLine($"Method: {method}");
            var address = method.MethodID;
            foreach (var stack in stacks)
            {
                bool found = false;
                foreach (var item in stack)
                {
                    if (item.Addresses.Contains((long)address))
                    {
                        found = true;
                        Console.WriteLine($"{stack} contains address for method");
                    }
                }
                if (!found)
                {
                    // throw new InvalidOperationException($"Failed to find stack for method: {method}");
                }
            }
            break;
        case var other:
            Console.WriteLine(other);
            break;
    }
}

foreach (var stack in stacks)
{
    foreach (var item in stack)
    {
        Console.WriteLine($"Stack {item}");
        foreach (var address in item.Addresses)
        {
            Console.WriteLine($"\t address {address}");
        }
    }
}

return;
