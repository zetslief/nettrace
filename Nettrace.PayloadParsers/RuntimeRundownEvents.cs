namespace Nettrace.PayloadParsers;

public static class RuntimeRundownProvider
{
    public const string Name = "Microsoft-Windows-DotNETRuntimeRundown";
}

// NOTE: this event also contains 2 older versions.
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

// NOTE: this event also contains older version.
public sealed record DCEndInit(uint ClrInstanceID) : IEvent
{
    public static int Id => 148;
    public static string Name => nameof(DCEndInit);
}

// NOTE: this event also contains older versions.
public sealed record MethodDCEndILToNativeMap(
    ulong MethodID,
    ulong ReJITID,
    byte MethodExtent,
    ushort CountOfMapEntries,
    uint[] ILOffsets,
    uint[] NativeOffsets,
    ushort ClrInstanceID,
    ulong ILVersionID
) : IEvent
{
    public static int Id => 150;
    public static string Name => nameof(MethodDCEndILToNativeMap);
}

// NOTE: this event has 2 versions (0, 1).
public sealed record DomainModuleDCEnd(
    ulong ModuleID,
    ulong AssemblyID,
    ulong AppDomainID,
    uint ModuleFlags,
    uint Reserved1,
    string ModuleILPath,
    string ModuleNativePath,
    ushort ClrInstanceID
) : IEvent
{
    public static int Id => 152;
    public static string Name => nameof(DomainModuleDCEnd);
}

// NOTE: this event also contains older versions.
public sealed record ModuleDCEnd(
    ulong ModuleID,
    ulong AssemblyID,
    uint ModuleFlags,
    uint Reserved1,
    string ModuleILPath,
    string ModuleNativePath,
    ushort ClrInstanceID,
    Guid ManagedPdbSignature,
    uint ManagedPdbAge,
    string ManagedPdbBuildPath,
    Guid NativePdbSignature,
    uint NativePdbAge,
    string NativePdbBuildPath,
    string NativeBuildID
) : IEvent
{
    public static int Id => 154;
    public static string Name => nameof(ModuleDCEnd);
}

// NOTE: 2 versions are available (0, 1).
public sealed record AssemblyDCEnd(
    ulong AssemblyID,
    ulong AppDomainID,
    ulong BindingID,
    uint AssemblyFlags,
    string FullyQualifiedAssemblyName,
    ushort ClrInstanceID
) : IEvent
{
    public static int Id => 156;
    public static string Name => nameof(AssemblyDCEnd);
}

// NOTE: 2 versions are available (0, 1).
public sealed record AppDomainDCEnd(
    ulong AppDomainID,
    uint AppDomainFlags,
    string AppDomainName,
    uint AppDomainIndex,
    ushort ClrInstanceID
) : IEvent
{
    public static int Id => 158;
    public static string Name => nameof(AppDomainDCEnd);
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
