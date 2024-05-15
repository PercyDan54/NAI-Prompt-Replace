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
            stream1.Position = 0;
            using var im = SKImage.FromEncodedData(stream1);

            if (im != null)
            {
                stream1.Position = 0;
                Image = new Bitmap(stream1);
            }
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

        ExpanderText = $"{Title} ({Util.TruncateString(ImagePath, 32)})";
        ImagePathText = $"{Util.TruncateString(ImagePath, 40)}";
    }

    private void loadReferenceImage()
    {
        string? file = ImagePath;

        if (!File.Exists(file))
        {
            ImagePath = null;
            Image = null;
            return;
        }

        using var fileStream = File.OpenRead(file);
        using var im = SKImage.FromEncodedData(fileStream);
        fileStream.Position = 0;

        if (im != null)
        {
            fileStream.Position = 0;
            Image = new Bitmap(fileStream);
        }
    }

    protected virtual void RemoveReferenceImage()
    {
        ImagePath = null;
        ImageData = null;
        Image = null;
    }
}
