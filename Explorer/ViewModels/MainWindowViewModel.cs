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

public class MetadataBlockViewModel(Block<MetadataEvent> metadata)
{
    private readonly Block<MetadataEvent> metadata = metadata;

    public Header Header => metadata.Header;

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

    private IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks = null;
    private MetadataBlockViewModel? selectedMetadataBlock = null; 

    private readonly ObservableAsPropertyHelper<IEnumerable<MetadataEventBlobViewModel>?> metadataEventBlobs;
    private MetadataEventBlobViewModel? selectedMetadataEventBlob = null; 
    private EventBlobViewModel[]? allEventBlobs; 

    private readonly ObservableAsPropertyHelper<IReadOnlyCollection<EventBlobViewModel>?> eventBlobs;
    private readonly ObservableAsPropertyHelper<LabeledRange[]?> timePoints;

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

    public LabeledRange[]? TimePoints => timePoints.Value;

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
        allEventBlobs = nettrace.EventBlocks
            .SelectMany(block => block.EventBlobs)
            .Select(blob => new EventBlobViewModel(blob))
            .ToArray();
        MetadataBlocks = nettrace.MetadataBlocks
            .Select(block => new MetadataBlockViewModel(block))
            .ToArray();
        Status = $"Read {stream.Position} bytes";
    }

    private static LabeledRange[]? ToLabeledRanges(IReadOnlyCollection<MetadataBlockViewModel>? metadataBlocks, IReadOnlyCollection<EventBlobViewModel>? eventBlobs)
    {
        if (eventBlobs?.Count > 1)
        {
            var result = new LabeledRange[eventBlobs.Count - 1];
            BlobsToRanges(eventBlobs, result.AsSpan());
            return result;
        }
        return null;
    }

    private static void BlobsToRanges(IReadOnlyCollection<EventBlobViewModel> blobs, Span<LabeledRange> output)
    {
        DateTime? previousTime = null;
        double previousValue = 0;
        int index = 0;
        foreach (var blob in blobs.OrderBy(blob => blob.Blob.TimeStamp))
        {
            if (previousTime is null)
            {
                previousTime = DateTime.FromFileTime(blob.Blob.TimeStamp);
            }
            else
            {
                var value = (DateTime.FromFileTime(blob.Blob.TimeStamp) - previousTime.Value).TotalMilliseconds;
                output[index] = new LabeledRange("", new(previousValue, value)); 
                previousValue = value;
                ++index;
            }
        }
    }
}