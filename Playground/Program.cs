using System.Collections.Immutable;
using Nettrace;
using Nettrace.HighLevel;
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
var stacks = file.StackBlocks.Select(s => s.BuildStackInfos(file.Trace.PointerSize)).ToImmutableArray();

foreach (var stack in stacks)
{
    foreach (var item in stack)
    {
        Console.WriteLine($"Stack {item}");
        foreach (var address in item.Addresses)
        {
            Console.WriteLine($"\t{address}");
        }
    }
}

foreach (var eventBlob in eventBlobs)
{
    switch (NettraceEventParser.ProcessEvent(metadataStorage[eventBlob.MetadataId].Payload, eventBlob))
    {
        case UnknownEvent unknownEvent:
            break;
        case MethodDCEndVerbose method:
            Console.WriteLine($"Method: {method}");
            var address = method.MethodStartAddress;
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
                    Console.WriteLine(method.MethodStartAddress - method.MethodToken);
                    Console.WriteLine(method.MethodStartAddress + method.MethodToken);
                    // throw new InvalidOperationException($"Failed to find stack for method: {method}");
                }
            }
            break;
        case var other:
            Console.WriteLine(other);
            break;
    }
}

return;
