using System.Windows.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using ReactiveUI;
using SkiaSharp;

namespace NAIPromptReplace.ViewModels;

public class GenerationLogViewModel : ReactiveObject
{
    public GenerationLogViewModel()
    {
        SaveImageCommand = ReactiveCommand.CreateFromTask<bool>(saveImage);;
    }

    public GenerationLog GenerationLog { get; set; } = new GenerationLog();
    public ICommand SaveImageCommand { get; set; }
    public ICommand? DeleteImageCommand { get; set; }

    private static readonly FilePickerSaveOptions saveImageFilePickerOptions = new FilePickerSaveOptions
    {
        FileTypeChoices =
        [
            new FilePickerFileType("PNG")
            {
                Patterns = ["*.png"]
            }
        ]
    };

    private async Task saveImage(bool original)
    {
        if (App.StorageProvider == null)
            return;

        var file = await App.StorageProvider.SaveFilePickerAsync(saveImageFilePickerOptions);

        if (file == null)
            return;

        await Task.Run(async () =>
        {
            await using var fileStream = await file.OpenWriteAsync();
            await using var stream = original ? fileStream : new MemoryStream();
            GenerationLog.Image?.Save(stream);

            if (original)
                return;

            stream.Position = 0;
            using var removeImageAlpha = Util.RemoveImageAlpha(stream);
            removeImageAlpha.Encode(fileStream, SKEncodedImageFormat.Png, 100);
        });
    }
}
