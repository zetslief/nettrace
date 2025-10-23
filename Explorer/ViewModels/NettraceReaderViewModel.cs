using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using Nettrace;
using Nettrace.PayloadParsers;
using ReactiveUI;
using static Nettrace.Helpers;
using static Nettrace.NettraceReader;

namespace Explorer.ViewModels;

public sealed class EventBlobViewModel(Trace trace, EventBlob<Event> eventBlob, EventBlob<MetadataEvent> metadata)
{
    public EventBlob<Event> Blob => eventBlob;
    public DateTime Timestamp => QpcToUtc(trace, eventBlob.TimeStamp);
    public IEvent? Event { get; } = GenericTplParser(metadata.Payload.Header.EventName, eventBlob.Payload.Bytes.Span);
    public override string ToString() => Blob.ToString();

    private static IEvent? GenericTplParser(string eventName, ReadOnlySpan<byte> payloadBytes) => eventName switch
    {
        var name when name == NewId.Name => TplParser.ParseNewId(payloadBytes),
        var name when name == TraceSynchronousWorkBegin.Name => TplParser.ParseTraceSynchronousWorkBegin(payloadBytes),
        var name when name == TraceSynchronousWorkEnd.Name => TplParser.ParseTraceSynchronousWorkEnd(payloadBytes),
        var name when name == TaskWaitContinuationStarted.Name => TplParser.ParseTaskWaitContinuationStarted(payloadBytes),
        var name when name == TraceOperationEnd.Name => TplParser.ParseTraceOperationEnd(payloadBytes),
        var name when name == TraceOperationBegin.Name => TplParser.ParseTraceOperationBegin(payloadBytes),
        var name when name == TaskWaitContinuationComplete.Name => TplParser.ParseTaskWaitContinuationComplete(payloadBytes),
        var name when name == TaskWaitEnd.Name => TplParser.ParseTaskWaitEnd(payloadBytes),
        var name when name == TaskWaitBegin.Name => TplParser.ParseTaskWaitBegin(payloadBytes),
        var name when name == AwaitTaskContinuationScheduled.Name => TplParser.ParseAwaitTaskContinuationScheduled(payloadBytes),
        var name when name == TaskScheduled.Name => TplParser.ParseTaskScheduled(payloadBytes),
        var name when name == TraceOperationRelation.Name => TplParser.ParseTraceOperationRelation(payloadBytes),
        var name when name == ProcessInfo.Name => ProcessInfoParser.ParseProcessInfo(payloadBytes),
        var other => null
    };
}

public sealed class StackViewModel(int id, int pointerSize, Stack stack)
{
    private readonly int _id = id;
    private readonly int _pointerSize = pointerSize;
    private readonly Stack _stack = stack;

    public override string ToString()
    {
        StringBuilder builder = new();
        builder.AppendLine($"Id {_id} - {_stack}");
        int cursor = 0;
        var payload = _stack.Payload.AsSpan();
        while (cursor < payload.Length)
        {
            if (_pointerSize == 4)
            {
                builder.AppendLine($"\t0x{MemoryMarshal.Read<int>(payload[cursor..(cursor + _pointerSize)]):X}");
            }
            else
            {
                builder.AppendLine($"\t0x{MemoryMarshal.Read<long>(payload[cursor..(cursor + _pointerSize)]):X}");
            }
            cursor += _pointerSize;
        }
        return builder.ToString();
    }
}

public sealed class SequencePointBlockViewModel(SequencePointBlock block)
{
    public SequencePointBlock Block { get; } = block;
}

public class NettraceReaderViewModel : ReactiveObject, IViewModel
{
    private readonly ILogger<NettraceReaderViewModel> _logger;
    private readonly NettraceParser _parser;
    private readonly IStorageProvider _storageProvider;
    private string? _filePath = "./../traces/perf_with_work.nettrace";
    private string _status = string.Empty;

    private Trace? _trace;

    private ImmutableArray<EventBlobViewModel> _allEventBlobs = [];
    private ImmutableArray<StackViewModel> _allStacks = [];
    private SequencePointBlockViewModel? _sequencePointBlock;

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

    public ImmutableArray<EventBlobViewModel> AllEventBlobs
    {
        get => _allEventBlobs;
        private set => this.RaiseAndSetIfChanged(ref _allEventBlobs, value);
    }

    public ImmutableArray<StackViewModel> AllStacks
    {
        get => _allStacks;
        set => this.RaiseAndSetIfChanged(ref _allStacks, value);
    }

    public SequencePointBlockViewModel? SequencePointBlock
    {
        get => _sequencePointBlock;
        set => this.RaiseAndSetIfChanged(ref _sequencePointBlock, value);
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
        AllStacks = [.. file.StackBlock.Stacks.Select((s, i) => new StackViewModel(file.StackBlock.FirstId + i - 1, file.Trace.PointerSize, s))];
        SequencePointBlock = new(file.SequencePointBlock);
    }
}
