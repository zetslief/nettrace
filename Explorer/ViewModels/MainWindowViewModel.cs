using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Explorer.Controls;
using ReactiveUI;

namespace Explorer.ViewModels;

public interface IViewModel { }

public sealed record ViewModelDescription(string Title, IViewModel ViewModel);

public class MainWindowViewModel : ReactiveObject
{
    private ViewModelDescription? _selectedViewDescription;

    public MainWindowViewModel(IEnumerable<ViewModelDescription> viewModels, Navigator navigator)
    {
        ViewDescriptions = [.. viewModels];
        _selectedViewDescription = ViewDescriptions.Single(vm => vm.ViewModel.GetType() == typeof(NettraceRecorderViewModel));
        navigator.OnNavigation += (s, e) =>
        {
            var viewModelDescription = ViewDescriptions.SingleOrDefault(vm => vm.ViewModel.GetType() == e.ViewModelType)
                ?? throw new InvalidOperationException($"Failed to find view model description for {e}");
            SelectedViewDescription = viewModelDescription;
        };
    }

    public IEnumerable<ViewModelDescription> ViewDescriptions { get; }

    public ViewModelDescription? SelectedViewDescription
    {
        get => _selectedViewDescription;
        set => this.RaiseAndSetIfChanged(ref _selectedViewDescription, value);
    }
}
