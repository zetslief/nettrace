using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using Socket stopSocket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
var endpoint = new UnixDomainSocketEndPoint(file);
var endpoint2 = new UnixDomainSocketEndPoint(file);

Console.WriteLine($"Connecting main socket...");
await socket.ConnectAsync(endpoint, cts.Token);

Console.WriteLine($"Connecting stop socket...");
await stopSocket.ConnectAsync(endpoint2, cts.Token);

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
Debug.Assert(sessionId is not null);
Console.WriteLine($"Session Id: {sessionId}");

Memory<byte> nettrace = new byte[1024 * 1024 * 32];
BufferContext bufferCtx = new(0, 0);
ParsingContext parsingCtx = new(0, null, State.Magic);
bool needMoreMemory = true;

var sessionStopwatch = Stopwatch.StartNew();

while (true)
{
    if (sessionStopwatch.Elapsed > TimeSpan.FromSeconds(60))
    {
        var maybeStopTracingCommandBuffer = TryStopTracing(sessionId.Value);
        if (maybeStopTracingCommandBuffer is null) throw new InvalidOperationException("Failed to create stop tracing command buffer"); 
        Console.WriteLine("Sending stop command...");
        var stopTracingWritten = await stopSocket.SendAsync(maybeStopTracingCommandBuffer.Value); 
        Console.WriteLine("Stop command sent!");
        Debug.Assert(stopTracingWritten == maybeStopTracingCommandBuffer.Value.Length);
        break;
    }
    
    if (needMoreMemory)
    {
        (var totalRead, bufferCtx) = await ReadDataFromSocket(socket, bufferCtx, nettrace);
        WriteBufferContextInfo(in bufferCtx, nettrace, totalRead);
    }
    
    (needMoreMemory, parsingCtx, bufferCtx) = ParseNettrace(in parsingCtx, in bufferCtx, nettrace);
}

(var read, bufferCtx) = await ReadDataFromSocket(socket, bufferCtx, nettrace);
while (read > 0)
{
    WriteBufferContextInfo(in bufferCtx, nettrace, read);
    (read, bufferCtx) = await ReadDataFromSocket(socket, bufferCtx, nettrace);
}

Console.WriteLine("Parsing the rest of data...");
while (!needMoreMemory)
{
    WriteBufferContextInfo(in bufferCtx, nettrace, 0);
    (needMoreMemory, parsingCtx, bufferCtx) = ParseNettrace(in parsingCtx, in bufferCtx, nettrace);
}

WriteBufferContextInfo(in bufferCtx, nettrace, 0);

Memory<byte> stopResultBuffer = new byte[HEADER_SIZE + sizeof(ulong)];
var stopRead = await stopSocket.ReceiveAsync(stopResultBuffer);
Console.WriteLine($"Read {stopRead} bytes from stop socket.");
BinaryPrimitives.TryReadUInt64LittleEndian(stopResultBuffer.Span[^sizeof(ulong)..], out var sessionIdAfterStop);
Console.WriteLine($"SessionId: {sessionIdAfterStop} {sessionId}");

static async Task<(int TotalRead, BufferContext BufferCtx)> ReadDataFromSocket(Socket socket, BufferContext bufferCtx, Memory<byte> nettrace)
{
    var requestStopwatch = new Stopwatch();
    long timeToRead = 0;
    int totalRead = 0;
    var (bufferCursor, bufferEnd) = bufferCtx;
    for (int attempt = 0; attempt < 100 && timeToRead < 100; ++attempt)
    {
        requestStopwatch.Start();
        var read = await socket.ReceiveAsync(nettrace[bufferEnd..]);
        if (read == 0) 
            break;
        requestStopwatch.Stop();
        timeToRead = requestStopwatch.ElapsedMilliseconds;
        bufferEnd += read;
        totalRead += read;
        if (bufferEnd == nettrace.Length)
        {
            var newNettrace = new byte[nettrace.Length];
            nettrace[bufferCursor..].CopyTo(newNettrace);
            bufferEnd -= bufferCursor; 
            bufferCursor = 0;
            nettrace = newNettrace;
        }

        requestStopwatch.Reset();
    }
    
    return (totalRead, new(bufferCursor, bufferEnd));
}

static void WriteBufferContextInfo(in BufferContext ctx, ReadOnlyMemory<byte> nettrace, int read)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Parsing - Receive {read} bytes.");
    Console.WriteLine($"Buffer Length - {nettrace.Length} ({nettrace.Length / 1e6d} Mb)");
    var spaceTaken = ctx.BufferEnd - ctx.BufferCursor;
    Console.WriteLine($"Buffer Cursor - {ctx.BufferCursor} | Buffer End  - {ctx.BufferEnd} | Space Taken: {spaceTaken} ({(spaceTaken / (float)nettrace.Length) * 100:F2}%)");
    Console.ResetColor();
}

