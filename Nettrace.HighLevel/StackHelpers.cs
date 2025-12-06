using System.Collections.Immutable;
using System.Runtime.InteropServices;

using static Nettrace.NettraceReader;

namespace Nettrace.HighLevel;

public record StackInfo(int Id, ImmutableArray<ulong> Addresses);

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
}
