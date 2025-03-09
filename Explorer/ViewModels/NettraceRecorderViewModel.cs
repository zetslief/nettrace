using ReactiveUI;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace Explorer.ViewModels;

public class NettraceRecorderViewModel : ReactiveObject
{
    private IEnumerable<string>? processes;

    public NettraceRecorderViewModel()
    {
        processes = Process.GetProcesses().Select(p => p.ToString()).ToArray();
    }

    public IEnumerable<string>? Processes
    {
        get => processes;
        set => this.RaiseAndSetIfChanged(ref processes, value);
    }
}