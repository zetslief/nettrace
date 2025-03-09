using ReactiveUI;

namespace Explorer.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    public NettraceReaderViewModel ReaderViewModel { get; } = new();
}
