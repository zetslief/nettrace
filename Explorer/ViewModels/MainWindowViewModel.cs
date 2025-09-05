using ReactiveUI;

namespace Explorer.ViewModels;

public class MainWindowViewModel(
    NettraceReaderViewModel readerViewModel,
    NettraceRecorderViewModel recorderViewModel) : ReactiveObject
{
    public NettraceReaderViewModel ReaderViewModel { get; } = readerViewModel;
    public NettraceRecorderViewModel RecorderViewModel { get; } = recorderViewModel;
}
