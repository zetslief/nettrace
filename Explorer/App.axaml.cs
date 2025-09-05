using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Explorer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole());
        serviceCollection.AddSingleton<NettraceReaderViewModel>();
        serviceCollection.AddSingleton<NettraceRecorderViewModel>();
        serviceCollection.AddSingleton<MainWindowViewModel>();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow()
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
