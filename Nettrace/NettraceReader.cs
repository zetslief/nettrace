using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using static Nettrace.Readers;

namespace Nettrace;

public static class NettraceReader
{
    public enum Tag : byte
    {
        NullReference = 1,
        BeginPrivateObject = 5,
        EndObject = 6,
    }

    public record Type(Tag Tag, string Name, int Version, int MinimumReaderVersion);
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
        public static EventBlob<TPayload> Create(byte flags, TPayload payload, in EventBlobParserContext context) => new(
            flags,
            context.MetadataId, context.SequenceNumber, context.CaptureThreadId,
            context.ProcessorNumber, context.ThreadId, context.StackId,
            context.TimeStamp, context.ActivityId, context.RelatedActivityId, context.IsSorted, context.PayloadSize,
            payload
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
    public record Event(ReadOnlyMemory<byte> Bytes);
    private delegate T PayloadDecoder<out T>(in ReadOnlySpan<byte> buffer);
    public sealed record RawBlock(Type Type, ReadOnlyMemory<byte> Buffer);
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
    public record NettraceFile(
        string Magic,
        Trace Trace,
        Block<MetadataEvent>[] MetadataBlocks,
        Block<Event>[] EventBlocks,
        StackBlock[] StackBlocks,
        SequencePointBlock[] SequencePointBlocks
    );

    public static NettraceFile Read(Stream stream)
    {
        Memory<byte> memory = new byte[stream.Length];
        stream.ReadExactly(memory.Span);

        int globalCursor = 0;
        ReadOnlySpan<byte> buffer = memory.Span;

        ReadOnlySpan<byte> magic = buffer[..MoveBy(ref globalCursor, 8)];

        if (!TryReadStreamHeader(buffer[globalCursor..], out var maybeStreamHeader))
            throw new InvalidOperationException($"Failed to read string header.");

        var (streamHeaderLength, streamHeader) = maybeStreamHeader.Value;
        globalCursor += streamHeaderLength;

        Trace? trace = null;
        Type? traceType = null;
        List<Block<MetadataEvent>> metadataBlocks = [];
        List<Block<Event>> eventBlocks = [];
        List<StackBlock> stacks = [];
        List<SequencePointBlock> sequencePointBlocks = [];

        while (TryStartObject(buffer[globalCursor..], out var maybeType))
        {
            var (typeLength, type) = maybeType.Value;
            globalCursor += typeLength;

            switch (type.Name)
            {
                case "Trace":
                    traceType = type;
                    if (!TryReadTrace(buffer[globalCursor..], out var maybeTrace))
                        throw new InvalidOperationException($"Failed to decode trace. Cursor: {globalCursor}");
                    (var traceLength, trace) = maybeTrace.Value;
                    globalCursor += traceLength;
                    break;
                case "MetadataBlock":
                    Debug.Assert(traceType is not null);
                    if (!TryReadRawBlock(buffer[globalCursor..], type, globalCursor, out var maybeMetadataRawBlock))
                        throw new InvalidOperationException($"Failed to read raw block. Cursor: {globalCursor}");
                    var (metadataRawBlockLength, metadataRawBlock) = maybeMetadataRawBlock.Value;
                    globalCursor += metadataRawBlockLength;
                    var metadataBlock = BlockDecoder(metadataRawBlock, MetadataEventDecoder(metadataRawBlock));
                    metadataBlocks.Add(metadataBlock);
                    break;
                case "StackBlock":
                    Debug.Assert(traceType is not null);
                    if (!TryReadRawBlock(buffer[globalCursor..], type, globalCursor, out var maybeStackRawBlock))
                        throw new InvalidOperationException($"Failed to read raw block. Cursor: {globalCursor}");
                    var (stackRawBlockLength, stackRawBlock) = maybeStackRawBlock.Value;
                    globalCursor += stackRawBlockLength;
                    var stack = StackBlockDecoder(stackRawBlock);
                    stacks.Add(stack);
                    break;
                case "EventBlock":
                    Debug.Assert(traceType is not null);
                    if (!TryReadRawBlock(buffer[globalCursor..], type, globalCursor, out var maybeEventRawBlock))
                        throw new InvalidOperationException($"Failed to read raw block. Cursor: {globalCursor}");
                    var (eventRawBlockLength, eventRawBlock) = maybeEventRawBlock.Value;
                    globalCursor += eventRawBlockLength;
                    var eventBlock = BlockDecoder(eventRawBlock, EventDecoder);
                    eventBlocks.Add(eventBlock);
                    break;
                case "SPBlock":
                    Debug.Assert(traceType is not null);
                    if (!TryReadRawBlock(buffer[globalCursor..], type, globalCursor, out var maybeSpRawBlock))
                        throw new InvalidOperationException($"Failed to read raw block. Cursor: {globalCursor}");
                    var (spRawBlockLength, spRawBlock) = maybeSpRawBlock.Value;
                    globalCursor += spRawBlockLength;
                    var sequencePointBlock = SequencePointBlockDecoder(spRawBlock);
                    sequencePointBlocks.Add(sequencePointBlock);
                    break;
                default:
                    throw new NotImplementedException($"Unknown object type: {type}");
            }

            if (!TryFinishObject(buffer, out var maybeFinishObjectLength))
                throw new InvalidOperationException($"Failed to finish object. Cursor {globalCursor}");
            globalCursor += maybeFinishObjectLength.Value;
        }

        return new(
            Encoding.UTF8.GetString(magic),
            trace ?? throw new InvalidOperationException("File doesn't contain trace."),
            [.. metadataBlocks],
            [.. eventBlocks],
            [.. stacks],
            [.. sequencePointBlocks]);
    }

    public static bool TryReadStreamHeader(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out (int, string)? result)
        => TryReadString(buffer[..], out result);

    public static bool TryStartObject(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out (int, Type)? result)
    {
        if (buffer.Length < sizeof(byte))
        {
            result = null;
            return false;
        }

        int cursor = 0;

        var tag = ReadTag(buffer[cursor++]);
        if (tag == Tag.BeginPrivateObject)
        {
            if (!TryReadType(buffer, ref cursor, out var maybeType))
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

    public static bool TryFinishObject(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out int? finishObjectLength)
    {
        if (buffer.Length < 1)
        {
            finishObjectLength = null;
            return false;
        }

        var _endObject = ReadTag(buffer[0]);
        finishObjectLength = 1;
        return true;
    }

    public static bool TryReadType(ReadOnlySpan<byte> buffer, ref int cursor, [NotNullWhen(true)] out Type? type)
    {
        if (buffer.Length < cursor + 11)
        {
            type = null;
            return false;
        }

        var beginPrivateObject = ReadTag(buffer[cursor++]);

        var tag = ReadTag(buffer[cursor++]);
        var version = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);
        var minimumReaderVersion = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);
        if (!TryReadString(buffer[cursor..], out var maybeName))
        {
            type = null;
            return false;
        }

        var (nameByteLength, name) = maybeName.Value;
        MoveBy(ref cursor, nameByteLength);

        if (cursor + 1 >= buffer.Length)
        {
            type = null;
            return false;
        }

        var endObject = ReadTag(buffer[cursor++]);

        type = new(tag, name, version, minimumReaderVersion);
        return true;
    }

    public static bool TryReadTrace(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out (int, Trace)? result)
    {
        if (buffer.Length < 48)
        {
            result = null;
            return false;
        }

        int cursor = 0;

        var year = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var month = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var dayOfWeek = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var day = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var hour = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var minute = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var second = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var millisecond = ReadInt16(buffer[cursor..MoveBy(ref cursor, sizeof(short))]);
        var syncTimeQpc = ReadInt64(buffer[cursor..MoveBy(ref cursor, sizeof(long))]);
        var qpcFrequency = ReadInt64(buffer[cursor..MoveBy(ref cursor, sizeof(long))]);
        var pointerSize = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);
        var processId = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);
        var numberOfProcessors = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);
        var expectedCpuSamplingRate = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);

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

    public static bool TryReadRawBlock(ReadOnlySpan<byte> buffer, Type type, int globalCursor, [NotNullWhen(true)] out (int, RawBlock)? result)
    {
        if (buffer.Length < sizeof(int))
        {
            result = null;
            return false;
        }

        int cursor = 0;
        var blockSize = ReadInt32(buffer[cursor..MoveBy(ref cursor, sizeof(int))]);

        var alignLength = Align(globalCursor);
        cursor += alignLength;

        if (cursor + blockSize > buffer.Length)
        {
            result = null;
            return false;
        }

        Memory<byte> blockBuffer = new byte[blockSize];
        buffer[cursor..MoveBy(ref cursor, blockSize)].CopyTo(blockBuffer.Span);
        result = (cursor, new(type, blockBuffer));
        return true;
    }

    private static Block<T> BlockDecoder<T>(RawBlock rawBlock, PayloadDecoder<T> payloadDecoder)
    {
        ReadOnlySpan<byte> blockBytes = rawBlock.Buffer.Span;

        int cursor = 0;
        var headerSize = MemoryMarshal.Read<short>(blockBytes[cursor..MoveBy(ref cursor, sizeof(short))]);
        var flags = MemoryMarshal.Read<short>(blockBytes[cursor..MoveBy(ref cursor, sizeof(short))]);
        var minTimestamp = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, sizeof(long))]);
        var maxTimestamp = MemoryMarshal.Read<long>(blockBytes[cursor..MoveBy(ref cursor, sizeof(long))]);

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
            if (blockBytes.Length == 597)
                Console.WriteLine("hello");
            var flag = blockBytes[cursor++];
            var firstBitIsSet = (flag & 1) != 0;
            var secondBitIsSet = (flag & 2) != 0;
            var thirdBitIsSet = (flag & 4) != 0;
            var forthBitIsSet = (flag & 8) != 0;
            var fifthBitIsSet = (flag & 16) != 0;
            var sixthBitIsSet = (flag & 32) != 0;
            var seventhBitIsSet = (flag & 64) != 0;
            var eighthBitIsSet = (flag & 128) != 0;

            int metadataId = firstBitIsSet ? ReadVarUInt32(blockBytes, ref cursor) : context.MetadataId;
            int sequenceNumber = secondBitIsSet
                ? ReadVarUInt32(blockBytes, ref cursor) + context.SequenceNumber
                : context.SequenceNumber;
            sequenceNumber = metadataId == 0 ? sequenceNumber : sequenceNumber + 1;
            long captureThreadId = secondBitIsSet ? ReadVarUInt64(blockBytes, ref cursor) : context.ThreadId;
            int processorNumber = secondBitIsSet ? ReadVarUInt32(blockBytes, ref cursor) : context.ProcessorNumber;
            long threadId = thirdBitIsSet ? ReadVarUInt64(blockBytes, ref cursor) : context.ThreadId;
            int stackId = forthBitIsSet ? ReadVarUInt32(blockBytes, ref cursor) : context.StackId;
            long timeStamp = ReadVarUInt64(blockBytes, ref cursor) + context.TimeStamp;
            Guid activityId = fifthBitIsSet ? ReadGuid(blockBytes, ref cursor) : context.ActivityId;
            Guid relatedActivityId = sixthBitIsSet ? ReadGuid(blockBytes, ref cursor) : context.RelatedActivityId;
            bool isSorted = seventhBitIsSet;
            int payloadSize = eighthBitIsSet ? ReadVarUInt32(blockBytes, ref cursor) : context.PayloadSize;

            context = new(
                metadataId, sequenceNumber, captureThreadId, processorNumber, threadId, stackId,
                timeStamp, activityId, relatedActivityId, isSorted, payloadSize);

            ReadOnlySpan<byte> payload = blockBytes[cursor..MoveBy(ref cursor, payloadSize)];
            eventBlobs.Add(EventBlob<T>.Create(flag, payloadDecoder(in payload), in context));
        }

        return new(rawBlock.Buffer.Length, new Header(headerSize, flags, minTimestamp, maxTimestamp, reserved.ToArray()), [.. eventBlobs]);
    }

    private static PayloadDecoder<MetadataEvent> MetadataEventDecoder(RawBlock rawBlock)
        => (in ReadOnlySpan<byte> buffer) => MetadataEventDecoder(in buffer, rawBlock.Type.Version);

    private static MetadataEvent MetadataEventDecoder(in ReadOnlySpan<byte> buffer, int fileVersion)
    {
        int cursor = 0;

        var payloadMetadataId = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
        var providerName = ReadUnicode(buffer, ref cursor);
        var eventId = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
        var eventName = ReadUnicode(buffer, ref cursor);
        long keywords = MemoryMarshal.Read<long>(buffer[cursor..MoveBy(ref cursor, 8)]);
        int version = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
        int level = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);

        var fieldsV1 = new List<FieldV1>();

        // if the metadata event specifies a V2Params tag, the event must have an empty V1 parameter FieldCount
        // and no field definitions.
        int fieldCount = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
        for (int fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
        {
            int typeCode = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
            string fieldName = ReadUnicode(buffer, ref cursor);
            fieldsV1.Add(new(typeCode, fieldName));
        }

        if (fileVersion >= 5)
        {
            int tagPaylodBytes = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
            byte tagKind = buffer[cursor++];
            // followed by tag payload
            const byte opCode = 1;
            const byte v2Params = 2;
            switch (tagKind)
            {
                case opCode:
                    byte eventOpCode = buffer[cursor++];
                    break;
                case v2Params:
                    int v2FieldCount = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
                    int v2TypeCode = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
                    const int eventPipeTypeCodeArray = 19;
                    if (v2TypeCode == eventPipeTypeCodeArray)
                    {
                        int arrayTypeCode = MemoryMarshal.Read<int>(buffer[cursor..MoveBy(ref cursor, 4)]);
                    }
                    // TODO: payload description (it also may be that description does not exist :))
                    string v2FieldName = ReadUnicode(buffer, ref cursor);
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

    private static Event EventDecoder(in ReadOnlySpan<byte> buffer) => new(buffer.ToArray());

    private static StackBlock StackBlockDecoder(RawBlock block)
    {
        ReadOnlySpan<byte> blockBuffer = block.Buffer.Span;
        var cursor = 0;

        var firstId = MemoryMarshal.Read<int>(blockBuffer[cursor..MoveBy(ref cursor, 4)]);
        var count = MemoryMarshal.Read<int>(blockBuffer[cursor..MoveBy(ref cursor, 4)]);

        var stacks = new Stack[count];
        for (int stackIndex = 0; stackIndex < stacks.Length; ++stackIndex)
        {
            var stackSize = MemoryMarshal.Read<int>(blockBuffer[cursor..MoveBy(ref cursor, 4)]);
            stacks[stackIndex] = new(stackSize, [.. blockBuffer[cursor..MoveBy(ref cursor, stackSize)]]);
        }
        return new(blockBuffer.Length, firstId, count, stacks);
    }

    private static SequencePointBlock SequencePointBlockDecoder(RawBlock block)
    {
        ReadOnlySpan<byte> blockBuffer = block.Buffer.Span;

        int cursor = 0;

        long timeStamp = MemoryMarshal.Read<long>(blockBuffer[cursor..MoveBy(ref cursor, 8)]);
        int threadCount = MemoryMarshal.Read<int>(blockBuffer[cursor..MoveBy(ref cursor, 4)]);

        var threads = new EventThread[threadCount];
        for (int threadIndex = 0; threadIndex < threads.Length; ++threadIndex)
        {
            long threadId = MemoryMarshal.Read<long>(blockBuffer[cursor..MoveBy(ref cursor, 8)]);
            int sequenceNumber = MemoryMarshal.Read<int>(blockBuffer[cursor..MoveBy(ref cursor, 4)]);
            threads[threadIndex] = new(threadId, sequenceNumber);
        }

        return new(blockBuffer.Length, timeStamp, threadCount, threads);
    }

    private static int MoveBy(ref int value, int by)
    {
        value += by;
        return value;
    }

    private static int Align(int cursor)
        => (4 - cursor % 4) % 4;

    private static Tag ReadTag(byte data) => (Tag)data;

    private static bool TryReadString(ReadOnlySpan<byte> data, [NotNullWhen(true)] out (int, string)? result)
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

    private static short ReadInt16(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<short>(data);

    private static int ReadInt32(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<int>(data);

    private static long ReadInt64(ReadOnlySpan<byte> data)
        => MemoryMarshal.Read<long>(data);

    private static Guid ReadGuid(ReadOnlySpan<byte> bytes, ref int cursor)
        => MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]);

    public static string ReadUnicode(ReadOnlySpan<byte> bytes, ref int cursor)
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
