using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Ipc;

namespace Explorer.ViewModels;

public sealed class RecordingService(string filename, IReadOnlyCollection<Provider> providers) : IDisposable
{
    private readonly string _filename = filename;
    private readonly IReadOnlyCollection<Provider> _providers = providers;
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
            Console.WriteLine($"Failed to start collecting trace: {maybeError}");
            return maybeError ?? IpcError.UnknownError;
        }
        
        SessionId = maybeSessionId.Value;
        Console.WriteLine($"Tracing start for session {SessionId}");
        return null;
    }

    public async Task StopAsync()
    {
        if (SessionId is null) throw new InvalidOperationException($"Failed to stop recording. Session Id is not known.");
        if (_networkSocket is null) throw new InvalidOperationException($"Failed to stop recording. Socket is null.");
        
        Console.WriteLine($"Stopping...");
        var stopEndpoint = new UnixDomainSocketEndPoint(_filename);
        using var stopSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await stopSocket.ConnectAsync(stopEndpoint).ConfigureAwait(false);
        Console.WriteLine($"Stop socket connected...");
        var stopRequested = await DiagnosticIpc.TryRequestStopTracing(stopSocket, SessionId.Value).ConfigureAwait(false);
        if (!stopRequested)
        {
            Console.WriteLine($"Failed to request stop tracing.");
            return;
        }
        
        var buffer = new byte[1024];
        var totalRead = 0;
        var read = 0;
        do
        {
            read = await _networkSocket.ReceiveAsync(buffer).ConfigureAwait(false); 
            totalRead += read;
        }
        while (read > 0);

        Console.WriteLine($"Reading {totalRead} bytes...");
        
        Console.WriteLine($"Waiting for session to be stopped...");
        var stopped = await DiagnosticIpc.TryWaitStopTracing(stopSocket, SessionId.Value).ConfigureAwait(false);
        Console.WriteLine($"Tracing stopped: {stopped}");
    }
    
    public void Dispose() => _networkSocket?.Dispose();
}