using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

const int HEADER_SIZE = 20;

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

IReadOnlyCollection<Provider> providers =
[
    new("ProfileMe", 0, 0, string.Empty),
];

bool sent = await TryCollectTracingCommand(socket.SendAsync, providers);
if (!sent) throw new InvalidOperationException("Failed to send CollectTracing command.");

Console.WriteLine("Command CollectTracing: sent.");

var (error, sessionId) = await ReadCollectTracingResponse(socket.ReceiveAsync);
if (error.HasValue) throw new InvalidOperationException($"Failed to get collect tracing response: {error}");
Console.WriteLine($"Session Id: {sessionId}");

while (true)
{
    var nettrace = new byte[4096];
    var read = await socket.ReceiveAsync(nettrace);
    Console.WriteLine($"Receive {read} bytes");
}

static async Task<bool> TryCollectTracingCommand(Func<ArraySegment<byte>, Task<int>> send,
    IReadOnlyCollection<Provider> providers)
{
    static int? WriteProvider(Span<byte> buffer, Provider provider)
    {
        var cursor = 0;
        BitConverter.TryWriteBytes(buffer[cursor..MoveBy(ref cursor, sizeof(ulong))], provider.Keywords);
        BitConverter.TryWriteBytes(buffer[cursor..MoveBy(ref cursor, sizeof(ulong))], provider.LogLevel);
        var providerName = Encoding.ASCII.GetBytes(provider.Name).AsSpan();
        providerName.CopyTo(buffer[cursor..MoveBy(ref cursor, providerName.Length)]);
        // skip filter data for now.
        return cursor;
    }

    var buffer = new byte[1024];

    var magic = "DOTNET_IPC_V1"u8.ToArray();
    magic.CopyTo(buffer.AsSpan());

    byte eventPipeCommandSet = 0x02;
    byte collectTracingCommandId = 0x02;
    int eventPipeCommandSetIndex = 17;
    int collectTracingCommandIdIndex = 18;
    buffer[eventPipeCommandSetIndex] = eventPipeCommandSet;
    buffer[collectTracingCommandIdIndex] = collectTracingCommandId;

    var cursor = 20;

    uint circularBufferMb = 1024;
    if (!BitConverter.TryWriteBytes(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], circularBufferMb))
        return false;
    
    uint format = 1; // NETTRACE
    if (!BitConverter.TryWriteBytes(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], format))
        return false;

    uint providersCount = (uint)providers.Count;
    if (!BitConverter.TryWriteBytes(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], providersCount))
        return false;

    foreach (var provider in providers)
    {
        var providerLength = WriteProvider(buffer[cursor..], provider);
        if (providerLength.HasValue)
            MoveBy(ref cursor, providerLength.Value);
        else
            return false;
    }

    var sizeIndex = 15;

    if (!BitConverter.TryWriteBytes(buffer[sizeIndex..WithOffset(sizeIndex, sizeof(ushort))], (ushort)cursor))
        return false;

    var sent = await send(buffer[..cursor]).ConfigureAwait(false);
    return sent == cursor;
}

static async Task<(IpcError? Error, ulong)> ReadCollectTracingResponse(Func<ArraySegment<byte>, Task<int>> receive)
{
    var buffer = new byte[HEADER_SIZE + sizeof(ulong)];
    int bytesRead = await receive(buffer).ConfigureAwait(false);
    Console.WriteLine(bytesRead);
    if (bytesRead < buffer.Length)
    {
        if (bytesRead == (HEADER_SIZE + sizeof(uint)))
        {
            return ((IpcError)BitConverter.ToUInt32(buffer.AsSpan(HEADER_SIZE, sizeof(uint))), 0);
        }
        return (IpcError.UnknownError, 0);
    }
    return (null, BitConverter.ToUInt64(buffer.AsSpan(HEADER_SIZE, sizeof(ulong))));
}

static int MoveBy(ref int cursor, int value)
{
    cursor += value;
    return cursor;
}

static int WithOffset(int cursor, int offset) => cursor + offset;

record Provider(string Name, ulong Keywords, ulong LogLevel, string FilterData);

enum IpcError : uint
{
    BadEncoding = 2148733828,
    UnknownCommand = 2148733829,
    UnknownMagic = 2148733830,
    UnknownError   = 2148733831,
}
