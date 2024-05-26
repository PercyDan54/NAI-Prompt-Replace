using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;
using SkiaSharp;

namespace NAIPromptReplace.ViewModels;

public class ReferenceImageViewModel : ReactiveObject
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

    private string title = string.Empty;
    private IImage? image;
    private object? content;
    private string? imagePath;
    private byte[]? imageData;
    private string expanderText = string.Empty;
    private string imagePathText = string.Empty;
    private bool strictImageFile;

    public string Title
    {
        get => title;
        set
        {
            this.RaiseAndSetIfChanged(ref title, value);
            updateText();
        }
    }

    public string ExpanderText
    {
        get => expanderText;
        set => this.RaiseAndSetIfChanged(ref expanderText, value);
    }

    public string ImagePathText
    {
        get => imagePathText;
        set => this.RaiseAndSetIfChanged(ref imagePathText, value);
    }

    public byte[]? ImageData
    {
        get => imageData;
        set => this.RaiseAndSetIfChanged(ref imageData, value);
    }

    public string? ImagePath
    {
        get => imagePath;
        set
        {
            this.RaiseAndSetIfChanged(ref imagePath, value);
            updateText();
        }
    }

    public object? Content
    {
        get => content;
        set => this.RaiseAndSetIfChanged(ref content, value);
    }

    public IImage? Image
    {
        get => image;
        set => this.RaiseAndSetIfChanged(ref image, value);
    }

    public bool StrictImageFile
    {
        get => strictImageFile;
        set => this.RaiseAndSetIfChanged(ref strictImageFile, value);
    }

    public ICommand BrowseCommand { get; set; }
    public ICommand RemoveCommand { get; set; }

    public ReferenceImageViewModel()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(browseReferenceImage);
        RemoveCommand = ReactiveCommand.Create(RemoveReferenceImage);
        updateText();
    }

    private async Task browseReferenceImage()
    {
        var storageProvider = App.StorageProvider;

        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(anyFilePickerOptions);

        if (files.Count == 0)
            return;

        var file = files[0];
        await SetReferenceImage(file);
    }

    public async Task SetReferenceImage(IStorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        using var im = SKImage.FromEncodedData(memoryStream);

        if (im != null)
        {
            memoryStream.Position = 0;
            Image = new Bitmap(memoryStream);
        }

        if (im != null || !StrictImageFile)
        {
            memoryStream.Position = 0;
            ImagePath = file.TryGetLocalPath();
            ImageData = memoryStream.ToArray();
        }
    }

    private void updateText()
    {
        if (string.IsNullOrEmpty(ImagePath))
        {
            ExpanderText = Title;
            ImagePathText = "Select Image";
            return;
        }

        ExpanderText = $"{Title} ({Util.TruncateString(Path.GetFileName(ImagePath), 32)})";
        ImagePathText = $"{Util.TruncateString(ImagePath, 40)}";
    }

    protected virtual void RemoveReferenceImage()
    {
        ImagePath = null;
        ImageData = null;
        Image = null;
    }
}
