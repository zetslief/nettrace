using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

var process = Process.GetProcessesByName("profileMe").Single();

Console.WriteLine($"Process {process.ProcessName} Id: {process.Id} started at {process.StartTime}");
Console.WriteLine($"Start time UNIX: {((DateTimeOffset)process.StartTime).ToUnixTimeSeconds()}");

var directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";

Debug.Assert(Directory.Exists(directory));

Console.WriteLine($"Directory: {directory}");

/*
In order to ensure filename uniqueness, a disambiguation key is generated.
On Mac and NetBSD, this is the process start time encoded as the number of seconds since UNIX epoch time.
If /proc/$PID/stat is available (all other *nix platforms), then the process start time encoded as jiffies since boot time is used. 
 */

var file = Directory.GetFiles(directory, $"dotnet-diagnostic-{process.Id}-*").Single();
Console.WriteLine($"File: {file}");

using CancellationTokenSource cts = new();
using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
var endpoint = new UnixDomainSocketEndPoint(file);

await socket.ConnectAsync(endpoint, cts.Token);

Console.WriteLine($"Connected? {socket.Connected}");

byte[] bytes = new byte[1000];
var len = SetupCollectTracingCommand(bytes, [new("ProfileMe", 0, 0, string.Empty)]);

var written = await socket.SendAsync(bytes[..len]);
Console.WriteLine($"Wrote {written} bytes.");

byte[] response = new byte[20 + 8];
var read = await socket.ReceiveAsync(response);

Console.WriteLine($"Read {read} bytes.");
Console.WriteLine($"Session Id: {ReadSessionId(bytes)}");

static int SetupCollectTracingCommand(Span<byte> buffer, IReadOnlyCollection<Provider> providers)
{
    static int SetupProvider(Span<byte> buffer, Provider provider)
    {
        BitConverter.TryWriteBytes(buffer[0..sizeof(ulong)], provider.Keywords);
        var cursor = sizeof(ulong);
        BitConverter.TryWriteBytes(buffer[cursor..(cursor + sizeof(ulong))], provider.LogLevel);
        cursor += sizeof(ulong);
        var providerName = Encoding.ASCII.GetBytes(provider.Name).AsSpan();
        providerName.CopyTo(buffer[cursor..]);
        cursor += providerName.Length;
        // skip filter data for now.
        return cursor;
    }
    
    var magic = Encoding.ASCII.GetBytes("DOTNET_IPC_V1").AsSpan();
    magic.CopyTo(buffer);
    var size = 20;
    byte eventPipeCommandSet = 0x02;
    byte collectTracingCommandId = 0x02;
    int eventPipeCommandSetIndex = 17;
    int collectTracingCommandIdIndex = 18;
    buffer[eventPipeCommandSetIndex] = eventPipeCommandSet;
    buffer[collectTracingCommandIdIndex] = collectTracingCommandId;
    
    uint circularBufferMb = 1024;
    BitConverter.TryWriteBytes(buffer[size..(size + sizeof(uint))], circularBufferMb);
    size += sizeof(uint);
    
    uint format = 1;
    BitConverter.TryWriteBytes(buffer[size..(size + sizeof(uint))], format);
    size += sizeof(uint);
    
    uint providersCount = (uint)providers.Count;
    BitConverter.TryWriteBytes(buffer[size..(size + sizeof(uint))], providersCount);
    size += sizeof(uint);
    
    foreach (var provider in providers)
        size += SetupProvider(buffer[size..], provider);
    var sizeIndex = 15;
    
    BitConverter.TryWriteBytes(buffer[sizeIndex..(sizeIndex + sizeof(ushort))], size);
    return size;
}

static ulong ReadSessionId(ReadOnlySpan<byte> response)
{
    return BitConverter.ToUInt64(response[20..]);
}

record Provider(string Name, ulong Keywords, ulong LogLevel, string FilterData);