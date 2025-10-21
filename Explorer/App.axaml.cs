using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Explorer.Controls;
using Explorer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ViewLocator = Explorer.Controls.ViewLocator;

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
        serviceCollection.AddSingleton<NettraceParser>();
        serviceCollection
            .AddSingleton<ViewLocator>()
            .AddSingleton<Navigator>()
            .AddSingleton<MainWindowViewModel>();
        serviceCollection
            .AddViewModel<NettraceReaderViewModel, NettraceReaderView>("Read")
            .AddViewModel<NettraceRecorderViewModel, NettraceRecorderView>("Record");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            serviceCollection.AddSingleton<IStorageProvider>(_ => mainWindow.StorageProvider);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            DataTemplates.Add(serviceProvider.GetRequiredService<ViewLocator>());
            desktop.MainWindow = mainWindow;
            mainWindow.DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class RegistrationHelpers
{
    public static IServiceCollection AddViewModel<TViewModel, TView>(this IServiceCollection serviceCollection, string title)
        where TViewModel : class, IViewModel
        where TView : class
    {
        serviceCollection.AddSingleton<TView>();
        serviceCollection.AddSingleton<TViewModel>();
        serviceCollection.AddSingleton<IViewModel, TViewModel>();
        serviceCollection.AddSingleton(sp => new ViewModelDescription(title, sp.GetRequiredService<TViewModel>()));
        serviceCollection.AddSingleton(new ViewDescription(title, typeof(TViewModel)));
        return serviceCollection;
    }
}
