using System.Diagnostics.Tracing;

TplEventListener? listener = null;
if (args.Length > 0)
    listener = new TplEventListener();

try
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        await Task.Yield();
    }
}
finally
{
    listener?.Dispose();
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
        Console.WriteLine($"event: {eventData.EventSource.Name}_{eventData.EventName}");
        if (eventData.PayloadNames is not null)
            Console.WriteLine($"\t Names   : {string.Join(',', eventData.PayloadNames)}");
        if (eventData.Payload is not null)
            Console.WriteLine($"\t Payloads: {string.Join(',', eventData.Payload)}");
    }
}
