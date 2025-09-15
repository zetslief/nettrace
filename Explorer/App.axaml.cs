using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        serviceCollection.
            AddSingleton<ViewLocator>()
            .AddSingleton<Navigator>()
            .AddSingleton<MainWindowViewModel>();
        serviceCollection
            .AddViewModel<NettraceReaderViewModel, NettraceReaderView>("Read")
            .AddViewModel<NettraceRecorderViewModel, NettraceRecorderView>("Record");
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DataTemplates.Add(serviceProvider.GetRequiredService<ViewLocator>());
            desktop.MainWindow = new MainWindow() { DataContext = viewModel };
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
