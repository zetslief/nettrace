using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Nettrace;
using Nettrace.HighLevel;
using Nettrace.PayloadParsers;
using ReactiveUI;
using static Nettrace.Helpers;
using static Nettrace.NettraceReader;

namespace Explorer.ViewModels;

public sealed class EventBlobViewModel(Trace trace, EventBlob<Event> eventBlob, EventBlob<MetadataEvent> metadata)
{
    public EventBlob<Event> Blob => eventBlob;
    public EventBlob<MetadataEvent> MetadataBlob => metadata;
    public DateTime Timestamp => QpcToUtc(trace, eventBlob.TimeStamp);
    public IEventViewModel Event => field ??= MapToViewModel(RawEvent);
    public IEvent RawEvent { get; } = NettraceEventParser.ProcessEvent(metadata.Payload, eventBlob);
    public override string ToString() => Blob.ToString();

    private static IEventViewModel MapToViewModel(IEvent @event) => @event switch
    {
        MethodDCEndILToNativeMap map => new MethodDCEndILToNativeMapViewModel(map),
        var other => new DefaultEventViewModel(other),
    };

    public interface IEventViewModel { }

    public class DefaultEventViewModel(IEvent @event) : IEventViewModel
    {
        private readonly IEvent _event = @event;

        public override string ToString() => $"{_event}";
    }

    public class MethodDCEndILToNativeMapViewModel(MethodDCEndILToNativeMap @event) : IEventViewModel
    {
        private readonly MethodDCEndILToNativeMap _event = @event;

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"Method ID: {_event.MethodID}");
            stringBuilder.Append($" ReJIT ID: {_event.ReJITID} ");
            stringBuilder.AppendLine($" Byte extent: {_event.MethodExtent:b8}");
            stringBuilder.Append($"CLR instance ID: {_event.ClrInstanceID}");
            stringBuilder.AppendLine($" IL Version ID: {_event.ILVersionID}");
            stringBuilder.AppendLine($"Map entries: {_event.CountOfMapEntries}");
            for (int index = 0; index < _event.CountOfMapEntries; ++index)
            {
                var (ilOffset, nativeOffset) = (_event.ILOffsets[index], _event.NativeOffsets[index]);
                stringBuilder.AppendLine($"\tILOffset {ilOffset} NativeOffset {nativeOffset}");
            }
            return stringBuilder.ToString();
        }
    }
}

public sealed class StackViewModel(StackInfo stackInfo)
{
    public int Id { get; } = stackInfo.Id;
    public ImmutableArray<long> Addresses { get; } = stackInfo.Addresses;
}

public sealed class SequencePointBlockViewModel(SequencePointBlock block)
{
    public SequencePointBlock Block { get; } = block;
    public override string ToString() => Block.ToString();
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
    private IEnumerable<EventBlobViewModel> _filteredEventBlobs = [];
    private ImmutableArray<StackViewModel> _allStacks = [];
    private ImmutableArray<SequencePointBlockViewModel> _sequencePointBlock = [];

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
        SelectedEventTypes.ToObservableChangeSet()
            .Subscribe(_ => FilteredEventBlobs = FilterEventBlobs(_allEventBlobs, SelectedEventTypes));
    }

    public ICommand ReadFileCommand { get; }
    public ICommand BrowseFileCommand { get; }

    public string? FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public IEnumerable<EventBlobViewModel> FilteredEventBlobs
    {
        get => _filteredEventBlobs;
        private set => this.RaiseAndSetIfChanged(ref _filteredEventBlobs, value);
    }

    private ImmutableArray<System.Type> _eventTypes = [];
    public ImmutableArray<System.Type> EventTypes
    {
        get => _eventTypes;
        private set => this.RaiseAndSetIfChanged(ref _eventTypes, value);
    }

    public ObservableCollection<System.Type> SelectedEventTypes { get; } = [];

    public ImmutableArray<StackViewModel> AllStacks
    {
        get => _allStacks;
        private set => this.RaiseAndSetIfChanged(ref _allStacks, value);
    }

    public ImmutableArray<SequencePointBlockViewModel> AllSequencePointBlocks
    {
        get => _sequencePointBlock;
        private set => this.RaiseAndSetIfChanged(ref _sequencePointBlock, value);
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
        _allEventBlobs = metadataIsIncomplete ? [] : [.. file.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(_trace, blob, metadataCache[blob.MetadataId]))
        ];
        SelectedEventTypes.Clear();
        EventTypes = [.. _allEventBlobs.Select(e => e.RawEvent.GetType()).Distinct()];
        AllStacks = [.. file.StackBlocks
            .Select(sb => sb.Stacks
                .Select((s, i) => new StackViewModel(StackHelpers.BuildStackInfo(sb.FirstId + i, file.Trace.PointerSize, s))))
            .Aggregate((acc, ss) => acc.Concat(ss))
        ];
        AllSequencePointBlocks = [.. file.SequencePointBlocks.Select(spb => new SequencePointBlockViewModel(spb))];
    }

    private static IEnumerable<EventBlobViewModel> FilterEventBlobs(ImmutableArray<EventBlobViewModel> eventBlobs, IReadOnlyList<System.Type> eventTypes)
        => [.. eventTypes.Count == 0 ? eventBlobs : eventBlobs.Where(b => eventTypes.Contains(b.RawEvent.GetType()))];
}
