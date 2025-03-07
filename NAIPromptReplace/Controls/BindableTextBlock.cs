using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace NAIPromptReplace.Controls;

public class BindableTextBlock : SelectableTextBlock
{
    public ObservableCollection<Run> RunList
    {
        get => GetValue(RunListProperty);
        set => SetValue(RunListProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<Run>> RunListProperty = AvaloniaProperty.Register<BindableTextBlock, ObservableCollection<Run>>(nameof(RunList));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == RunListProperty)
        {
            if (e.OldValue is ObservableCollection<Run> oldItems)
            {
                oldItems.CollectionChanged -= OnCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<Run> newItems)
            {
                Inlines.Clear();
                Inlines.AddRange(newItems);
                newItems.CollectionChanged += OnCollectionChanged;
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Inlines.Clear();
        Inlines.AddRange((ObservableCollection<Run>)sender);
    }
}
