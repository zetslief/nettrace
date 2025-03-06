using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using Nettrace;
using Ipc;

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

var (maybeError, maybeSessionId) = await DiagnosticIpc.TryCollectTracing(socket, providers);
if (maybeError.HasValue) throw new InvalidOperationException($"Failed to start tracing: {maybeError.Value}");
Debug.Assert(maybeSessionId is not null);
var sessionId = maybeSessionId.Value;
Console.WriteLine($"Session Id: {sessionId}");

Memory<byte> buffer = new byte[1024 * 1024 * 32];
BufferContext bufferCtx = new(0, 0);
ParsingContext parsingCtx = new(0, null, State.Magic);
bool needMoreMemory = true;

var sessionStopwatch = Stopwatch.StartNew();

while (true)
{
    if (sessionStopwatch.Elapsed > TimeSpan.FromMinutes(1))
    {
        var requestSuccess = await DiagnosticIpc.TryRequestStopTracing(stopSocket, sessionId);
        Debug.Assert(requestSuccess);
        break;
    }

    if (needMoreMemory)
    {
        (var totalRead, bufferCtx, buffer) = await ReadDataFromSocket(socket, bufferCtx, buffer);
        WriteBufferContextInfo(in bufferCtx, buffer, totalRead);
    }

    (needMoreMemory, parsingCtx, bufferCtx) = ParseNettrace(in parsingCtx, in bufferCtx, buffer);
}

(var read, bufferCtx, buffer) = await ReadDataFromSocket(socket, bufferCtx, buffer);
while (read > 0)
{
    WriteBufferContextInfo(in bufferCtx, buffer, read);
    (read, bufferCtx, buffer) = await ReadDataFromSocket(socket, bufferCtx, buffer);
}

Console.WriteLine("Parsing the rest of data...");
while (!needMoreMemory)
{
    WriteBufferContextInfo(in bufferCtx, buffer, 0);
    (needMoreMemory, parsingCtx, bufferCtx) = ParseNettrace(in parsingCtx, in bufferCtx, buffer);
}

WriteBufferContextInfo(in bufferCtx, buffer, 0);

var stoppedSessionId = await DiagnosticIpc.TryWaitStopTracing(stopSocket, sessionId);
Console.WriteLine($"Stopped session id: {stoppedSessionId} | origin id {sessionId}");
Console.WriteLine($"Success: {sessionId == stoppedSessionId}");

static async Task<(int TotalRead, BufferContext BufferCtx, Memory<byte> Buffer)> ReadDataFromSocket(Socket socket, BufferContext bufferCtx, Memory<byte> buffer)
{
    var requestStopwatch = new Stopwatch();
    long timeToRead = 0;
    int totalRead = 0;
    var (bufferCursor, bufferEnd) = bufferCtx;
    for (int attempt = 0; attempt < 100 && timeToRead < 100; ++attempt)
    {
        requestStopwatch.Start();
        var read = await socket.ReceiveAsync(buffer[bufferEnd..]);
        if (read == 0)
            break; // TODO: (this break is not safe, refactor it. there might be some edge case.)
        requestStopwatch.Stop();
        timeToRead = requestStopwatch.ElapsedMilliseconds;
        bufferEnd += read;
        totalRead += read;
        if (bufferEnd == buffer.Length)
        {
            var newBuffer = new byte[buffer.Length];
            buffer[bufferCursor..].CopyTo(newBuffer);
            bufferEnd -= bufferCursor;
            bufferCursor = 0;
            buffer = newBuffer;
        }

        requestStopwatch.Reset();
    }

    return (totalRead, new(bufferCursor, bufferEnd), buffer);
}

static void WriteBufferContextInfo(in BufferContext ctx, ReadOnlyMemory<byte> buffer, int read)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Parsing - Receive {read} bytes.");
    Console.WriteLine($"Buffer Length - {buffer.Length} ({buffer.Length / 1e6d} Mb)");
    var spaceTaken = ctx.BufferEnd - ctx.BufferCursor;
    Console.WriteLine($"Buffer Cursor - {ctx.BufferCursor} | Buffer End  - {ctx.BufferEnd} | Space Taken: {spaceTaken} ({(spaceTaken / (float)buffer.Length) * 100:F2}%)");
    Console.ResetColor();
}

static (bool NeedMoreMemory, ParsingContext ParsinGCtx, BufferContext bufferCtx) ParseNettrace(
    in ParsingContext ctx,
    in BufferContext bufferContext,
    ReadOnlyMemory<byte> buffer)
{
    var (globalCursor, currentObject, state) = ctx;
    var (bufferCursor, bufferEnd) = bufferContext;
    var bufferCursorStart = bufferCursor;
    var needMoreMemory = false;

    var span = buffer.Span[bufferCursor..bufferEnd];

    switch (ctx.State)
    {
        case State.Magic:
            var magic = Encoding.UTF8.GetString(buffer[..8].Span);
            MoveBy(ref bufferCursor, 8);
            Console.WriteLine($"Magic: {magic}");
            state = State.StreamHeader;
            break;
        case State.StreamHeader:
            if (!NettraceReader.TryReadStreamHeader(span, out var maybeStreamHeader))
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
            if (!NettraceReader.TryStartObject(span, out var maybeNewObject))
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
                    if (!NettraceReader.TryReadTrace(span, out var maybeTrace))
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
                    if (!NettraceReader.TryReadRawBlock(span, currentObject, globalCursor, out var maybeRawBlock))
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
            if (!NettraceReader.TryFinishObject(span, out var finishObjectLength))
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

    if (needMoreMemory)
        return (true, ctx, bufferContext);

    var bufferCursorMoved = bufferCursor - bufferCursorStart;
    globalCursor += bufferCursorMoved;
    return (needMoreMemory, new(globalCursor, currentObject, state), new(bufferCursor, bufferEnd));
}

static int MoveBy(ref int cursor, int value)
{
    cursor += value;
    return cursor;
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