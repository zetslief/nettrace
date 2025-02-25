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
    for (int i = 0; i < count; ++i)
    {
        await Task.Run(() => ProfileMe.Log.Tick(DateTime.Now));
    }
}

[EventSource(Name = "ProfileMe")]
public class ProfileMe : EventSource
{
    public static ProfileMe Log { get; } = new();

    [Event(1)]
    public void Tick(DateTime time) => WriteEvent(1, time);
}
