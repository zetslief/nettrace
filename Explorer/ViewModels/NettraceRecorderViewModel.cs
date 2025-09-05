using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Ipc;
using Microsoft.Extensions.Logging;
using Nettrace;
using ReactiveUI;

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
    private readonly ILogger<NettraceRecorderViewModel> _logger;
    private IEnumerable<ProcessViewModel>? processes;
    private IEnumerable<ProcessViewModel>? failedProcesses;
    private ProcessViewModel? selectedProcess;
    private IEnumerable<EventProviderViewModel>? eventProviders;
    private bool _openFileAutomatically = true;

    public NettraceRecorderViewModel(ILogger<NettraceRecorderViewModel> logger)
    {
        _logger = logger;
        RecordCommand = ReactiveCommand.Create(RecordAsync);
        RefreshCommand = ReactiveCommand.Create(Refresh);

        eventProviders = [new("ProfileMe"), new("System.Threading.Tasks.TplEventSource")];

        Refresh();
    }

    public ICommand RecordCommand { get; }
    public ICommand RefreshCommand { get; }

    public IEnumerable<ProcessViewModel>? Processes
    {
        get => processes;
        set => this.RaiseAndSetIfChanged(ref processes, value);
    }

    public IEnumerable<ProcessViewModel>? FailedProcesses
    {
        get => failedProcesses;
        set => this.RaiseAndSetIfChanged(ref failedProcesses, value);
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

    public bool OpenFileAutomatically
    {
        get => _openFileAutomatically;
        set => this.RaiseAndSetIfChanged(ref _openFileAutomatically, value);
    }

    private async Task RecordAsync()
    {
        static Provider ToEventProvider(EventProviderViewModel vm)
            => new(vm.Name, ulong.MaxValue, 0, string.Empty);

        _logger.LogInformation("{SelectedProcessSocketFilename}", SelectedProcess?.SocketFilename);
        if (EventProviders is null || SelectedProcess is null)
        {
            return;
        }

        using var recordingService = new RecordingService(SelectedProcess.SocketFilename, [.. EventProviders.Select(ToEventProvider)], _logger);
        var maybeError = await recordingService.StartAsync().ConfigureAwait(false);

        if (maybeError is not null || recordingService.SessionId is null)
        {
            _logger.LogError("Failed to start collecting trace: {MaybeError}", maybeError);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        byte[]? bytes = await recordingService.StopAsync().ConfigureAwait(false);
        if (bytes is null)
        {
            _logger.LogError("Failed to stop recording.");
            return;
        }

        using var stream = new MemoryStream(bytes);
        var nettraceFile = NettraceReader.Read(stream);
        _logger.LogInformation("{NettraceFile}", nettraceFile);
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
            if (!int.TryParse(processId, out int id))
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

        string directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";
        string[] files = Directory.GetFiles(directory, $"{prefix}*");
        Processes = [.. files
            .Select(FileToProcess)
            .Where(pvm => pvm.Error is null)
        ];
        FailedProcesses = [.. files
            .Select(FileToProcess)
            .Where(pvm => pvm.Error is not null)
        ];
    }
}
