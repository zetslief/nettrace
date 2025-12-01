namespace Nettrace;

public static class Readers
{
    public static int ReadVarInt32(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        int result = 0;
        var maxIndex = 5;
        for (int byteIndex = 0; byteIndex < maxIndex; ++byteIndex)
        {
            int @byte = bytes[cursor++];
            bool @break = (@byte & 1 << 7) == 0;
            @byte &= (1 << 7) - 1;
            @byte <<= 7 * byteIndex;
            result |= @byte;
            if (@break)
                break;
        }
        return (int)result;
    }

    public static long ReadVarInt64(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        long result = 0;
        var maxIndex = 10;
        for (int byteIndex = 0; byteIndex < maxIndex; ++byteIndex)
        {
            long @byte = bytes[cursor++];
            bool @break = (@byte & 1 << 7) == 0;
            @byte &= (1 << 7) - 1;
            @byte <<= 7 * byteIndex;
            result |= @byte;
            if (@break)
                break;
        }
        return result;
    }

}
