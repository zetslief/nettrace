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
    new("Microsoft-Windows-DotNETRuntime", ulong.MaxValue, 0, string.Empty),
    new("Microsoft-DotNETCore-SampleProfiler", ulong.MaxValue, 0, string.Empty),
    new("System.Threading.Tasks.TplEventSource", ulong.MaxValue, 0, string.Empty),
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

var state = State.Magic;
NettraceReader.Type? currentObject = null;

Memory<byte> nettrace = new byte[1024 * 8];
int globalCursor = 0;
int bufferEnd = 0;
bool needMoreMemory = true;

var stopwatch = new Stopwatch();

while (true)
{
    if (needMoreMemory)
    {
        long timeToRead = 0;
        for (int attempt = 0; attempt < 100 && timeToRead < 100; ++attempt)
        {
            stopwatch.Start();
            var read = await socket.ReceiveAsync(nettrace[bufferEnd..]);
            stopwatch.Stop();
            timeToRead = stopwatch.ElapsedMilliseconds;
            bufferEnd += read;
            if (bufferEnd == nettrace.Length)
            {
                var newNettrace = new byte[nettrace.Length * 2];
                nettrace.CopyTo(newNettrace);
                nettrace = newNettrace;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Parsing - Receive {read} bytes. Time to read - {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Buffer Length - {nettrace.Length} ({nettrace.Length / 1e6d} Mb)");
            Console.WriteLine($"Global Cursor - {globalCursor} | Buffer End  - {bufferEnd} | Space Taken: {bufferEnd - globalCursor} ({(bufferEnd / (float)nettrace.Length) * 100:F2}%)");
            Console.ResetColor();
            stopwatch.Reset();
        }

        needMoreMemory = false;
    }

    switch (state)
    {
        case State.Magic:
            var magic = Encoding.UTF8.GetString(nettrace[..8].Span);
            MoveBy(ref globalCursor, 8); 
            Console.WriteLine($"Magic: {magic}");
            state = State.StreamHeader;
            break;
        case State.StreamHeader:
            if (!NettraceReader.TryReadStreamHeader(nettrace.Span[globalCursor..bufferEnd], out var maybeStreamHeader))
            {
                needMoreMemory = true;
                break;
            }
            var (streamHeaderLength, streamHeader) = maybeStreamHeader.Value;
            Console.WriteLine($"Stream Header: {streamHeader}");
            globalCursor += streamHeaderLength;
            state = State.StartObject;
            break;
        case State.StartObject:
            if (!NettraceReader.TryStartObject(nettrace.Span[globalCursor..bufferEnd], out var maybeNewObject))
            {
                needMoreMemory = true;
                break;
            }
            var (newObjectLength, newObject) = maybeNewObject.Value;
            Console.WriteLine($"New object: {newObject}");
            globalCursor += newObjectLength;
            currentObject = newObject;
            state = State.NewObject;
            break;
        case State.NewObject:
            Debug.Assert(currentObject is not null);
            switch (currentObject.Name)
            {
                case "Trace":
                    if (!NettraceReader.TryReadTrace(nettrace.Span[globalCursor..bufferEnd], out var maybeTrace))
                    {
                        needMoreMemory = true;
                        break;
                    }
                    
                    var (traceLength, trace) = maybeTrace.Value;
                    Console.WriteLine($"Trace: {trace}");
                    globalCursor += traceLength;
                    state = State.FinishObject;
                    break;
                case var blockish when blockish.EndsWith("Block"):
                    if (!NettraceReader.TryReadRawBlock(nettrace.Span[globalCursor..bufferEnd], currentObject, globalCursor, out var maybeRawBlock))
                    {
                        needMoreMemory = true;
                        break;
                    }

                    var (rawBlockLength, rawBlock) = maybeRawBlock.Value;
                    Console.WriteLine($"\tRaw Block: {rawBlock}");
                    globalCursor += rawBlockLength;
                    state = State.FinishObject;
                    break;
                default:
                    throw new NotImplementedException($"Reading {currentObject.Name} - {currentObject.Version} is not implemented.");
            }
            break;
        case State.FinishObject:
            if (!NettraceReader.TryFinishObject(nettrace.Span[globalCursor..bufferEnd], out var finishObjectLength))
            {
                needMoreMemory = true;
                break;
            }
            
            globalCursor += finishObjectLength.Value;
            Console.WriteLine($"Finish current object: {currentObject}.");
            currentObject = null;
            state = State.StartObject;
            break;
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
        BinaryPrimitives.TryWriteUInt32LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(uint))], 0);
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

    uint circularBufferMb = 1000;
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
    Magic,
    StreamHeader,
    StartObject,
    NewObject,
    FinishObject,
}