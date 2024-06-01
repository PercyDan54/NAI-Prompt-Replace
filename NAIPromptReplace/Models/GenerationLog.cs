using Avalonia.Media.Imaging;

namespace NAIPromptReplace.Models;

public class GenerationLog
{
    public string Text { get; set; } = string.Empty;
    public Bitmap? Image { get; set; }
}
