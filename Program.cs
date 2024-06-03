using System.Text;
using System.Runtime.InteropServices;

var filePath = args[0];
using var file = File.OpenRead(filePath);
NettraceReader.Read(file);

public static class NettraceReader
{
    public enum Tag : byte
    {
        NullReference = 1,
        BeginPrivateObject = 5,
        EndObject = 6,
    }

    public record Type(Tag Tag, string Name, int Vesrion, int MinimumReaderVersion);
    public record Trace(
        DateTime DateTime,
         long SynTimeQpc,
         long QpcFrequency,
         int PointerSize,
         int ProcessId,
         int NumberOfProcessors,
         int ExpectedCpuSamplingRate);
    public record Object<T>(Type Type, T Payload);

    public static void Read(Stream stream)
    {
        Span<byte> magic = stackalloc byte[8];
        stream.ReadExactly(magic);

        Console.Write("Magic: ");
        Fmt.BytesHex(magic);
        Console.WriteLine($" -> {Encoding.UTF8.GetString(magic)}");

        var streamHeader = ReadString(stream);

        Console.Write("StreamHeader: ");
        Console.WriteLine(streamHeader);

        Object<Trace> trace = ReadObject(stream, TraceDecoder);
        Console.WriteLine(trace);
    }

    private static Object<T> ReadObject<T>(Stream stream, Func<Stream, T> payloadDecoder)
    {
        var beginPrivateObject = ReadTag(stream);

        Type type = ReadType(stream);

        var payload = payloadDecoder(stream);

        var endObject = ReadTag(stream);

        return new(type, payload);
    }

    private static Type ReadType(Stream stream)
    {
        var beginPrivateObject = ReadTag(stream);

        var tag = ReadTag(stream);
        var version = ReadInt32(stream);
        var minimumReaderVersion = ReadInt32(stream);
        var name = ReadString(stream);

        var endObject = ReadTag(stream);

        return new((Tag)tag, name, version, minimumReaderVersion);
    }

    private static Trace TraceDecoder(Stream stream)
    {
        var year = ReadInt16(stream);
        var month = ReadInt16(stream);
        var dayOfWeek = ReadInt16(stream);
        var day = ReadInt16(stream);
        var hour = ReadInt16(stream);
        var minute = ReadInt16(stream);
        var second = ReadInt16(stream);
        var millisecond = ReadInt16(stream);
        var syncTimeQpc = ReadInt64(stream);
        var qpcFrequency = ReadInt64(stream);
        var pointerSize = ReadInt32(stream);
        var processId = ReadInt32(stream);
        var numberOfProcessors = ReadInt32(stream);
        var expectedCpuSamplingRate = ReadInt32(stream);
        return new(
            new(year, month, day, hour, minute, second, millisecond),
            syncTimeQpc,
            qpcFrequency,
            pointerSize,
            processId,
            numberOfProcessors,
            expectedCpuSamplingRate
        );
    }

    private static byte ReadTag(Stream stream)
    {
        Span<byte> tag = [0];
        stream.ReadExactly(tag);
        return tag[0];
    }

    private static string ReadString(Stream stream)
    {
        var length = ReadInt32(stream);
        Span<byte> content = new byte[length];
        stream.ReadExactly(content);
        return Encoding.UTF8.GetString(content);
    }

    private static short ReadInt16(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[2];
        stream.ReadExactly(lengthBytes);
        return MemoryMarshal.Read<short>(lengthBytes);
    }

    private static int ReadInt32(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        stream.ReadExactly(lengthBytes);
        return MemoryMarshal.Read<int>(lengthBytes);
    }

    private static long ReadInt64(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[8];
        stream.ReadExactly(lengthBytes);
        return MemoryMarshal.Read<long>(lengthBytes);
    }

    private static void PrintBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var @byte in bytes[..^1])
        {
            Console.Write($"{@byte:D} ");
        }
        Console.WriteLine($"{bytes[^1]:D}");
    }

}

internal static class Fmt
{
    public static void BytesHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        foreach (var @byte in bytes[..^1])
        {
            ByteHex(@byte);
            Console.Write(" ");
        }
        ByteHex(bytes[^1]);
    }

    public static void ByteHex(byte @byte)
    {
        Console.Write($"{@byte:X}");
    }

    public static void Bytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        foreach (var @byte in bytes[..^1])
        {
            Byte(@byte);
            Console.Write(" ");
        }
        Byte(bytes[^1]);
    }

    public static void Byte(byte @byte)
    {
        Console.Write($"{@byte:X}");
    }

    public static void Tag(byte @byte)
    {
        Console.Write((NettraceReader.Tag)@byte);
    }
}