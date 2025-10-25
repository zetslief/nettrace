namespace Nettrace.PayloadParsers;

public static class EventPipeProvider
{
    public const string Name = "Microsoft-DotNETCore-EventPipe";
}

public sealed record ProcessInfo(string CommandLine, string OsInformation, string ArchInformation) : IEvent
{
    public static int Id => 1;
    public static string Name => nameof(ProcessInfo);
}
