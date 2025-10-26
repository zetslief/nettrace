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

    public static AwaitTaskContinuationScheduled ParseAwaitTaskContinuationScheduled(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
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

public static class RuntimeRundownEvents
{
    public static RuntimeInformationRundown ParseRuntimeInformationRundown(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<byte>(bytes[cursor..MoveBy(ref cursor, sizeof(byte))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]),
        NettraceReader.ReadUnicode(bytes, ref cursor)
    );

    public static DCEndInit ParseDCEndInit(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ushort>(bytes)
    );

    public static MethodDCEndVerbose ParseMethodDCEndVerbose(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        0 // WARNING: this is a new field in V2 message. It is missing in V1 message.
    );

    public static MethodDCEndILToNativeMap ParseMethodDCEndILToNativeMap(ReadOnlySpan<byte> bytes, int cursor = 0)
    {
        ulong methodId = MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]);
        ulong reJitId = MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]);
        byte methodExtent = MemoryMarshal.Read<byte>(bytes[cursor..MoveBy(ref cursor, sizeof(byte))]);
        ushort countOfMapEntries = MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]);
        uint[] ilOffsets = new uint[countOfMapEntries];
        for (int index = 0; index < ilOffsets.Length; ++index)
            ilOffsets[index] = MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]);
        uint[] nativeOffsets = new uint[countOfMapEntries];
        for (int index = 0; index < nativeOffsets.Length; ++index)
            nativeOffsets[index] = MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]);
        ushort clrInstanceId = MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]);
        ulong ilVersionId = MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]);
        return new(methodId, reJitId, methodExtent, countOfMapEntries, ilOffsets, nativeOffsets, clrInstanceId, ilVersionId);
    }

    public static DomainModuleDCEnd ParseDomainModuleDCEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))])
    );

    public static ModuleDCEnd ParseModuleDCEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))]),
        MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<Guid>(bytes[cursor..MoveBy(ref cursor, 16)]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        string.Empty // WARNING: this is a new field in V3 message. NativeBuildID
    );

    public static AssemblyDCEnd ParseAssemblyDCEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))])
    );

    public static AppDomainDCEnd ParseAppDomainDCEnd(ReadOnlySpan<byte> bytes, int cursor = 0) => new(
        MemoryMarshal.Read<ulong>(bytes[cursor..MoveBy(ref cursor, sizeof(ulong))]),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        NettraceReader.ReadUnicode(bytes, ref cursor),
        MemoryMarshal.Read<uint>(bytes[cursor..MoveBy(ref cursor, sizeof(uint))]),
        MemoryMarshal.Read<ushort>(bytes[cursor..MoveBy(ref cursor, sizeof(ushort))])
    );

    private static int MoveBy(ref int value, int by)
    {
        value += by;
        return value;
    }
}
