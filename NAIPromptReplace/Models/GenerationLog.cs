using Avalonia.Media.Imaging;

namespace NAIPromptReplace.Models;

public class GenerationLog
{
    public string Text { get; set; } = string.Empty;
    public Bitmap? Thumbnail { get; set; }
    public Bitmap? Image { get; set; }
}
