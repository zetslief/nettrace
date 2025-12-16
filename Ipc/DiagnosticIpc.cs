using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;

namespace Ipc;

public record Provider(string Name, ulong Keywords, uint LogLevel, string FilterData);

public enum IpcError : uint
{
    BadEncoding = 2148733828,
    UnknownCommand = 2148733829,
    UnknownMagic = 2148733830,
    UnknownError = 2148733831,
}

public static class DiagnosticIpc
{
    private const int HEADER_SIZE = 20;
    private const string DIAGNOSTIC_FILE_PREFIX = "dotnet-diagnostic-";

    public static bool TryGetDiagnosticFileForProcess(int processId, [NotNullWhen(true)] out string? diagnosticFilePath)
    {
        string directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";
        string[] files = Directory.GetFiles(directory, $"{DIAGNOSTIC_FILE_PREFIX}*");
        string processIdString = processId.ToString();
        diagnosticFilePath = files.FirstOrDefault(f => f.Contains(processIdString));
        return !string.IsNullOrEmpty(diagnosticFilePath);
    }

    public static async Task<(IpcError?, ulong? sessionId)> TryCollectTracing(Socket socket, IReadOnlyCollection<Provider> providers)
    {
        var maybeCollectTracingCommandBuffer = TryCollectTracingCommand(providers);
        if (maybeCollectTracingCommandBuffer is null) return (IpcError.UnknownError, null);
        var collectTracingCommandBuffer = maybeCollectTracingCommandBuffer.Value;

        var sent = await socket.SendAsync(collectTracingCommandBuffer);
        if (sent != collectTracingCommandBuffer.Length)
        {
            return (IpcError.UnknownError, null);
        }

        var responseMemory = new byte[HEADER_SIZE + sizeof(ulong)];
        var responseLength = await socket.ReceiveAsync(responseMemory);
        var maybeError = TryReadCollectTracingResponse(responseMemory.AsSpan(0, responseLength), out var sessionId);
        return (maybeError, sessionId);
    }

    public static async Task<bool> TryRequestStopTracing(Socket socket, ulong sessionId)
    {
        var maybeStopTracingCommandBuffer = TryStopTracing(sessionId);
        if (maybeStopTracingCommandBuffer is null) throw new InvalidOperationException("Failed to create stop tracing command buffer");
        var stopTracingWritten = await socket.SendAsync(maybeStopTracingCommandBuffer.Value);
        return stopTracingWritten == maybeStopTracingCommandBuffer.Value.Length;
    }

    public static async Task<ulong?> TryWaitStopTracing(Socket socket, ulong sessionId)
    {
        Memory<byte> stopResultBuffer = new byte[HEADER_SIZE + sizeof(ulong)];
        var stopRead = await socket.ReceiveAsync(stopResultBuffer);
        if (stopRead != stopResultBuffer.Length) return null;
        BinaryPrimitives.TryReadUInt64LittleEndian(stopResultBuffer.Span[^sizeof(ulong)..], out var sessionIdAfterStop);
        return sessionIdAfterStop;
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
}
