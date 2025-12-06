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
    BuildAddressMethodMap(stack, methods, addressMethodMap);
    DumpStack(stack, addressMethodMap);
}

static void BuildAddressMethodMap(
    StackInfo stack,
    ImmutableArray<MethodDCEndVerbose> methods,
    Dictionary<ulong, MethodDCEndVerbose> storage)
{
    foreach (var address in stack.Addresses)
    {
        foreach (var method in methods)
        {
            var start = method.MethodStartAddress;
            var end = start + method.MethodSize;
            if (address >= start && address <= end)
            {
                if (!storage.TryGetValue(address, out var existingMethod))
                {
                    storage.Add(address, method);
                }
                else
                {
                    if (existingMethod != method)
                        throw new InvalidOperationException($"{method} already exists in the storage for {address} in {stack}.");
                }
            }
        }
    }
}

static void DumpStack(StackInfo stack, IReadOnlyDictionary<ulong, MethodDCEndVerbose> stackMethodMap)
{
    Console.WriteLine($"Stack {stack.Id} Size: {stack.Addresses.Length}");
    foreach (var address in stack.Addresses)
    {
        if (stackMethodMap.TryGetValue(address, out var method))
            Console.WriteLine($"\tAddress {address} {method.MethodNamespace} {method.MethodName} {method.MethodSignature}");
        else
            Console.WriteLine($"\tAddress {address} <Unknown>");
    }
}
