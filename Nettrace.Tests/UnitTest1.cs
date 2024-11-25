using Microsoft.Diagnostics.Tracing;

namespace Nettrace.Tests;

public class NettraceReaderTest
{
    const string filePath = "perf.nettrace";

    [Fact]
    public void TestEventCount()
    {
        int expectedCount = 0;
        UsingEventPipe(onEvent: _ => ++expectedCount);
        NettraceReader.NettraceFile? nettraceFile = NettraceReader.Read(File.OpenRead(filePath));
        var actualCount = nettraceFile.EventBlocks.Sum(blob => blob.EventBlobs.Length);
        Assert.Equal(expectedCount, actualCount);
    }

    private static void UsingEventPipe(Action<TraceEvent>? onEvent = null, Action<TraceEvent>? onUnhandledEvent = null)
    {
        static void Ignore(TraceEvent @event) {}

        using var eventSource = new EventPipeEventSource(filePath);
        eventSource.AllEvents += onEvent ?? Ignore;
        eventSource.UnhandledEvents += onUnhandledEvent ?? Ignore;
        eventSource.Process();
    }
}
/*
using Nettrace;
using Microsoft.Diagnostics.Tracing;
using static Nettrace.NettraceReader;

var filePath = args[0];

NettraceReader.NettraceFile? nettraceFile = NettraceReader.Read(File.OpenRead(filePath));
var eventCount = UseEventPipe(filePath);

var nettraceEventCount = nettraceFile.EventBlocks.Sum((blob) => blob.EventBlobs.Length);

Console.WriteLine($"Nettrace event count: {nettraceEventCount}");
Console.WriteLine($"EventPipe event count: {eventCount}");

static int UseEventPipe(string filePath)
{
    using var eventSource = new EventPipeEventSource(filePath);
    var versions = new HashSet<int>();
    int eventCount = 0; 
    eventSource.AllEvents += (@event) =>
    {
        ++eventCount;
    };
    eventSource.Process();
    Console.WriteLine($"Versions of the events: {string.Join(',', versions)}");
    return eventCount;
}
*/