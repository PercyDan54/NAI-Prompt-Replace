using System.Text.Json.Serialization;
using Avalonia;

namespace NAIPromptReplace.Models;

public class V4Prompt
{
    public V4Caption Caption { get; set; } = new V4Caption();
}

public class V4Caption
{
    public string BaseCaption { get; set; } = string.Empty;
    public V4CharCaption[] CharCaptions { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseCoords { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseOrder { get; set; }
}

public class V4CharPrompt
{
    public string Prompt { get; set; } = string.Empty;
    public string Uc { get; set; } = string.Empty;
    public Point Center { get; set; } = new Point(0.5, 0.5);
}

public class V4CharCaption
{
    public string CharCaption { get; set; } = string.Empty;
    public Point[] Centers { get; set; } = [new Point(0.5, 0.5)];
}
