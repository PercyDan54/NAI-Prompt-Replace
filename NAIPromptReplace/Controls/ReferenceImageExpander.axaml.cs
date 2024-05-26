using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Controls;

public class ReferenceImageExpander : ContentControl
{
    public static readonly StyledProperty<string?> ImagePathProperty = AvaloniaProperty.Register<ReferenceImageExpander, string?>(nameof(ImagePath));
    public static readonly StyledProperty<IImage?> ImageProperty = AvaloniaProperty.Register<ReferenceImageExpander, IImage?>(nameof(Image));
    public static readonly StyledProperty<ICommand?> BrowseCommandProperty = AvaloniaProperty.Register<ReferenceImageExpander, ICommand?>(nameof(BrowseCommand));
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty = AvaloniaProperty.Register<ReferenceImageExpander, ICommand?>(nameof(RemoveCommand));

    public string? ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }
    
    public static readonly StyledProperty<byte[]?> ImageDataProperty = AvaloniaProperty.Register<ReferenceImageExpander, byte[]?>(nameof(ImageData), null, true);
    
    public byte[]? ImageData
    {
        get => GetValue(ImageDataProperty);
        set => SetValue(ImageDataProperty, value);
    }

    public ICommand? BrowseCommand
    {
        get => GetValue(BrowseCommandProperty);
        set => SetValue(BrowseCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    #region ExpanderName
    public static readonly StyledProperty<string> ExpanderNameProperty = AvaloniaProperty.Register<ReferenceImageExpander, string>(nameof(ExpanderName));

    public string ExpanderName
    {
        get => GetValue(ExpanderNameProperty);
        set => SetValue(ExpanderNameProperty, value);
    }
    #endregion

    public ReferenceImageExpander()
    {
        AddHandler(DragDrop.DropEvent, onDrop);
    }

    private async void onDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.Handled = true;
            var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();

            foreach (var item in files)
            {
                if (item is IStorageFile file)
                {
                    await ((ReferenceImageViewModel)DataContext).SetReferenceImage(file)!;
                }
            }
        }
    }
}
