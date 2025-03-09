using ReactiveUI;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Input;
using System.Diagnostics;

namespace Explorer.ViewModels;

public sealed class ProcessViewModel(Process process)
{
    public int Id { get; } = process.Id;
    public string Name { get; } = process.ProcessName;
}

public class NettraceRecorderViewModel : ReactiveObject
{
    private IEnumerable<ProcessViewModel>? processes;

    public NettraceRecorderViewModel()
    {
        RefreshCommand = ReactiveCommand.Create(Refresh);
        
        Refresh();
    }

    public ICommand RefreshCommand { get; }

    public IEnumerable<ProcessViewModel>? Processes
    {
        get => processes;
        set => this.RaiseAndSetIfChanged(ref processes, value);
    }

    private void Refresh()
    {
        const string prefix = "dotnet-diagnostic-";

        static Process? FileToProcess(string file)
        {
            file = Path.GetFileName(file);
            var endIndex = file.IndexOf('-', prefix.Length);
            Console.WriteLine($"{file} - {file[prefix.Length..endIndex]}");
            return int.TryParse(file[prefix.Length..endIndex], out var id) ? Process.GetProcessById(id) : null;
        }

        var directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";
        var files = Directory.GetFiles(directory, $"{prefix}*");
        Processes = files
            .Select(FileToProcess)
            .Select(p => new ProcessViewModel(p ?? throw new InvalidOperationException($"Failed to get process from file.")))
            .ToArray();
    }
}