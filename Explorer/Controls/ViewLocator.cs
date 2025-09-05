using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using System;
using System.Collections.Immutable;
using Explorer.ViewModels;

namespace Explorer.Controls;

public class ViewLocator(IServiceProvider serviceProvider) : IDataTemplate
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

    private TextBlock CreateErrorTextBlock(string error) => new() { Text = error };
}
