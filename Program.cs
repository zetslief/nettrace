using System.Text;
using System.Runtime.InteropServices;
using System;

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
    public record Header(short HeaderSize, short Flags, long MinTimestamp, long MaxTimestamp, byte[] Reserved);
    public record EventBlob(
        byte Flags,
        int MetadataId,
        int SequenceNumber,
        long CaptureThreadId,
        int ProcessorNumber,
        long ThreadId,
        int StackId,
        long TimeStamp,
        Guid ActivityId,
        Guid RelatedActivityId,
        bool IsSorted,
        int PayloadSize,
        byte[] Payload
    );
    public sealed record Block(int BlockSize, Header Header, EventBlob[] EventBlobs) // MetaddtaaBlock block uses the same layout as EventBlock 
    {
        private bool PrintMembers(StringBuilder builder)
        {
            builder.AppendLine($"Block: {BlockSize} bytes");
            builder.AppendLine($"Header: {Header}");
            builder.AppendLine($"EventBlobs: {EventBlobs.Length}");
            foreach (var eventBlob in EventBlobs)
            {
                builder.AppendLine($"\t{eventBlob}");
            }
            return true;
        }
    }
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

        Object<Block> firstBlock = ReadObject(stream, BlockDecoder);
        Console.WriteLine(firstBlock);

        Object<string> next = ReadObject(stream, SkipPayloadDecoder);
        Console.WriteLine(next);
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

    private static Block BlockDecoder(Stream stream)
    {
        var blockSize = ReadInt32(stream);
        
        long alignOffset = 4 - (stream.Position % 4);
        stream.Seek(alignOffset, SeekOrigin.Current);

        Span<byte> blockBytes = new byte[blockSize];
        stream.ReadExactly(blockBytes);

        int cursor = 0;
        var headerSize = MemoryMarshal.Read<short>(blockBytes[cursor..MoveBy(ref cursor, 2)]);
        var flags = MemoryMarshal.Read<short>(blockBytes[cursor..MoveBy(ref cursor, 2)]);
        var minTimestamp = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, 8)]);
        var maxTimestamp = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, 8)]);

        var reserved = blockBytes[cursor..MoveBy(ref cursor, headerSize - cursor)];

        if ((flags & 1) != 1)
        {
            throw new NotImplementedException($"Uncompressed event blob format is not implemented!");
        }

        // parsing event blobs

        // parser state for this block
        int previousMetadataId = 0;
        int previousSequenceNumber = 0;
        long previousCaptureThreadId = 0;
        int previousProcessorNumber = 0;
        long previousThreadId = 0;
        int previousStackId = 0;
        long previousTimeStamp = 0;
        Guid previousActivityId = Guid.Empty;
        Guid previousRelatedActivityId = Guid.Empty;
        int previousPayloadSize = 0;

        // event blob
        var eventBlobs = new List<EventBlob>(100);
        while (cursor < blockBytes.Length)
        {
            var flag = blockBytes[MoveBy(ref cursor, 1)];
            var firstBitIsSet = (flag & 1) == 1;
            var secondBitIsSet = (flag & 2) == 1;
            var thirdBitIsSet = (flag & 4) == 1;
            var forthBitIsSet = (flag & 8) == 1;
            var fifthBitIsSet = (flag & 16) == 1;
            var sixthBitIsSet = (flag & 32) == 1;
            var seventhBitIsSet = (flag & 64) == 1;
            var eighthBitIsSet = (flag & 128) == 1;

            var metadataId = firstBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor)
                : previousMetadataId;
            var sequenceNumber = secondBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor) + previousSequenceNumber
                : previousSequenceNumber;
            if (metadataId != 0)
            {
                ++sequenceNumber;
            }
            long captureThreadId = secondBitIsSet
                ? ReadVarInt64(blockBytes, ref cursor)
                : previousCaptureThreadId;
            int processorNumber = secondBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor)
                : previousProcessorNumber; 
            long threadId = thirdBitIsSet
                ? ReadVarInt64(blockBytes, ref cursor)
                : previousThreadId;
            int stackId = forthBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor)
                : previousStackId;
            long timeStamp = ReadVarInt64(blockBytes, ref cursor) + previousTimeStamp;
            Guid activityId = fifthBitIsSet
                ? ReadGuid(blockBytes, ref cursor)
                : previousActivityId;
            Guid relatedActivityId = sixthBitIsSet
                ? ReadGuid(blockBytes, ref cursor)
                : previousRelatedActivityId;
            bool isSorted = seventhBitIsSet;
            int payloadSize = eighthBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor)
                : previousPayloadSize;

            previousMetadataId = metadataId;
            previousSequenceNumber = sequenceNumber;
            previousCaptureThreadId = captureThreadId;
            previousProcessorNumber = processorNumber;
            previousThreadId = threadId;
            previousStackId = stackId;
            previousTimeStamp = timeStamp;
            previousActivityId = activityId;
            previousRelatedActivityId = relatedActivityId;
            previousPayloadSize = payloadSize;
            
            ReadOnlySpan<byte> payload = blockBytes[cursor..MoveBy(ref cursor, payloadSize)];

            eventBlobs.Add(new(
                flag,
                metadataId,
                sequenceNumber,
                captureThreadId,
                processorNumber,
                threadId,
                stackId,
                timeStamp,
                activityId,
                relatedActivityId,
                isSorted,
                payloadSize,
                payload.ToArray()));
        }

        return new(blockSize, new Header(headerSize, flags, minTimestamp, maxTimestamp, reserved.ToArray()), [.. eventBlobs]);
    }

    private static string SkipPayloadDecoder(Stream stream)
    {
        while (ReadTag(stream) != Tag.EndObject)
        {
            // skip
        }
        stream.Seek(-1, SeekOrigin.Current);
        return "Empty";
    }

    static int MoveBy(ref int value, int by) => value += by; 

    private static Tag ReadTag(Stream stream) => (Tag)ReadByte(stream);

    private static string ReadString(Stream stream)
    {
        var length = ReadInt32(stream);
        Span<byte> content = new byte[length];
        stream.ReadExactly(content);
        return Encoding.UTF8.GetString(content);
    }

    private static byte ReadByte(Stream stream)
    {
        Span<byte> content = [0];
        stream.ReadExactly(content);
        return content[0];
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

    private static int ReadVarInt32(Span<byte> bytes, ref int cursor)
    {
        int result = 0;
        for (int byteIndex = 0; byteIndex < cursor + 5; ++byteIndex, ++cursor)
        {
            int @byte = bytes[byteIndex];
            if (@byte == 0)
            {
                break;
            }
            @byte <<= 7 * byteIndex;
            result |= @byte;
        }
        return result;
    }

    private static long ReadVarInt64(Span<byte> bytes, ref int cursor)
    {
        long result = 0;
        for (int byteIndex = 0; byteIndex < cursor + 9; ++byteIndex, ++cursor)
        {
            long @byte = bytes[byteIndex];
            if (@byte == 0)
                break;
            @byte <<= 7 * byteIndex;
            result |= @byte;
        }
        return result;
    }

    private static Guid ReadGuid(Span<byte> bytes, ref int cursor)
        => MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]);
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