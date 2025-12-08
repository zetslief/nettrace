using System.Diagnostics.Tracing;

while (true)
{
    await ShortRunningTask();
    await LongRunningTask();
}

static async Task ShortRunningTask()
{
    await Task.Delay(TimeSpan.FromMilliseconds(100));
}

static async Task LongRunningTask()
{
    await Task.Delay(TimeSpan.FromSeconds(1));
}
