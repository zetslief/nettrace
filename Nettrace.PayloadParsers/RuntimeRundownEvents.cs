namespace Nettrace.PayloadParsers;

public static class RuntimeRundownProvider
{
    public const string Name = "Microsoft-Windows-DotNETRuntimeRundown";
}

/*
Event 187 RuntimeInformationRundown
*/
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
    public static string Name => nameof(RuntimeInformationRundown);
}
