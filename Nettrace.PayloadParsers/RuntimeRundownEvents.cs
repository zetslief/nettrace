namespace Nettrace.PayloadParsers;

public static class RuntimeRundownProvider
{
    public const string Name = "Microsoft-Windows-DotNETRuntimeRundown";
}

// this event also contains 2 older versions.
public sealed record MethodDCEndVerbose(
    ulong MethodID,
    ulong ModuleID,
    ulong MethodStartAddress,
    uint MethodSize,
    uint MethodToken,
    uint MethodFlags,
    string MethodNamespace,
    string MethodName,
    string MethodSignature,
    ushort ClrInstanceID,
    ulong ReJITID
) : IEvent
{
    public static int Id => 144;
    public static string Name => nameof(MethodDCEndVerbose);
}

// this event also contains older version.
public sealed record DCEndInit(uint ClrInstanceID) : IEvent
{
    public static int Id => 148;
    public static string Name => nameof(DCEndInit);
}

public sealed record MethodDCEndILToNativeMap(
    ulong MethodID,
    ulong ReJITID,
    byte MethodExtent,
    ushort CountOfMapEntries,
    ushort ClrInstanceID,
    ulong ILVersionID
) : IEvent
{
    public static int Id => 150;
    public static string Name => nameof(MethodDCEndILToNativeMap);
}

public sealed record RuntimeInformationRundown(
    ushort ClrInstanceID,
    ushort Sku,
    ushort BclMajorVersion,
    ushort BclMinorVersion,
    ushort BclBuildNumber,
    ushort BclQfeNumber,
    ushort VMMajorVersion,
    ushort VMMinorVersion,
    ushort VMBuildNumber,
    ushort VMQfeNumber,
    uint StartupFlags,
    byte StartupMode,
    string CommandLine,
    Guid ComObjectGuid,
    string RuntimeDllPath
) : IEvent
{
    public static int Id => 187;
    public static string Name => nameof(RuntimeInformationRundown);
}
