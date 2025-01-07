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
using static Nettrace.Helpers;

namespace Explorer.ViewModels;

public class MetadataBlockViewModel(Trace trace, Block<MetadataEvent> metadata)
{
    public BlockHeaderViewModel Header { get; } = new(trace, metadata.Header);

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

public sealed class BlockHeaderViewModel(Trace trace, Header header)
{
    public DateTime MinTime => QpcToUtc(trace, header.MinTimestamp);
    public DateTime MaxTime => QpcToUtc(trace, header.MaxTimestamp);
    public TimeSpan Duration => MaxTime - MinTime;
}

public sealed class EventBlockViewModel(Trace trace, Block<Event> block)
{
    public BlockHeaderViewModel Header { get; } = new(trace, block.Header);
    public int BlobCount => block.EventBlobs.Length;
    public override string ToString() => block.ToString();
}

public class EventBlobViewModel(Trace trace, EventBlob<Event> eventBlob)
{
    public EventBlob<Event> Blob => eventBlob;
    public DateTime Timestamp => QpcToUtc(trace, eventBlob.TimeStamp);
    public override string ToString() => Blob.ToString();
}

public class MainWindowViewModel : ReactiveObject
{
    private string? filePath = "./../perf_100ms.nettrace";
    private string status = string.Empty;

    private Trace? trace;

    private IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks = null;
    private MetadataBlockViewModel? selectedMetadataBlock = null;

    private IReadOnlyCollection<EventBlockViewModel>? eventBlocks = null;

    private readonly ObservableAsPropertyHelper<IEnumerable<MetadataEventBlobViewModel>?> metadataEventBlobs;
    private MetadataEventBlobViewModel? selectedMetadataEventBlob = null;
    private EventBlobViewModel[]? allEventBlobs;

    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<EventBlobViewModel>?> eventBlobs;
    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<Node>> timePoints;

    public MainWindowViewModel()
    {
        WelcomeCommand = ReactiveCommand.Create(OnCommand);

        this.WhenAnyValue(v => v.MetadataBlocks)
            .Select(MetadataBlockViewModel? (m) => null)
            .ToProperty(this, vm => vm.SelectedMetadataBlock);
        metadataEventBlobs = this.WhenAnyValue(x => x.SelectedMetadataBlock)
            .Select(metadataBlock => metadataBlock?.Blobs)
            .ToProperty(this, vm => vm.MetadataEventBlobs);
        eventBlobs = this.WhenAnyValue(x => x.SelectedMetadataEventBlob)
            .Select(blob => allEventBlobs?.Where(eb => eb.Blob.MetadataId == blob?.Payload.Header.MetaDataId).ToArray())
            .ToProperty(this, vm => vm.EventBlobs);
        timePoints = this.WhenAnyValue(v => v.MetadataBlocks, v => v.EventBlocks, v => v.EventBlobs, ToLabeledRanges)
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
    
    public IReadOnlyCollection<EventBlockViewModel>? EventBlocks
    {
        get => this.eventBlocks;
        set => this.RaiseAndSetIfChanged(ref eventBlocks, value);
    }

    public IEnumerable<MetadataEventBlobViewModel>? MetadataEventBlobs => metadataEventBlobs.Value;

    public MetadataEventBlobViewModel? SelectedMetadataEventBlob
    {
        get => selectedMetadataEventBlob;
        set => this.RaiseAndSetIfChanged(ref selectedMetadataEventBlob, value);
    }

    public IReadOnlyCollection<EventBlobViewModel>? EventBlobs => eventBlobs.Value;

    public IReadOnlyCollection<Node> TimePoints => timePoints.Value;

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
            .Select(blob => new EventBlobViewModel(trace, blob))
            .ToArray();
        MetadataBlocks = nettrace.MetadataBlocks
            .Select(block => new MetadataBlockViewModel(trace, block))
            .ToArray();
        EventBlocks = nettrace.EventBlocks.Select(block => new EventBlockViewModel(trace, block)).ToArray();
        Status = $"Read {stream.Position} bytes";
    }

    private IReadOnlyCollection<Node> ToLabeledRanges(
        IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks,
        IReadOnlyCollection<EventBlockViewModel>? eventBlocks,
        IReadOnlyCollection<EventBlobViewModel>? eventBlobs)
    {
        var result = new List<Node>();

        if (eventBlobs?.Count > 0)
        {
            var eventBlobNodes = new List<Node>();
            BlobsToRanges(trace!, eventBlobs, eventBlobNodes);
            result.Add(new TreeNode(eventBlobNodes));
        }

        if (eventBlocks?.Count > 0)
        {
            var eventBlockNodes = new List<Node>();
            EventBlocksToRanges(trace!, eventBlocks, eventBlockNodes);
            result.Add(new TreeNode(eventBlockNodes));
        }

        if (metadataBlocks?.Count > 0)
        {
            var metadataBlockNodes = new List<Node>();
            MetadataBlocksToRanges(trace!, metadataBlocks, metadataBlockNodes);
            result.Add(new TreeNode(metadataBlockNodes));
        }

        return [new TreeNode(result)];
    }

    private static void MetadataBlocksToRanges(
        Trace trace,
        IReadOnlyCollection<MetadataBlockViewModel> metadataBlocks,
        List<Node> result)
    {
        result.AddRange(metadataBlocks
            .OrderBy(block => block.Header.MinTime)
            .Select(block => new Rectangle(new(ToSeconds(block.Header.MinTime), 0, ToSeconds(block.Header.MaxTime), 1), Avalonia.Media.Colors.Red)));
    }

    private static void EventBlocksToRanges(
        Trace trace,
        IReadOnlyCollection<EventBlockViewModel> metadataBlocks,
        List<Node> result)
    {
        result.AddRange(metadataBlocks
            .OrderBy(block => block.Header.MinTime)
            .Select(block => new Rectangle(new(ToSeconds(block.Header.MinTime), 1, ToSeconds(block.Header.MaxTime), 2), Avalonia.Media.Colors.Blue)));
    }

    private static void BlobsToRanges(Trace trace, IReadOnlyCollection<EventBlobViewModel> blobs, List<Node> output)
    {
        foreach (var blob in blobs)
        {
            var currentTime = QpcToUtc(trace, blob.Blob.TimeStamp);
            output.Add(new Point(new(ToSeconds(currentTime), 2.1f), Avalonia.Media.Colors.Green));
        }
    }
    
    private static float ToSeconds(DateTime dateTime)
        => (float)((dateTime - dateTime.Date).Ticks / 10_000_000d);
}
