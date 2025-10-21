using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using Nettrace;
using ReactiveUI;
using static Nettrace.Helpers;
using static Nettrace.NettraceReader;

namespace Explorer.ViewModels;

public class EventBlobViewModel(Trace trace, EventBlob<Event> eventBlob, EventBlob<MetadataEvent> metadata)
{
    public EventBlob<Event> Blob => eventBlob;
    public DateTime Timestamp => QpcToUtc(trace, eventBlob.TimeStamp);
    public int EventId => metadata.Payload.Header.EventId;
    public string EventName => metadata.Payload.Header.EventName;
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
        var metadataCache = file.BuildMetadataCache();
        bool metadataIsIncomplete = false;
        foreach (var blob in file.EventBlocks.SelectMany(block => block.EventBlobs))
        {
            if (!metadataCache.ContainsKey(blob.MetadataId))
            {
                _logger.LogError("{Blob} does not have corresponding metadata id.", blob);
                metadataIsIncomplete = true;
            }
        }
        AllEventBlobs = metadataIsIncomplete ? [] : [.. file.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(_trace, blob, metadataCache[blob.MetadataId]))
        ];
    }
}
