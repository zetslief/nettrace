using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
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
    private readonly ILogger<NettraceReaderViewModel> _logger;
    private readonly NettraceParser _parser;
    private readonly IStorageProvider _storageProvider;
    private string? _filePath = "./../traces/perf_with_work.nettrace";
    private string _status = string.Empty;

    private Trace? _trace;

    private ImmutableArray<EventBlobViewModel>? _allEventBlobs;

    public NettraceReaderViewModel(ILogger<NettraceReaderViewModel> logger, NettraceParser parser, IStorageProvider storageProvider)
    {
        _logger = logger;
        _parser = parser;
        _storageProvider = storageProvider;
        ReadFileCommand = ReactiveCommand.Create(OnFileRead);
        BrowseFileCommand = ReactiveCommand.Create(OnFileBrowseAsync);

        _parser.OnFileChanged += (s, e)
            => ReadFile(parser.GetFile() ?? throw new InvalidOperationException($"Failed to get nettrace file."));
        _logger.LogInformation("{ViewModelType}  is created.", typeof(NettraceRecorderViewModel));
    }

    public ICommand ReadFileCommand { get; }
    public ICommand BrowseFileCommand { get; }

    public string? FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public ImmutableArray<EventBlobViewModel>? AllEventBlobs
    {
        get => _allEventBlobs;
        set => this.RaiseAndSetIfChanged(ref _allEventBlobs, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private async Task OnFileBrowseAsync()
    {
        var startFolder = await _storageProvider.TryGetFolderFromPathAsync(Environment.CurrentDirectory).ConfigureAwait(true);
        _logger.LogInformation("Opening file picker at folder: {StartFolder}", startFolder?.TryGetLocalPath());
        var result = await _storageProvider.OpenFilePickerAsync(new()
        {
            Title = "Select nettrace file",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = [new("nettrace") { Patterns = ["*.nettrace"] }, new("all") { Patterns = ["*.*"] }]
        }).ConfigureAwait(true);
        if (result.Count != 1) return;
        string? localPath = result[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(localPath)) return;
        FilePath = localPath;
        OnFileRead();
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
        _allEventBlobs = [.. file.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(_trace, blob))
        ];
    }
}
