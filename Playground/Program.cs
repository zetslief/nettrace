using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
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

var events = file.EventBlocks
    .SelectMany(b => b.EventBlobs)
    .Select(b => (object)NettraceEventParser.ProcessEvent(metadataStorage[b.MetadataId].Payload, b))
    .ToImmutableArray();
var stacks = file.StackBlocks.Select(s => s.BuildStackInfos(file.Trace.PointerSize))
    .SelectMany(s => s)
    .ToImmutableArray();

Dictionary<ulong, MethodDCEndVerbose> addressMethodMap = [];
var methods = events
    .Where(e => e is MethodDCEndVerbose)
    .Cast<MethodDCEndVerbose>()
    .OrderBy(m => m.MethodStartAddress)
    .ToImmutableArray();
foreach (var stack in stacks)
{
    var error = StackHelpers.TryBuildAddressMethodMap(stack, methods, addressMethodMap);
    if (error is not null) throw new InvalidOperationException($"{error}");
    DumpStack(stack, addressMethodMap);
}

static void DumpStack(StackInfo stack, IReadOnlyDictionary<ulong, MethodDCEndVerbose> stackMethodMap)
{
    Console.WriteLine($"Stack {stack.Id} Height: {stack.Addresses.Length}");
    foreach (var address in stack.Addresses)
    {
        if (stackMethodMap.TryGetValue(address, out var method))
            Console.WriteLine($"\t0x{address:x} {method.MethodSignature} {method.MethodNamespace} {method.MethodName}");
        else
            Console.WriteLine($"\t0x{address:x} <Unknown>");
    }
}
