using ReactiveUI;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Input;
using System.Diagnostics;

namespace Explorer.ViewModels;

public sealed class ProcessViewModel(string socketFileName, string? error, Process? process)
{
    public int? Id { get; } = process?.Id;
    public string? Name { get; } = process?.ProcessName;
    public string SocketFilename { get; } = socketFileName;
    public string? Error { get; } = error;
}

public class NettraceRecorderViewModel : ReactiveObject
{
    private IEnumerable<ProcessViewModel>? processes;
    private ProcessViewModel? selectedProcess;

    public NettraceRecorderViewModel()
    {
        RecordCommand = ReactiveCommand.Create(Record);
        RefreshCommand = ReactiveCommand.Create(Refresh);
        
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

    private void Record()
    {
        Console.WriteLine($"{SelectedProcess?.SocketFilename}");
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