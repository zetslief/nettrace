using System.Diagnostics.Tracing;

// using var listener = new TplEventListener();

while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(5));
    await Task.Yield();
}

public sealed class TplEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        Console.WriteLine($"event: {eventData.EventSource.Name}_{eventData.EventName} {eventData}");
        if (eventData.PayloadNames is not null)
        {
            Console.WriteLine($"\t {string.Join(',', eventData.PayloadNames)}");
            Console.WriteLine($"\t {string.Join(',', eventData.Payload)}");
        }
    }
}
