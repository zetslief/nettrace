using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ipc;

namespace Explorer.ViewModels;

public sealed class RecordingService(string filename, IReadOnlyCollection<Provider> providers, ILogger logger) : IDisposable
{
    private readonly string _filename = filename;
    private readonly IReadOnlyCollection<Provider> _providers = providers;
    private readonly ILogger _logger = logger;
    private Socket? _networkSocket = null;

    public ulong? SessionId { get; private set; }

    public async Task<IpcError?> StartAsync()
    {
        var networkEndpoint = new UnixDomainSocketEndPoint(_filename);
        _networkSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await _networkSocket.ConnectAsync(networkEndpoint).ConfigureAwait(false);
        var (maybeError, maybeSessionId) = await DiagnosticIpc.TryCollectTracing(_networkSocket, _providers).ConfigureAwait(false);

        if (maybeError is not null || maybeSessionId is null)
        {
            _logger.LogError("Failed to start collecting trace: {MaybeError}", maybeError);
            return maybeError ?? IpcError.UnknownError;
        }

        SessionId = maybeSessionId.Value;
        _logger.LogInformation("Tracing start for session {SessionId}", SessionId);
        return null;
    }

    public async Task<byte[]?> StopAsync()
    {
        if (SessionId is null) throw new InvalidOperationException($"Failed to stop recording. Session Id is not known.");
        if (_networkSocket is null) throw new InvalidOperationException($"Failed to stop recording. Socket is null.");

        _logger.LogInformation("stopping...");
        var stopEndpoint = new UnixDomainSocketEndPoint(_filename);
        using var stopSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await stopSocket.ConnectAsync(stopEndpoint).ConfigureAwait(false);
        _logger.LogInformation($"Stop socket connected...");
        bool stopRequested = await DiagnosticIpc.TryRequestStopTracing(stopSocket, SessionId.Value).ConfigureAwait(false);
        if (!stopRequested)
        {
            _logger.LogError("Failed to request stop tracing.");
            return null;
        }

        byte[] buffer = new byte[1024 * 1024 * 1024];
        int totalRead = 0;
        int read = 0;
        do
        {
            read = await _networkSocket.ReceiveAsync(buffer.AsMemory(totalRead)).ConfigureAwait(false);
            totalRead += read;
        }
        while (read > 0);

        _logger.LogInformation("Read {TotalRead} bytes... Capacity = {BufferLength}", totalRead, buffer.Length);

        _logger.LogInformation("Waiting for session to be stopped...");
        ulong? stopped = await DiagnosticIpc.TryWaitStopTracing(stopSocket, SessionId.Value).ConfigureAwait(false);
        _logger.LogInformation("Tracing stopped: {Stopped}", stopped ?? 0);
        return buffer[..totalRead];;
    }

    public void Dispose() => _networkSocket?.Dispose();
}
