using System.Runtime.InteropServices;

namespace Nettrace.PayloadParsers;

public static class TplParser
{
    public static TaskWaitBegin ParseTaskWaitBegin(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static AwaitTaskContinuationScheduled ParseAwaitTaskContinuation(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TaskScheduled ParseTaskScheduled(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TraceOperationRelation ParseTraceOperationRelation(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static NewId ParseNewId(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TraceSynchronousWorkBegin ParseTraceSynchronousWorkBegin(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TraceSynchronousWorkEnd ParseTraceSynchronousWorkEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TraceOperationEnd ParseTraceOperationEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TaskWaitContinuationStarted ParseTaskWaitContinuationStarted(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TraceOperationBegin ParseTraceOperationBegin(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(long))])
    );

    public static TaskWaitContinuationComplete ParseTaskWaitContinuationComplete(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    public static TaskWaitEnd ParseTaskWaitEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))]),
        MemoryMarshal.Read<int>(bytes[cursor..MoveBy(ref cursor, sizeof(int))])
    );

    private static int MoveBy(ref int value, int by)
    {
        value += by;
        return value;
    }
}

public static class ProcessInfoParser
{
    public static ProcessInfo ParseProcessInfo(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor)
    );
}
