using System.Diagnostics.Tracing;

int index = 0;

while (true)
{
    var time = DateTime.Now;
    ProfileMe.Log.Tick(time);
    Console.WriteLine($"{index++} : {time}");
    await Task.Delay(TimeSpan.FromSeconds(1));
}

[EventSource(Name = "ProfileMe")]
public class ProfileMe : EventSource
{
    public static ProfileMe Log { get; } = new();

    [Event(1)]
    public void Tick(DateTime time) => WriteEvent(1, time);
}
