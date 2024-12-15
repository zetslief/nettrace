using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Nettrace;
using ReactiveUI;

using MetadataBlock = Nettrace.NettraceReader.EventBlob<Nettrace.NettraceReader.MetadataEvent>;
using EventBlock = Nettrace.NettraceReader.EventBlob<Nettrace.NettraceReader.Event>;

namespace Explorer.ViewModels;

public class MetadataBlobViewModel(MetadataBlock metadata)
{
    public MetadataBlock Block { get; } = metadata;

    public override string ToString() => Block.ToString();
}

public class EventViewModel(EventBlock metadataBlock)
{
    private readonly EventBlock eventBlock = metadataBlock;

    public EventBlock Block => eventBlock;

    public override string ToString()
    {
        return eventBlock.ToString();
    }
}

public class MainWindowViewModel : ReactiveObject
{
    private string? filePath = "./../perf.nettrace";
    private string status = string.Empty;

    private MetadataBlobViewModel[]? metadataBlocks = null;
    private MetadataBlobViewModel? selectedMetadataBlock = null; 

    private IEnumerable<EventViewModel>? allEventBlocks = null;
    private IEnumerable<EventViewModel>? eventBlocks = null;

    public MainWindowViewModel()
    {
        WelcomeCommand = ReactiveCommand.Create(OnCommand);
        this.WhenAnyValue(x => x.SelectedMetadataBlock)
            .Subscribe(RefreshEventBlocks);
    }

    private void RefreshEventBlocks(MetadataBlobViewModel? model)
    {
        Console.WriteLine($"Selected: {model?.Block}");

        if (model is null)
        {
            EventBlocks = [];
            return;
        }

        var metadataId = model.Block.Payload.Header.MetaDataId;
        Console.WriteLine($"Metadata IDs: {string.Join(',', metadataId)}");
        EventBlocks = allEventBlocks?
            .Where(b => b.Block.MetadataId == metadataId)
            .ToArray();
    }

    public ICommand WelcomeCommand { get; }

    public string? FilePath
    {
        get => filePath;
        set => this.RaiseAndSetIfChanged(ref filePath, value);
    }

    public MetadataBlobViewModel[]? MetadataBlocks
    {
        get => metadataBlocks;
        private set => this.RaiseAndSetIfChanged(ref metadataBlocks, value);
    }

    public MetadataBlobViewModel? SelectedMetadataBlock
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
        allEventBlocks = nettrace.EventBlocks
            .SelectMany(eb => eb.EventBlobs)
            .Select(b => new EventViewModel(b))
            .ToArray();
        MetadataBlocks = nettrace.MetadataBlocks
            .SelectMany(s => s.EventBlobs)
            .Select(s => new MetadataBlobViewModel(s))
            .ToArray();
        Status = $"Read {stream.Position} bytes";
    }
}