using System.Diagnostics;

namespace Nettrace;

public static class Readers
{
    private const byte low_mask = 0x7f;
    private const byte high_mask = 1 << 7;
    private const byte second_high_mask = 1 << 6;

    public static int ReadVarInt32(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        uint result = 0;
        var size = sizeof(int) * 8;
        var shift = 0;
        byte @byte = high_mask;
        while ((@byte & high_mask) == high_mask)
        {
            @byte = bytes[cursor++];
            result |= (uint)(@byte & low_mask) << shift;
            shift += 7;
        }
        if ((shift < size) && ((@byte & second_high_mask) == second_high_mask))
        {
            result |= (uint)-(1 << shift);
        }
        return (int)result;
    }

    public static long ReadVarInt64(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        ulong result = 0;
        var size = sizeof(long) * 8;
        var shift = 0;
        byte @byte = high_mask;
        while ((@byte & high_mask) == high_mask)
        {
            @byte = bytes[cursor++];
            result |= (ulong)(@byte & low_mask) << shift;
            shift += 7;
        }
        if ((shift < size) && ((@byte & second_high_mask) == second_high_mask))
        {
            result |= (ulong)(long)-(1 << shift);
        }
        return (long)result;
    }
}
