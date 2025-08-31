using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Explorer.ViewModels;
using ReactiveUI;

namespace Explorer;

public partial class App : Application
{
    public override void Initialize()
    {
        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(exception =>
        {
            Dispatcher.UIThread.Post(() => throw new UnhandledErrorException("Unhandled exception was thrown.", exception),
                DispatcherPriority.Send);
        });
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow()
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
