using Nettrace;
using Microsoft.Diagnostics.Tracing;
using static Nettrace.NettraceReader;

var filePath = args[0];

NettraceReader.NettraceFile? nettraceFile = NettraceReader.Read(File.OpenRead(filePath));
var eventCount = UseEventPipe(filePath);

var nettraceEventCount = nettraceFile.EventBlocks.Sum((blob) => blob.EventBlobs.Length);

Console.WriteLine($"Nettrace event count: {nettraceEventCount}");
Console.WriteLine($"EventPipe event count: {eventCount}");

static int UseEventPipe(string filePath)
{
    using var eventSource = new EventPipeEventSource(filePath);
    var versions = new HashSet<int>();
    int eventCount = 0; 
    eventSource.AllEvents += (@event) =>
    {
        ++eventCount;
    };
    eventSource.Process();
    Console.WriteLine($"Versions of the events: {string.Join(',', versions)}");
    return eventCount;
}