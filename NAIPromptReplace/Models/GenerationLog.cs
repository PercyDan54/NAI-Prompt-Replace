using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace NAIPromptReplace.Models;

public class GenerationLog
{
    public string Text { get; set; } = string.Empty;
    public IStorageFile? File { get; set; }
    public Bitmap? Thumbnail { get; set; }
    public Bitmap? Image { get; set; }
}
