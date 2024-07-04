using Nettrace;
using Microsoft.Diagnostics.Tracing;

var filePath = args[0];

const bool useNettrace = false;
if (useNettrace)
{
    UseNettrace(filePath);
}
else
{
    UseEventPipe(filePath);
}

static void UseNettrace(string filePath)
{
    using var file = File.OpenRead(filePath);
    NettraceReader.Read(file);
}

static void UseEventPipe(string filePath)
{
    using var eventSource = new EventPipeEventSource(filePath);
    int eventCount = 0; 
    eventSource.AllEvents += (@event) =>
    {
        ++eventCount;
        Console.WriteLine($"{eventCount}: {@event}"); 
    };
    eventSource.Process();
    Console.WriteLine($"Total number of events: {eventCount}");
}