using System.Diagnostics;
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
    await Task.Delay(TimeSpan.FromMilliseconds(100));
}

static async Task LongRunningTask()
{
    await Task.Delay(TimeSpan.FromSeconds(1));
}

static async Task SyncTask()
{
    var stopwatch = Stopwatch.StartNew();
    SpinWait.SpinUntil(() => stopwatch.Elapsed < TimeSpan.FromMilliseconds(300));
    await Task.Delay(TimeSpan.FromMilliseconds(300));
}
