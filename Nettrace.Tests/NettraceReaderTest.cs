using Microsoft.Diagnostics.Tracing;

namespace Nettrace.Tests;

public sealed class NettraceReaderTest
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

    [Fact]
    public void TestPaylaodLength()
    {
        List<int> exp = [];
        UsingEventPipe(onEvent: @event => exp.Add(@event.EventData().Length));
        NettraceReader.NettraceFile? nettraceFile = NettraceReader.Read(File.OpenRead(filePath));
        var actualSequenceNumbers = nettraceFile.EventBlocks.SelectMany(blob => blob.EventBlobs).Select(blob => blob.PayloadSize).ToArray();

        Assert.Equal(exp, actualSequenceNumbers);
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