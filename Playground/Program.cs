using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using Nettrace;

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
    new("ProfileMe", ulong.MaxValue, 0, string.Empty),
];

var buffer = TryCollectTracingCommand(providers)
    ?? throw new InvalidOperationException("Failed to create buffer for CollectTracing command.");

var sent = await socket.SendAsync(buffer);
if (sent != buffer.Length) throw new InvalidOperationException($"Failed to send CollectTracing command. Sent only {sent} bytes.");

Console.WriteLine($"Command CollectTracing: sent {sent}.");

var responseMemory = new byte[HEADER_SIZE + sizeof(ulong)];
var responseLength = await socket.ReceiveAsync(responseMemory);
Console.WriteLine($"Read {responseLength} data.");
var maybeError = TryReadCollectTracingResponse(responseMemory.AsSpan(0, responseLength), out var sessionId);
if (maybeError.HasValue) throw new InvalidOperationException($"Failed to get collect tracing response: {maybeError}");
Console.WriteLine($"Session Id: {sessionId}");

var state = State.ReadingNewObject;
NettraceReader.Type? currentObject = null;

while (true)
{
    var nettrace = new byte[1024 * 4];
    var read = await socket.ReceiveAsync(nettrace);
    Console.WriteLine($"Receive {read} bytes");
    Console.WriteLine(Encoding.UTF8.GetString(nettrace.AsSpan(..read)));

    using var stream = new MemoryStream(nettrace[8..read]);
    switch (state)
    {
        default:
            throw new NotImplementedException($"{state} is not implemented");
    }
}

static ReadOnlyMemory<byte>? TryCollectTracingCommand(IReadOnlyCollection<Provider> providers)
{
    static int? WriteProvider(Span<byte> buffer, Provider provider)
    {
        var cursor = 0;
        BinaryPrimitives.TryWriteUInt64LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(ulong))], provider.Keywords);
        BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], provider.LogLevel);
        var providerName = Encoding.Unicode.GetBytes($"{provider.Name}\0").AsSpan();
        BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], (uint)provider.Name.Length + 1);
        providerName.CopyTo(buffer[cursor..MoveBy(ref cursor, providerName.Length)]);
        return cursor;
    }

    Memory<byte> data = new byte[1024];
    var buffer = data.Span;

    var magic = "DOTNET_IPC_V1"u8.ToArray();
    magic.CopyTo(buffer);

    byte eventPipeCommandSet = 0x02;
    byte collectTracingCommandId = 0x02;
    int eventPipeCommandSetIndex = 16;
    int collectTracingCommandIdIndex = 17;

    buffer[eventPipeCommandSetIndex] = eventPipeCommandSet;
    buffer[collectTracingCommandIdIndex] = collectTracingCommandId;

    var cursor = 20;

    uint circularBufferMb = 1;
    if (!BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], circularBufferMb))
        return null;

    uint format = 1; // NETTRACE
    if (!BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], format))
        return null;

    var providersCount = (uint)providers.Count;
    if (!BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], providersCount))
        return null;

    foreach (var provider in providers)
    {
        var providerLength = WriteProvider(buffer[cursor..], provider);
        if (providerLength.HasValue)
            MoveBy(ref cursor, providerLength.Value);
        else
            return null;
    }

    var sizeIndex = 14;
    if (!BinaryPrimitives.TryWriteUInt16LittleEndian(buffer[sizeIndex..WithOffset(sizeIndex, 2)], (ushort)cursor))
        return null;

    return data;
}

static IpcError? TryReadCollectTracingResponse(ReadOnlySpan<byte> data, out ulong? sessionId)
{
    switch (data.Length)
    {
        case HEADER_SIZE + sizeof(uint):
            sessionId = uint.MaxValue;
            return (IpcError)BitConverter.ToUInt32(data[HEADER_SIZE..WithOffset(HEADER_SIZE, sizeof(uint))]);
        case HEADER_SIZE + sizeof(ulong):
            sessionId = BitConverter.ToUInt64(data[HEADER_SIZE..WithOffset(HEADER_SIZE, sizeof(ulong))]);
            return null;
        default:
            sessionId = uint.MaxValue;
            return IpcError.UnknownError;
    }
}

static int MoveBy(ref int cursor, int value)
{
    cursor += value;
    return cursor;
}

static int WithOffset(int cursor, int offset) => cursor + offset;

record Provider(string Name, ulong Keywords, uint LogLevel, string FilterData);

enum IpcError : uint
{
    BadEncoding = 2148733828,
    UnknownCommand = 2148733829,
    UnknownMagic = 2148733830,
    UnknownError = 2148733831,
}

enum State
{
    ReadingNewObject,
    PayloadParsing,
}