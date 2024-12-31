using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Explorer.Controls;
using Nettrace;
using ReactiveUI;

using static Nettrace.NettraceReader;
using Range = Explorer.Controls.Range;

namespace Explorer.ViewModels;

public class MetadataBlockViewModel(Trace trace, Block<MetadataEvent> metadata)
{
    private readonly Block<MetadataEvent> metadata = metadata;

    public Header Header => metadata.Header;

    public TimeSpan Duraction => MainWindowViewModel.ToUtc(trace, Header.MaxTimestamp) - MainWindowViewModel.ToUtc(trace, Header.MinTimestamp);

    public IReadOnlyCollection<MetadataEventBlobViewModel> Blobs { get; } = metadata.EventBlobs
        .Select(b => new MetadataEventBlobViewModel(b))
        .ToArray();
}

public class MetadataEventBlobViewModel(EventBlob<MetadataEvent> blob)
{
    private readonly EventBlob<MetadataEvent> blob = blob;

    public MetadataEvent Payload => blob.Payload;

    public override string ToString() => blob.ToString();
}

public class EventBlockViewModel(Block<Event> metadataBlock)
{
    private readonly Block<Event> eventBlock = metadataBlock;

    public Block<Event> Block => eventBlock;

    public override string ToString()
    {
        return eventBlock.ToString();
    }
}

public class EventBlobViewModel(EventBlob<Event> eventBlob)
{
    public EventBlob<Event> Blob => eventBlob;

    public override string ToString() => Blob.ToString();
}

public class MainWindowViewModel : ReactiveObject
{
    private string? filePath = "./../perf.nettrace";
    private string status = string.Empty;

    private Trace? trace;

    private IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks = null;
    private MetadataBlockViewModel? selectedMetadataBlock = null; 

    private readonly ObservableAsPropertyHelper<IEnumerable<MetadataEventBlobViewModel>?> metadataEventBlobs;
    private MetadataEventBlobViewModel? selectedMetadataEventBlob = null; 
    private EventBlobViewModel[]? allEventBlobs; 

    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<EventBlobViewModel>?> eventBlobs;
    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<Renderable>> timePoints;

    public MainWindowViewModel()
    {
        WelcomeCommand = ReactiveCommand.Create(OnCommand);

        this.WhenAnyValue(v => v.MetadataBlocks)
            .Select(m => (MetadataBlockViewModel?)null)
            .ToProperty(this, vm => vm.SelectedMetadataBlock);
        metadataEventBlobs = this.WhenAnyValue(x => x.SelectedMetadataBlock)
            .Select(metadataBlock => metadataBlock?.Blobs)   
            .ToProperty(this, vm => vm.MetadataEventBlobs);
        eventBlobs = this.WhenAnyValue(x => x.SelectedMetadataEventBlob)
            .Select(blob => allEventBlobs?.Where(eb => eb.Blob.MetadataId == blob?.Payload.Header.MetaDataId).ToArray())
            .ToProperty(this, vm => vm.EventBlobs); 
        timePoints = this.WhenAnyValue(v => v.MetadataBlocks, v => v.EventBlobs, ToLabeledRanges)
            .ToProperty(this, vm => vm.TimePoints);
    }

    public ICommand WelcomeCommand { get; }

    public string? FilePath
    {
        get => filePath;
        set => this.RaiseAndSetIfChanged(ref filePath, value);
    }

    public IReadOnlyCollection<MetadataBlockViewModel>? MetadataBlocks
    {
        get => metadataBlocks;
        private set => this.RaiseAndSetIfChanged(ref metadataBlocks, value);
    }

    public MetadataBlockViewModel? SelectedMetadataBlock
    {
        get => selectedMetadataBlock;
        set => this.RaiseAndSetIfChanged(ref selectedMetadataBlock, value);
    }

    public IEnumerable<MetadataEventBlobViewModel>? MetadataEventBlobs => metadataEventBlobs.Value;

    public MetadataEventBlobViewModel? SelectedMetadataEventBlob
    {
        get => selectedMetadataEventBlob;
        set => this.RaiseAndSetIfChanged(ref selectedMetadataEventBlob, value);
    }

    public IReadOnlyCollection<EventBlobViewModel>? EventBlobs => eventBlobs.Value;

    public IReadOnlyCollection<Renderable> TimePoints => timePoints.Value;

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
        trace = nettrace.Trace;
        allEventBlobs = nettrace.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(blob))
            .ToArray();
        MetadataBlocks = nettrace.MetadataBlocks
            .Select(block => new MetadataBlockViewModel(trace, block))
            .ToArray();
        Status = $"Read {stream.Position} bytes";
    }

    private IReadOnlyCollection<Renderable> ToLabeledRanges(IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks, IReadOnlyCollection<EventBlobViewModel>? eventBlobs)
    {
        var result = new Stack<IReadOnlyCollection<Renderable>>();

        if (metadataBlocks?.Count > 1)
        {
            var metadataBlockRenderables = new List<Renderable>();
            MetadataBlocksToRanges(trace!, metadataBlocks, metadataBlockRenderables);
            result.Push(metadataBlockRenderables);
        }

        if (eventBlobs?.Count > 1)
        {
            var eventBlobRenderables = new List<Renderable>();
            BlobsToRanges(trace!, eventBlobs, eventBlobRenderables);
            result.Push(eventBlobRenderables);
        }

        return [new StackedRenderable(result)];
    }

    private static void MetadataBlocksToRanges(
        Trace trace,
        IReadOnlyCollection<MetadataBlockViewModel> metadataBlocks,
        List<Renderable> result)
    {
        foreach (var block in metadataBlocks.OrderBy(block => block.Header.MinTimestamp))
            result.Add(new LabeledRange("", new(
                ToUtc(trace, block.Header.MinTimestamp),
                ToUtc(trace, block.Header.MaxTimestamp))));
    }

    private static void BlobsToRanges(Trace trace, IReadOnlyCollection<EventBlobViewModel> blobs, List<Renderable> output)
    {
        DateTime? previousTime = null;
        foreach (var blob in blobs.OrderBy(blob => blob.Blob.TimeStamp))
        {
            if (previousTime is null)
            {
                previousTime = ToUtc(trace, blob.Blob.TimeStamp);
            }
            else
            {
                output.Add(new LabeledRange("", new(previousTime.Value, ToUtc(trace, blob.Blob.TimeStamp)))); 
            }
        }
    }

    public static DateTime ToUtc(Trace trace, long timestamp)
    {
        long ticks = (long)((timestamp - trace.SynTimeQpc) * 1_0_000_000 / (double)trace.QpcFrequency);
        return trace.DateTime.AddTicks(ticks);
    }
}