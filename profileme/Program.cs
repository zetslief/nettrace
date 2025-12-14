using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

Console.WriteLine($"Id: {Process.GetCurrentProcess().Id}");

while (true)
{
    await ShortRunningTask();
    await LongRunningTask();
    await SyncTask();
}

static async Task ShortRunningTask()
{
    var guid = TraceSpan.Start(nameof(ShortRunningTask));
    await Task.Delay(TimeSpan.FromMilliseconds(100));
    TraceSpan.Finish(guid);
}

static async Task LongRunningTask()
{
    var guid = TraceSpan.Start(nameof(LongRunningTask));
    await Task.Delay(TimeSpan.FromSeconds(1));
    TraceSpan.Finish(guid);
}

static async Task SyncTask()
{
    var guid = TraceSpan.Start(nameof(SyncTask));
    var stopwatch = Stopwatch.StartNew();
    SpinWait.SpinUntil(() => stopwatch.Elapsed < TimeSpan.FromMilliseconds(300));
    await Task.Delay(TimeSpan.FromMilliseconds(300));
    TraceSpan.Finish(guid);
}

[EventSource(Name = "SimpleSpanEventSource")]
public class TraceSpan : EventSource
{
    public static TraceSpan Log { get; } = new();

    [Event(1)]
    public Guid SpanStart(string operationName)
    {
        Guid id = Guid.NewGuid();
        WriteEvent(1, id, operationName);
        return id;
    }

    [Event(2)]
    public void SpanFinish(Guid id) => WriteEvent(2, id);

    public static Guid? Start(string operationName) => Log.IsEnabled() ?  Log.SpanStart(operationName) : null;

    public static void Finish(Guid? guid)
    {
        if (guid is null) return;
        if (Log.IsEnabled()) Log.SpanFinish(guid.Value);
    }
}