static (bool NeedMoreMemory, ParsingContext ParsinGCtx, BufferContext bufferCtx) ParseNettrace(
    in ParsingContext ctx,
    in BufferContext bufferContext,
    ReadOnlyMemory<byte> nettrace)
{
    var (globalCursor, currentObject, state) = ctx;
    var (bufferCursor, bufferEnd) = bufferContext;
    var bufferCursorStart = bufferCursor;
    var needMoreMemory = false;

    switch (ctx.State)
    {
        case State.Magic:
            var magic = Encoding.UTF8.GetString(nettrace[..8].Span);
            MoveBy(ref bufferCursor, 8); 
            Console.WriteLine($"Magic: {magic}");
            state = State.StreamHeader;
            break;
        case State.StreamHeader:
            if (!NettraceReader.TryReadStreamHeader(nettrace.Span[bufferCursor..bufferEnd], out var maybeStreamHeader))
            {
                needMoreMemory = true;
                break;
            }
            var (streamHeaderLength, streamHeader) = maybeStreamHeader.Value;
            Console.WriteLine($"Stream Header: {streamHeader}");
            bufferCursor += streamHeaderLength;
            state = State.StartObject;
            break;
        case State.StartObject:
            if (!NettraceReader.TryStartObject(nettrace.Span[bufferCursor..bufferEnd], out var maybeNewObject))
            {
                needMoreMemory = true;
                break;
            }
            var (newObjectLength, newObject) = maybeNewObject.Value;
            Console.WriteLine($"New object: {newObject}");
            bufferCursor += newObjectLength;
            currentObject = newObject;
            state = State.NewObject;
            break;
        case State.NewObject:
            Debug.Assert(currentObject is not null);
            switch (currentObject.Name)
            {
                case "Trace":
                    if (!NettraceReader.TryReadTrace(nettrace.Span[bufferCursor..bufferEnd], out var maybeTrace))
                    {
                        needMoreMemory = true;
                        break;
                    }
                    
                    var (traceLength, trace) = maybeTrace.Value;
                    Console.WriteLine($"Trace: {trace}");
                    bufferCursor += traceLength;
                    state = State.FinishObject;
                    break;
                case var blockish when blockish.EndsWith("Block"):
                    if (!NettraceReader.TryReadRawBlock(nettrace.Span[bufferCursor..bufferEnd], currentObject, globalCursor, out var maybeRawBlock))
                    {
                        needMoreMemory = true;
                        break;
                    }

                    var (rawBlockLength, rawBlock) = maybeRawBlock.Value;
                    Console.WriteLine($"\tRaw Block: {rawBlock}");
                    bufferCursor += rawBlockLength;
                    state = State.FinishObject;
                    break;
                default:
                    throw new NotImplementedException($"Reading {currentObject.Name} - {currentObject.Version} is not implemented.");
            }
            break;
        case State.FinishObject:
            if (!NettraceReader.TryFinishObject(nettrace.Span[bufferCursor..bufferEnd], out var finishObjectLength))
            {
                needMoreMemory = true;
                break;
            }
            
            bufferCursor += finishObjectLength.Value;
            Console.WriteLine($"Finish current object: {currentObject}.");
            currentObject = null;
            state = State.StartObject;
            break;
        default:
            throw new NotImplementedException($"{state} is not implemented");
    }

    if (needMoreMemory) return (true, ctx, bufferContext);

    var bufferCursorMoved = bufferCursor - bufferCursorStart; 
    globalCursor += bufferCursorMoved;
    return (needMoreMemory, new(globalCursor, currentObject, state), new(bufferCursor, bufferEnd));
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

    uint circularBufferMb = 128;
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

    return data[..cursor];
}

static IpcError? TryReadCollectTracingResponse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ulong? sessionId)
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

static ReadOnlyMemory<byte>? TryStopTracing(ulong sessionId)
{
    Memory<byte> data = new byte[HEADER_SIZE + sizeof(ulong)];
    var buffer = data.Span;

    var magic = "DOTNET_IPC_V1"u8.ToArray();
    magic.CopyTo(buffer);

    byte eventPipeCommandSet = 0x02;
    byte collectTracingCommandId = 0x01;
    int eventPipeCommandSetIndex = 16;
    int collectTracingCommandIdIndex = 17;

    buffer[eventPipeCommandSetIndex] = eventPipeCommandSet;
    buffer[collectTracingCommandIdIndex] = collectTracingCommandId;

    var cursor = HEADER_SIZE;

    if (!BinaryPrimitives.TryWriteUInt64LittleEndian(buffer[cursor..MoveBy(ref cursor, sizeof(ulong))], sessionId))
        return null;
    
    var sizeIndex = 14;
    if (!BinaryPrimitives.TryWriteUInt16LittleEndian(buffer[sizeIndex..WithOffset(sizeIndex, 2)], (ushort)cursor))
        return null;
    
    return data[..cursor];
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

readonly record struct BufferContext(int BufferCursor, int BufferEnd);

readonly record struct ParsingContext(
    int GlobalCursor,
    NettraceReader.Type? CurrentObject,
    State State
);