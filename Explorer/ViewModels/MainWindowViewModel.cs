using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Nettrace;
using ReactiveUI;

using MetadataBlock = Nettrace.NettraceReader.Block<Nettrace.NettraceReader.MetadataEvent>;
using EventBlock = Nettrace.NettraceReader.Block<Nettrace.NettraceReader.Event>;

namespace Explorer.ViewModels;

public class MetadataViewModel(MetadataBlock metadataBlock)
{
    private readonly MetadataBlock metadataBlock = metadataBlock;

    public override string ToString()
    {
        return metadataBlock.ToString();
    }
}

public class EventViewModel(EventBlock metadataBlock)
{
    private readonly EventBlock eventBlock = metadataBlock;

    public override string ToString()
    {
        return eventBlock.ToString();
    }
}

public class MainWindowViewModel : ReactiveObject
{
    private string? filePath = "./../perf.nettrace";
    private string status = string.Empty;

    private MetadataViewModel[]? metadataBlocks = null;
    private MetadataViewModel? selectedMetadataBlock = null; 

    private IEnumerable<EventViewModel>? eventBlocks = null;

    public MainWindowViewModel()
    {
        WelcomeCommand = ReactiveCommand.Create(OnCommand);
    }

    public ICommand WelcomeCommand { get; }

    public string? FilePath
    {
        get => filePath;
        set => this.RaiseAndSetIfChanged(ref filePath, value);
    }

    public MetadataViewModel[]? MetadataBlocks
    {
        get => metadataBlocks;
        private set => this.RaiseAndSetIfChanged(ref metadataBlocks, value);
    }

    public MetadataViewModel? SelectedMetadataBlock
    {
        get => selectedMetadataBlock;
        set => this.RaiseAndSetIfChanged(ref selectedMetadataBlock, value);
    }

    public IEnumerable<EventViewModel>? EventBlocks
    {
        get => eventBlocks;
        set => this.RaiseAndSetIfChanged(ref eventBlocks, value);
    }

    public string Status
    {
        get => status;
        private set => this.RaiseAndSetIfChanged(ref status, value);
    }

    private void OnCommand()
    {
        if (filePath is null)
        {
            return;
        }

        var path = Path.GetFullPath(filePath);

        if (!File.Exists(path))
        {
            Status = $"File not found: {path}";
            return;
        }

        using var stream =  File.Open(path, FileMode.Open);
        var nettrace = NettraceReader.Read(stream);
        MetadataBlocks = nettrace.MetadataBlocks.Select(b => new MetadataViewModel(b)).ToArray();
        EventBlocks = nettrace.EventBlocks.Select(b => new EventViewModel(b)).ToArray();
        Status = $"Read {stream.Position} bytes";
    }
}