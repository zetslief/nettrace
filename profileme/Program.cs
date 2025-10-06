using System.Diagnostics;
using System.Diagnostics.Tracing;

var delay = TimeSpan.FromMilliseconds(10);
int index = 0;

while (true)
{
    var time = DateTime.Now;
    ProfileMe.Log.Tick(time);
    Console.WriteLine($"{index++} : {time}");
    await Work(10000);
    await Task.Delay(delay).ConfigureAwait(false);
}

static async Task Work(int count)
{
    using CancellationTokenSource cts = new();
    for (int i = 0; i < count; ++i)
    {
        await Task.Run(async () => { await Task.Yield(); ProfileMe.Log.Tick(DateTime.Now); });
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(100), cts.Token);
            var background = Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(200000)));
            await Task.Delay(TimeSpan.FromSeconds(0.01));
            cts.Cancel();
            await delay;
            await background;
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine($"{stopwatch.Elapsed} Exception: {e}");
        }

    }
}

[EventSource(Name = "ProfileMe")]
public class ProfileMe : EventSource
{
    public static ProfileMe Log { get; } = new();

    [Event(1)]
    public void Tick(DateTime time) => WriteEvent(1, time);
}
