using Nettrace;
using Microsoft.Diagnostics.Tracing;

var filePath = args[0];

var nettraceFile = NettraceReader.Read(File.OpenRead(filePath));
var eventCount = UseEventPipe(filePath);

var nettraceEventCount = nettraceFile.EventBlocks.Sum((blob) => blob.EventBlobs.Length);

Console.WriteLine($"Nettrace event count: {nettraceEventCount}");
Console.WriteLine($"EventPipe event count: {eventCount}");

static int UseEventPipe(string filePath)
{
    using var eventSource = new EventPipeEventSource(filePath);
    int eventCount = 0; 
    eventSource.AllEvents += (@event) =>
    {
        ++eventCount;
    };
    eventSource.Process();
    return eventCount;
}