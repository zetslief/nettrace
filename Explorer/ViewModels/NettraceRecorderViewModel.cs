using ReactiveUI;
using System.Linq;
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
        Processes = Process.GetProcesses()
            .OrderByDescending(p => p.StartTime)
            .Select(p => new ProcessViewModel(p))
            .ToArray();
    }
}