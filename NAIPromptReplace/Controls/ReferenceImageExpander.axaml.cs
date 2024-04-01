using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace NAIPromptReplace.Controls;

public class ReferenceImageExpander : ContentControl
{
    private static readonly FilePickerOpenOptions anyFilePickerOptions = new FilePickerOpenOptions
    {
        FileTypeFilter =
        [
            new FilePickerFileType("Any")
            {
                Patterns = ["*.*"]
            }
        ]
    };

    
    public static readonly StyledProperty<string?> ImagePathProperty = AvaloniaProperty.Register<ReferenceImageExpander, string?>(nameof(ImagePath), null, true);
    
    public string? ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }
    
    public static readonly StyledProperty<byte[]?> ImageDataProperty = AvaloniaProperty.Register<ReferenceImageExpander, byte[]?>(nameof(ImageData), null, true);
    
    public byte[]? ImageData
    {
        get => GetValue(ImageDataProperty);
        set => SetValue(ImageDataProperty, value);
    }
    
    #region ExpanderName
    public static readonly StyledProperty<string> ExpanderNameProperty = AvaloniaProperty.Register<ReferenceImageExpander, string>(nameof(ExpanderName));

    public string ExpanderName
    {
        get => GetValue(ExpanderNameProperty);
        set => SetValue(ExpanderNameProperty, value);
    }
    #endregion

    private Image ReferenceImage = null!;
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        AddHandler(DragDrop.DropEvent, onDrop);
        ReferenceImage = e.NameScope.Find<Image>("ReferenceImage");
        var btn = e.NameScope.Find<Button>("BrowseRefImageButton");
        btn?.AddHandler(Button.ClickEvent, BrowseRefImageButton_OnClick);
        btn = e.NameScope.Find<Button>("RemoveRefImageButton");
        btn?.AddHandler(Button.ClickEvent, RemoveRefImageButton_OnClick);
        loadReferenceImage();
    }
    
    private async void BrowseRefImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(anyFilePickerOptions);

        if (files.Count == 0)
            return;

        var file = files[0];
        using var stream = await file.OpenReadAsync();
        using var stream1 = new MemoryStream();
        await stream.CopyToAsync(stream1);
        ImageData = stream1.ToArray();

        if (!OperatingSystem.IsAndroid())
        {
            ImagePath = file.TryGetLocalPath();
            loadReferenceImage();
        }
        else
        {
            stream.Position = 0;
            using var im = SKImage.FromEncodedData(stream);

            if (im != null)
            {
                stream.Position = 0;
                ReferenceImage.Source = new Bitmap(stream);
            }
        }
    }

    private void onDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();

            foreach (var item in files)
            {
                if (item is IStorageFile file)
                {
                    ImagePath = file.Path.LocalPath;
                }
            }

            loadReferenceImage();
            e.Handled = true;
        }
    }

    private void loadReferenceImage()
    {
        string? file = ImagePath;

        if (!File.Exists(file))
        {
            ImagePath = null;
            ReferenceImage.Source = null;
            return;
        }

        using var fileStream = File.OpenRead(file);
        using var im = SKImage.FromEncodedData(fileStream);
        fileStream.Position = 0;

        if (im != null)
        {
            fileStream.Position = 0;
            ReferenceImage.Source = new Bitmap(fileStream);
            ImagePath = file;
        }
    }

    private void removeReferenceImage()
    {
        ImagePath = null;
        loadReferenceImage();
    }

    private void RemoveRefImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        removeReferenceImage();
    }
}