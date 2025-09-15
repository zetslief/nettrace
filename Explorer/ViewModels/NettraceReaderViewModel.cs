using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Explorer.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using static Nettrace.Helpers;
using static Nettrace.NettraceReader;

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
    public EventBlob<MetadataEvent> Blob { get; } = blob;
    public MetadataEvent Payload { get; } = blob.Payload;

    public override string ToString() => Blob.ToString();
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

public class NettraceReaderViewModel : ReactiveObject, IViewModel
{
    private readonly NettraceParser _parser;
    private string? _filePath = "./../traces/perf_with_work.nettrace";
    private string _status = string.Empty;

    private Trace? _trace;

    private IReadOnlyCollection<MetadataBlockViewModel>? _metadataBlocks = null;
    private MetadataBlockViewModel? _selectedMetadataBlock = null;

    private IReadOnlyCollection<EventBlockViewModel>? _eventBlocks = null;

    private readonly ObservableAsPropertyHelper<IEnumerable<MetadataEventBlobViewModel>?> _metadataEventBlobs;
    private MetadataEventBlobViewModel? _selectedMetadataEventBlob = null;
    private EventBlobViewModel[]? _allEventBlobs;

    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<EventBlobViewModel>?> _eventBlobs;
    private readonly ObservableAsPropertyHelper<Node> _timePoints;

    public NettraceReaderViewModel(ILogger<NettraceReaderViewModel> logger, NettraceParser parser)
    {
        _parser = parser;
        logger.LogInformation("{ViewModelType}  is created.", typeof(NettraceRecorderViewModel));
        ReadFileCommand = ReactiveCommand.Create(OnFileRead);

        this.WhenAnyValue(v => v.MetadataBlocks)
            .Select(MetadataBlockViewModel? (m) => null)
            .ToProperty(this, vm => vm.SelectedMetadataBlock);
        _metadataEventBlobs = this.WhenAnyValue(x => x.SelectedMetadataBlock)
            .Select(metadataBlock => metadataBlock?.Blobs)
            .ToProperty(this, vm => vm.MetadataEventBlobs);
        _eventBlobs = this.WhenAnyValue(x => x.SelectedMetadataEventBlob)
            .Select(blob => _allEventBlobs?.Where(eb => eb.Blob.MetadataId == blob?.Payload.Header.MetaDataId).ToArray())
            .ToProperty(this, vm => vm.EventBlobs);
        _timePoints = this.WhenAnyValue(v => v.MetadataBlocks, v => v.EventBlocks, v => v.EventBlobs, ToLabeledRanges)
            .ToProperty(this, vm => vm.TimePoints);

        _parser.OnFileChanged += (s, e)
            => ReadFile(parser.GetFile() ?? throw new InvalidOperationException($"Failed to get nettrace file."));
    }

    public ICommand ReadFileCommand { get; }

    public string? FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public IReadOnlyCollection<MetadataBlockViewModel>? MetadataBlocks
    {
        get => _metadataBlocks;
        private set => this.RaiseAndSetIfChanged(ref _metadataBlocks, value);
    }

    public MetadataBlockViewModel? SelectedMetadataBlock
    {
        get => _selectedMetadataBlock;
        set => this.RaiseAndSetIfChanged(ref _selectedMetadataBlock, value);
    }

    public IReadOnlyCollection<EventBlockViewModel>? EventBlocks
    {
        get => this._eventBlocks;
        set => this.RaiseAndSetIfChanged(ref _eventBlocks, value);
    }

    public IEnumerable<MetadataEventBlobViewModel>? MetadataEventBlobs => _metadataEventBlobs.Value;

    public MetadataEventBlobViewModel? SelectedMetadataEventBlob
    {
        get => _selectedMetadataEventBlob;
        set => this.RaiseAndSetIfChanged(ref _selectedMetadataEventBlob, value);
    }

    public IReadOnlyCollection<EventBlobViewModel>? EventBlobs => _eventBlobs.Value;

    public Node TimePoints => _timePoints.Value;

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private void OnFileRead()
    {
        if (_filePath is null)
            return;

        string path = Path.GetFullPath(_filePath);

        if (!File.Exists(path))
        {
            Status = $"File not found: {path}";
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        _parser.SetFile(bytes);
        Status = $"Read {bytes.Length} bytes";
    }

    private void ReadFile(NettraceFile file)
    {
        _trace = file.Trace;
        _allEventBlobs = file.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(_trace, blob))
            .ToArray();
        MetadataBlocks = file.MetadataBlocks
            .Select(block => new MetadataBlockViewModel(_trace, block))
            .ToArray();
        EventBlocks = file.EventBlocks.Select(block => new EventBlockViewModel(_trace, block)).ToArray();
    }

    private Node ToLabeledRanges(
        IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks,
        IReadOnlyCollection<EventBlockViewModel>? eventBlocks,
        IReadOnlyCollection<EventBlobViewModel>? eventBlobs)
    {
        var result = new List<Node>();

        if (eventBlobs?.Count > 0)
        {
            var eventBlobNodes = new List<Node>();
            BlobsToRanges(_trace!, eventBlobs, eventBlobNodes);
            result.Add(new TreeNode(eventBlobNodes));
        }

        if (eventBlocks?.Count > 0)
        {
            var eventBlockNodes = new List<Node>();
            EventBlocksToRanges(_trace!, eventBlocks, eventBlockNodes);
            result.Add(new TreeNode(eventBlockNodes));
        }

        if (metadataBlocks?.Count > 0)
        {
            var metadataBlockNodes = new List<Node>();
            MetadataBlocksToRanges(_trace!, metadataBlocks, metadataBlockNodes);
            result.Add(new TreeNode(metadataBlockNodes));
        }

        return new TreeNode(result);
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
