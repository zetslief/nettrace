using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Explorer.ViewModels;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Explorer.Controls;

public sealed record ViewDescription(string Title, Type ViewModelType);

public sealed class Navigator(
    ILogger<Navigator> logger,
    IEnumerable<ViewDescription> viewDescriptions)
{
    private readonly ILogger<Navigator> _logger = logger;
    private readonly ImmutableArray<ViewDescription> _viewDescriptions = [.. viewDescriptions];

    public event EventHandler<ViewDescription>? OnNavigation;

    public void NavigateToViewModel<T>() where T : ReactiveObject
    {
        var viewDescription = _viewDescriptions.SingleOrDefault(v => v.ViewModelType == typeof(T));
        if (viewDescription is null)
            throw new InvalidOperationException($"Failed to navigate to view model of type: '{typeof(T)}'");
        _logger.LogInformation("Navigating to {ViewDescription}", viewDescription);
        OnNavigation?.Invoke(this, viewDescription);
    }
}

public sealed class ViewLocator(IServiceProvider serviceProvider) : IDataTemplate
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ImmutableDictionary<Type, Type> _viewModelToViewMap = ImmutableDictionary.CreateRange<Type, Type>([
        new(typeof(NettraceReaderViewModel), typeof(NettraceReaderView)),
        new(typeof(NettraceRecorderViewModel), typeof(NettraceRecorderView)),
    ]);

    public Control? Build(object? param)
    {
        if (param is null) return CreateErrorTextBlock("View Locator - param is null");
        var type = param.GetType();

        if (_viewModelToViewMap.TryGetValue(type, out Type? viewType)
            && _serviceProvider.GetService(viewType) is Control view)
        {
            return view;
        }

        return CreateErrorTextBlock($"View Locator - Failed to resolve view model: '{param}'. View type: '{viewType}'.");
    }

    public bool Match(object? data) => data is ReactiveObject;

    private static TextBlock CreateErrorTextBlock(string error) => new() { Text = error };
}
