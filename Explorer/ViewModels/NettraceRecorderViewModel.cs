using ReactiveUI;
using System.Linq;
using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Windows.Input;
using System.Diagnostics;
using System.Threading.Tasks;
using Ipc;

namespace Explorer.ViewModels;

public sealed class ProcessViewModel(string socketFileName, string? error, Process? process)
{
    public int? Id { get; } = process?.Id;
    public string? Name { get; } = process?.ProcessName;
    public string SocketFilename { get; } = socketFileName;
    public string? Error { get; } = error;
}

public sealed class EventProviderViewModel(string name)
{
    public string Name { get; } = name;
}

public class NettraceRecorderViewModel : ReactiveObject
{
    private IEnumerable<ProcessViewModel>? processes;
    private ProcessViewModel? selectedProcess;
    private IEnumerable<EventProviderViewModel>? eventProviders;

    public NettraceRecorderViewModel()
    {
        RecordCommand = ReactiveCommand.Create(RecordAsync);
        RefreshCommand = ReactiveCommand.Create(Refresh);
        
        eventProviders = [new("ProfileMe")];
        
        Refresh();
    }

    public ICommand RecordCommand { get; }
    public ICommand RefreshCommand { get; }

    public IEnumerable<ProcessViewModel>? Processes
    {
        get => processes;
        set => this.RaiseAndSetIfChanged(ref processes, value);
    }

    public ProcessViewModel? SelectedProcess
    {
        get => selectedProcess;
        set => this.RaiseAndSetIfChanged(ref selectedProcess, value);
    }

    public IEnumerable<EventProviderViewModel>? EventProviders
    {
        get => eventProviders;
        set => this.RaiseAndSetIfChanged(ref eventProviders, value);
    }

    private async Task RecordAsync()
    {
        static Provider ToEventProvider(EventProviderViewModel vm)
            => new(vm.Name, ulong.MaxValue, 0, string.Empty);
        
        Console.WriteLine($"{SelectedProcess?.SocketFilename}");
        if (EventProviders is null || SelectedProcess is null)
        {
            return;
        }
        
        var networkEndpoint = new UnixDomainSocketEndPoint(SelectedProcess.SocketFilename);
        using var networkSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await networkSocket.ConnectAsync(networkEndpoint).ConfigureAwait(false);
        var (maybeError, maybeSessionId) = await DiagnosticIpc.TryCollectTracing(networkSocket, EventProviders.Select(ToEventProvider).ToArray()).ConfigureAwait(false);

        if (maybeError is not null || maybeSessionId is null)
        {
            Console.WriteLine($"Failed to start collecting trace: {maybeError}");
            return;
        }
        
        var sessionId = maybeSessionId.Value;
        Console.WriteLine($"Tracing start for session {sessionId}");

        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Console.WriteLine($"Stopping...");
        var stopEndpoint = new UnixDomainSocketEndPoint(SelectedProcess.SocketFilename);
        using var stopSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await stopSocket.ConnectAsync(stopEndpoint).ConfigureAwait(false);
        Console.WriteLine($"Stop socket connected...");
        var stopRequested = await DiagnosticIpc.TryRequestStopTracing(stopSocket, sessionId).ConfigureAwait(false);
        if (!stopRequested)
        {
            Console.WriteLine($"Failed to request stop tracing.");
            return;
        }
        
        var buffer = new byte[1024];
        var totalRead = 0;
        var read = 1;
        do
        {
            read = await networkSocket.ReceiveAsync(buffer).ConfigureAwait(false); 
            totalRead += read;
        }
        while (read > 0);

        Console.WriteLine($"Reading {totalRead} bytes...");
        
        Console.WriteLine($"Waiting for session to be stopped...");
        var stopped = await DiagnosticIpc.TryWaitStopTracing(stopSocket, sessionId).ConfigureAwait(false);
        Console.WriteLine($"Tracing stopped: {stopped}");
    }

    private void Refresh()
    {
        const string prefix = "dotnet-diagnostic-";

        static ProcessViewModel FileToProcess(string socketFile)
        {
            var fileName = Path.GetFileName(socketFile);
            var endIndex = fileName.IndexOf('-', prefix.Length);
            var range = prefix.Length..endIndex;
            var processId = fileName[range];
            if (!int.TryParse(processId, out var id))
            {
                return new(socketFile, $"Failed to parse process id {processId} in {fileName}({range})", null);
            }

            try
            {
                var process = Process.GetProcessById(id);
                return new(socketFile, null, process);
            }
            catch (ArgumentException e)
            {
                return new(socketFile, $"Failed to find process with id {id} ({fileName}). Error: {e}", null);
            }
        }

        var directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";
        var files = Directory.GetFiles(directory, $"{prefix}*");
        Processes = files
            .Select(FileToProcess)
            .ToArray();
    }
}