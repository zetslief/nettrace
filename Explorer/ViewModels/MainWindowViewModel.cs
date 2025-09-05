using ReactiveUI;
using System.Collections.Generic;

namespace Explorer.ViewModels;

public record ViewDescription(string Title, ReactiveObject ViewModel);

public class MainWindowViewModel(
    NettraceReaderViewModel readerViewModel,
    NettraceRecorderViewModel recorderViewModel) : ReactiveObject
{
    private ViewDescription? _selectedViewDescription = new("Record", recorderViewModel);

    public IEnumerable<ViewDescription> ViewDescriptions { get; } = [
        new("Record", recorderViewModel),
        new("Read", readerViewModel),
    ];

    public ViewDescription? SelectedViewDescription
    {
        get => _selectedViewDescription;
        set => this.RaiseAndSetIfChanged(ref _selectedViewDescription, value);
    }
}
