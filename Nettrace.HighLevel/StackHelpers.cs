using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Nettrace.PayloadParsers;

using static Nettrace.NettraceReader;

namespace Nettrace.HighLevel;

public record StackInfo(int Id, ImmutableArray<ulong> Addresses);
public record StackBuildError(StackInfo Stack, ulong Address, MethodDCEndVerbose Current, MethodDCEndVerbose Existing)
{
    public override string ToString() => $"ERROR: multiple methods mapped to the same address ({Address})."
        + $" Stack {Stack.Id}. Existing method: {Existing}. Current method: {Current}";
}

public static class StackHelpers
{
    public static StackInfo BuildStackInfo(int stackId, int pointerSize, Stack stack)
    {
        List<ulong> addresses = new(stack.Payload.Length);
        for (int index = 0; index < stack.Payload.Length; index += pointerSize)
        {
            addresses.Add(pointerSize == 4
                ? MemoryMarshal.Read<uint>(stack.Payload.AsSpan()[index..(index + pointerSize)])
                : MemoryMarshal.Read<ulong>(stack.Payload.AsSpan()[index..(index + pointerSize)]));
        }
        return new(stackId, [.. addresses]);
    }

    public static IEnumerable<StackInfo> BuildStackInfos(this StackBlock block, int pointerSize)
        => block.Stacks.Select((s, i) => BuildStackInfo(block.FirstId + i, pointerSize, s));

    public static StackBuildError? TryBuildAddressMethodMap(
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
                        storage.Add(address, method);
                    else if (existingMethod != method)
                        return new(stack, address, method, existingMethod);
                }
            }
        }
        return null;
    }
}
