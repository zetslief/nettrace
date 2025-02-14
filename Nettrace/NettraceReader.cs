using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Nettrace;

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
    public readonly ref struct EventBlobParserContext(
        int metadataId, int sequenceNumber, long captureThreadId, int processorNumber, long threadId, int stackId,
        long timeStamp, Guid activityId, Guid relatedActivityId, bool isSorted, int payloadSize)
    {
        public readonly int MetadataId = metadataId;
        public readonly int SequenceNumber = sequenceNumber;
        public readonly long CaptureThreadId = captureThreadId;
        public readonly int ProcessorNumber = processorNumber;
        public readonly long ThreadId = threadId;
        public readonly int StackId = stackId;
        public readonly long TimeStamp = timeStamp;
        public readonly Guid ActivityId = activityId;
        public readonly Guid RelatedActivityId = relatedActivityId;
        public readonly bool IsSorted = isSorted;
        public readonly int PayloadSize = payloadSize;
    }
    public record EventBlob<TPayload>(
        byte Flags, int MetadataId, int SequenceNumber, long CaptureThreadId, int ProcessorNumber, long ThreadId, int StackId,
        long TimeStamp, Guid ActivityId, Guid RelatedActivityId, bool IsSorted, int PayloadSize, TPayload Payload
    )
    {
        public static EventBlob<TPayload> Create(byte Flags, TPayload Payload, in EventBlobParserContext context) => new(
            Flags,
            context.MetadataId, context.SequenceNumber, context.CaptureThreadId,
            context.ProcessorNumber, context.ThreadId, context.StackId,
            context.TimeStamp, context.ActivityId, context.RelatedActivityId, context.IsSorted, context.PayloadSize,
            Payload
        );
    }
    public record MetadataHeader(
        int MetaDataId, string ProviderName, int EventId,
        string EventName, long Keywords, int Version, int Level);
    public record FieldV1(int TypeCode, string FieldName);
    public record FieldV2(int TypeCode, string FieldName);
    public sealed record MetadataPayload(int FieldCount, FieldV1[] Fields)
    {
        private bool PrintMembers(StringBuilder builder)
        {
            builder.AppendLine($"MetadataPayload ({FieldCount} fields):");
            foreach (var field in Fields)
            {
                builder.AppendLine($"\t\t{field}");
            }
            return true;
        }
    }
    public record MetadataEvent(MetadataHeader Header, MetadataPayload Payload);
    public record Event(byte[] Bytes);
    public sealed record RawBlock(Type Type, Memory<byte> Payload);
    public sealed record Block<T>(int BlockSize, Header Header, EventBlob<T>[] EventBlobs)
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
    public record Stack(int StackSize, byte[] Payload);
    public sealed record StackBlock(int BlockSize, int FirstId, int Count, Stack[] Stacks)
    {
        private bool PrintMembers(StringBuilder builder)
        {
            builder.AppendLine($"Block: {BlockSize} bytes");
            builder.AppendLine($"FirstId: {FirstId} Count: {Count}");
            builder.AppendLine($"Stacks: {Stacks.Length}");
            foreach (var stack in Stacks)
            {
                builder.AppendLine($"\t{stack}");
            }
            return true;
        }
    }
    public record EventThread(long ThreadId, int SequenceNumber);
    public sealed record SequencePointBlock(int BlockSize, long TimeStamp, int ThreadCount, EventThread[] Threads)
    {
        private bool PrintMembers(StringBuilder builder)
        {
            builder.AppendLine($"Sequence Point Block: {BlockSize} bytes");
            builder.AppendLine($"TimeStamp: {TimeStamp} ThreadCount: {ThreadCount}");
            builder.AppendLine($"Threads: {Threads.Length}");
            foreach (var thread in Threads)
            {
                builder.AppendLine($"\t{thread}");
            }
            return true;
        }
    }
    public record Object<T>(Type Type, T Payload);
    public delegate bool ObjectDecoder<T>(ReadOnlySpan<byte> data, [NotNullWhen(true)] out (int, T) result);
    public record NettraceFile(
        string Magic,
        Trace Trace,
        Block<MetadataEvent>[] MetadataBlocks,
        Block<Event>[] EventBlocks,
        StackBlock StackBlock,
        SequencePointBlock SequencePointBlock
    );

    public static NettraceFile Read(Stream stream)
    {
        Span<byte> magic = stackalloc byte[8];
        stream.ReadExactly(magic);

        var streamHeader = ReadString(stream);

        Trace? trace = null;
        Type? traceType = null;
        StackBlock? stack = null;
        List<Block<MetadataEvent>> metadataBlocks = [];
        List<Block<Event>> eventBlocks = [];
        SequencePointBlock? sequencePointBlock = null;

        while (TryStartObject(stream, out var type))
        {
            switch (type!.Name)
            {
                case "Trace":
                    traceType = type;
                    trace = TraceDecoder(stream);
                    break;
                case "MetadataBlock":
                    Debug.Assert(traceType is not null);
                    var metadataDecoder = BlockDecoder(CreateMetadataEventDecoder(traceType!.Vesrion));
                    var metadata = metadataDecoder(stream);
                    metadataBlocks.Add(metadata);
                    break;
                case "StackBlock":
                    stack = StackBlockDecoder(stream);
                    break;
                case "EventBlock":
                    var eventBlockDecoder = BlockDecoder(EventDecoder);
                    var @event = eventBlockDecoder(stream);
                    eventBlocks.Add(@event);
                    break;
                case "SPBlock":
                    sequencePointBlock = SequencePointBlockDecoder(stream);
                    break;
                default:
                    throw new NotImplementedException($"Unknown object type: {type}");
            }
            FinishObject(stream);
        }

        return new(
            Encoding.UTF8.GetString(magic),
            trace ?? throw new InvalidOperationException("File doesn't contain trace."),
            [.. metadataBlocks],
            [.. eventBlocks],
            stack ?? throw new InvalidOperationException("File doesn't contain stack."),
            sequencePointBlock ?? throw new InvalidOperationException("File dosn't contain SPB."));
    }

    public static bool TryStartObject(ReadOnlySpan<byte> stream, [NotNullWhen(true)] out (int, Type)? result)
    {
        if (stream.Length < sizeof(byte))
        {
            result = null;
            return false;
        }
        
        int cursor = 0;

        var tag = ReadTag(stream[MoveBy(ref cursor, sizeof(byte))]);
        if (tag == Tag.BeginPrivateObject)
        {
            if (!TryReadType(stream, ref cursor, out var maybeType))
            {
                result = null;
                return false;
            }

            result = (cursor, maybeType);
            return true;
        }
        else
        {
            Debug.Assert(tag == Tag.NullReference);
            result = null;
            return false;
        }
    }

    private static void FinishObject(ReadOnlySpan<byte> stream, ref int cursor)
    {
        var _endObject = ReadTag(stream[MoveBy(ref cursor, sizeof(byte))]);
    }

    private static bool TryReadObject<T>(ReadOnlySpan<byte> stream, ObjectDecoder<T> payloadDecoder, [NotNullWhen(true)] out (int, Object<T>)? result)
    {
        int cursor = 0;
        
        var beginPrivateObject = ReadTag(stream[MoveBy(ref cursor, sizeof(byte))]);

        if (!TryReadType(stream, ref cursor, out var maybeType))
        {
            result = null;
            return false;
        }

        if (!payloadDecoder(stream[cursor..], out var maybePayload))
        {
            result = null;
            return false;
        }
        
        var (payloadByteLength, payload) = maybePayload;
        MoveBy(ref cursor, payloadByteLength);

        var endObject = ReadTag(stream[MoveBy(ref cursor, sizeof(byte))]);

        result = (cursor, new(maybeType, payload));
        return false;
    }

    private static bool TryReadType(ReadOnlySpan<byte> data, ref int cursor, [NotNullWhen(true)] out Type? type)
    {
        if (data.Length < cursor + 11)
        {
            type = null;
            return false;
        }

        var beginPrivateObject = ReadTag(data[MoveBy(ref cursor, sizeof(byte))]);

        var tag = ReadTag(data[MoveBy(ref cursor, sizeof(byte))]);
        var version = ReadInt32(data[cursor..MoveBy(ref cursor, sizeof(int))]);
        var minimumReaderVersion = ReadInt32(data[cursor..MoveBy(ref cursor, sizeof(int))]);
        if (!TryReadString(data[cursor..], out var maybeName))
        {
            type = null;
            return false;
        }
        
        var (nameByteLength, name) = maybeName.Value;
        MoveBy(ref cursor, nameByteLength);

        var endObject = ReadTag(data[MoveBy(ref cursor, sizeof(byte))]);

        type = new(tag, name, version, minimumReaderVersion);
        return true;
    }

    private static bool TryDecodeTrace(ReadOnlySpan<byte> stream, [NotNullWhen(true)] out (int, Trace)? result)
    {
        if (stream.Length < 48)
        {
            result = null;
            return false;
        }

        int cursor = 0;
        
        var year = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var month = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var dayOfWeek = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var day = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var hour = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var minute = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var second = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var millisecond = ReadInt16(stream[cursor..MoveBy(ref cursor, sizeof(short))]);
        var syncTimeQpc = ReadInt64(stream[cursor..MoveBy(ref cursor, sizeof(long))]);
        var qpcFrequency = ReadInt64(stream[cursor..MoveBy(ref cursor, sizeof(long))]);
        var pointerSize = ReadInt32(stream[cursor..MoveBy(ref cursor, sizeof(int))]);
        var processId = ReadInt32(stream[cursor..MoveBy(ref cursor, sizeof(int))]);
        var numberOfProcessors = ReadInt32(stream[cursor..MoveBy(ref cursor, sizeof(int))]);
        var expectedCpuSamplingRate = ReadInt32(stream[cursor..MoveBy(ref cursor, sizeof(int))]);
        
        result = (cursor, new(
            new(year, month, day, hour, minute, second, millisecond),
            syncTimeQpc,
            qpcFrequency,
            pointerSize,
            processId,
            numberOfProcessors,
            expectedCpuSamplingRate
        ));
        return true;
    }

    public delegate T PayloadDecoder<T>(in ReadOnlySpan<byte> bytes);

    private static bool TryRawReadBlock(ReadOnlySpan<byte> data, Type type, [NotNullWhen(true)] out (int, RawBlock)? result)
    {
        if (data.Length < sizeof(int))
        {
            result = null;
            return false;
        }
        
        int cursor = 0;
        var blockSize = ReadInt32(data[cursor..MoveBy(ref cursor, sizeof(int))]);

        if (cursor + blockSize > data.Length)
        {
            result = null;
            return false;
        }

        // TODO: Align(stream);
        Memory<byte> buffer = new byte[blockSize];
        data[cursor..MoveBy(ref cursor, blockSize)].CopyTo(buffer.Span);
        result = (cursor, new(type, buffer));
        return true;
    }

    private static Block<T> BlockDecoder<T>(RawBlock rawBlock, PayloadDecoder<T> payloadDecoder)
    {
        ReadOnlySpan<byte> blockBytes = rawBlock.Payload.Span;

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
        EventBlobParserContext context = default;

        // event blob
        var eventBlobs = new List<EventBlob<T>>(100);
        while (cursor < blockBytes.Length)
        {
            var flag = blockBytes[cursor++];
            var firstBitIsSet = (flag & 1) != 0;
            var secondBitIsSet = (flag & 2) != 0;
            var thirdBitIsSet = (flag & 4) != 0;
            var forthBitIsSet = (flag & 8) != 0;
            var fifthBitIsSet = (flag & 16) != 0;
            var sixthBitIsSet = (flag & 32) != 0;
            var seventhBitIsSet = (flag & 64) != 0;
            var eighthBitIsSet = (flag & 128) != 0;

            int metadataId = firstBitIsSet ? ReadVarInt32(blockBytes, ref cursor) : context.MetadataId;
            int sequenceNumber = secondBitIsSet
                ? ReadVarInt32(blockBytes, ref cursor) + context.SequenceNumber
                : context.SequenceNumber;
            sequenceNumber = metadataId == 0 ? sequenceNumber : sequenceNumber + 1;
            long captureThreadId = secondBitIsSet ? ReadVarInt64(blockBytes, ref cursor) : context.ThreadId;
            int processorNumber = secondBitIsSet ? ReadVarInt32(blockBytes, ref cursor) : context.ProcessorNumber;
            long threadId = thirdBitIsSet ? ReadVarInt64(blockBytes, ref cursor) : context.ThreadId;
            int stackId = forthBitIsSet ? ReadVarInt32(blockBytes, ref cursor) : context.StackId;
            long timeStamp = ReadVarInt64(blockBytes, ref cursor) + context.TimeStamp;
            Guid activityId = fifthBitIsSet ? ReadGuid(blockBytes, ref cursor) : context.ActivityId;
            Guid relatedActivityId = sixthBitIsSet ? ReadGuid(blockBytes, ref cursor) : context.RelatedActivityId;
            bool isSorted = seventhBitIsSet;
            int payloadSize = eighthBitIsSet ? ReadVarInt32(blockBytes, ref cursor) : context.PayloadSize;

            context = new(
                metadataId, sequenceNumber, captureThreadId, processorNumber, threadId, stackId,
                timeStamp, activityId, relatedActivityId, isSorted, payloadSize);

            ReadOnlySpan<byte> payload = blockBytes[cursor..MoveBy(ref cursor, payloadSize)];

            eventBlobs.Add(EventBlob<T>.Create(flag, payloadDecoder(in payload), in context));
        }

        return new(rawBlock.Payload.Length, new Header(headerSize, flags, minTimestamp, maxTimestamp, reserved.ToArray()), [.. eventBlobs]);
    }

    private static PayloadDecoder<MetadataEvent> CreateMetadataEventDecoder(int fileVersion)
        => (in ReadOnlySpan<byte> bytes) => MetadataEventDecoder(in bytes, fileVersion);

    private static MetadataEvent MetadataEventDecoder(in ReadOnlySpan<byte> bytes, int fileVersion)
    {
        int cursor = 0;

        var payloadMetadataId = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
        var providerName = ReadUnicode(bytes, ref cursor);
        var eventId = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
        var eventName = ReadUnicode(bytes, ref cursor);
        long keywords = MemoryMarshal.Read<long>(bytes[cursor..MoveBy(ref cursor, 8)]);
        int version = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
        int level = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);

        var fieldsV1 = new List<FieldV1>();

        // if the metadata event specifies a V2Params tag, the event must have an empty V1 parameter FieldCount
        // and no field definitions.
        int fieldCount = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
        for (int fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
        {
            int typeCode = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
            string fieldName = ReadUnicode(bytes, ref cursor);
            fieldsV1.Add(new(typeCode, fieldName));
        }

        if (fileVersion >= 5)
        {
            int tagPaylodBytes = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
            byte tagKind = bytes[cursor++];
            // followed by tag payload
            const byte opCode = 1;
            const byte v2Params = 2;
            switch (tagKind)
            {
                case opCode:
                    byte eventOpCode = bytes[cursor++];
                    break;
                case v2Params:
                    int v2FieldCount = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
                    int v2TypeCode = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
                    const int eventPipeTypeCodeArray = 19;
                    if (v2TypeCode == eventPipeTypeCodeArray)
                    {
                        int arrayTypeCode = MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, 4)]);
                    }
                    // TODO: payload description (it also may be that description does not exist :))
                    string v2FieldName = ReadUnicode(bytes, ref cursor);
                    break;
                default:
                    throw new NotSupportedException($"Unknown tag kind: '{tagKind}'!");
            }
            throw new NotImplementedException($"V2 fields are not implemented yet!");
        }

        var metadataEventHeader = new MetadataHeader(
            payloadMetadataId, providerName, eventId, eventName, keywords, version, level);
        var metadataPayload = new MetadataPayload(fieldCount, [.. fieldsV1]);
        return new(metadataEventHeader, metadataPayload);
    }

    private static Event EventDecoder(in ReadOnlySpan<byte> bytes) => new(bytes.ToArray());

    private static byte[] RawEventDecoder(in ReadOnlySpan<byte> bytes)
        => bytes.ToArray();

    private static StackBlock StackBlockDecoder(Stream stream)
    {
        int blockSize = ReadInt32(stream);

        Align(stream);

        Span<byte> blockBytes = new byte[blockSize];
        stream.ReadExactly(blockBytes);

        var cursor = 0;

        var firstId = MemoryMarshal.Read<int>(blockBytes[cursor..MoveBy(ref cursor, 4)]);
        var count = MemoryMarshal.Read<int>(blockBytes[cursor..MoveBy(ref cursor, 4)]);

        var stacks = new Stack[count];
        for (int stackIndex = 0; stackIndex < stacks.Length; ++stackIndex)
        {
            var stackSize = MemoryMarshal.Read<int>(blockBytes[cursor..MoveBy(ref cursor, 4)]);
            stacks[stackIndex] = new(stackSize, [.. blockBytes[cursor..MoveBy(ref cursor, stackSize)]]);
        }
        return new(blockSize, firstId, count, stacks);
    }

    private static SequencePointBlock SequencePointBlockDecoder(Stream stream)
    {
        var blockSize = ReadInt32(stream);

        Align(stream);

        Span<byte> blockBytes = new byte[blockSize];
        stream.ReadExactly(blockBytes);
        int cursor = 0;

        long timeStamp = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, 8)]);
        int threadCount = MemoryMarshal.Read<int>(blockBytes[cursor..MoveBy(ref cursor, 4)]);

        var threads = new EventThread[threadCount];
        for (int threadIndex = 0; threadIndex < threads.Length; ++threadIndex)
        {
            long threadId = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, 8)]);
            int sequenceNumber = MemoryMarshal.Read<int>(blockBytes[cursor..MoveBy(ref cursor, 4)]);
            threads[threadIndex] = new(threadId, sequenceNumber);
        }

        return new(blockSize, timeStamp, threadCount, threads);
    }

    private static string SkipPayloadDecoder(Stream stream)
    {
        while (ReadByte(stream) != (byte)Tag.EndObject)
        {
            // skip
        }
        stream.Seek(-1, SeekOrigin.Current);
        return "Empty";
    }

    private static int MoveBy(ref int value, int by)
    {
        value += by;
        return value;
    }

    private static void Align(Stream stream)
    {
        var padding = (4 - stream.Position % 4) % 4;
        stream.Seek(padding, SeekOrigin.Current);
    }

    private static Tag ReadTag(byte data) => (Tag)data;

    public static bool TryReadString(ReadOnlySpan<byte> data, [NotNullWhen(true)] out (int, string)? result)
    {
        if (data.Length < sizeof(int))
        {
            result = null;
            return false;
        }

        var cursor = 0;
        var length = ReadInt32(data[cursor..MoveBy(ref cursor, sizeof(int))]);
        if (length > data.Length)
        {
            result = null;
            return false;
        }
        
        var @string = Encoding.UTF8.GetString(data[cursor..MoveBy(ref cursor, length)]);
        result = (cursor, @string);
        return true;
    }

    private static byte ReadByte(Stream stream)
    {
        Span<byte> content = [0];
        stream.ReadExactly(content);
        return content[0];
    }

    private static short ReadInt16(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<short>(data);

    private static int ReadInt32(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<int>(data);

    private static long ReadInt64(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<long>(data);

    private static void PrintBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var @byte in bytes[..^1])
        {
            Console.Write($"{@byte:D} ");
        }
        Console.WriteLine($"{bytes[^1]:D}");
    }

    private static int ReadVarInt32(ReadOnlySpan<byte> bytes, ref int cursor)
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
        return result;
    }

    private static long ReadVarInt64(ReadOnlySpan<byte> bytes, ref int cursor)
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

    private static Guid ReadGuid(ReadOnlySpan<byte> bytes, ref int cursor)
        => MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]);

    private static string ReadUnicode(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        var startCursor = cursor;
        ReadOnlySpan<byte> nullBytes = [0, 0];
        var nextByte = bytes[cursor..MoveBy(ref cursor, 2)];
        while (nextByte[0] != nullBytes[0] || nextByte[1] != nullBytes[1])
        {
            nextByte = bytes[cursor..MoveBy(ref cursor, 2)];
        }

        var providerNameBytes = bytes[startCursor..(cursor - 2)];

        return Encoding.Unicode.GetString(providerNameBytes);
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
