using System;
using Avalonia.Controls;

namespace Explorer.Controls;

public partial class NettraceReaderView : UserControl
{
    public NettraceReaderView()
    {
        InitializeComponent();
    }
}

public class BoxedItem : ContentControl
{
}

public class BoxedItemsControl : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new BoxedItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<BoxedItem>(item, out recycleKey);
}
