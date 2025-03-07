using System.Text.Json.Serialization;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;

namespace NAIPromptReplace.Models;

public class V4Prompt
{
    public V4Caption Caption { get; set; } = new V4Caption();
}

public class V4Caption
{
    public string BaseCaption { get; set; } = string.Empty;
    public List<V4CharCaption> CharCaptions { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseCoords { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseOrder { get; set; }
}

public class V4CharPrompt : ReactiveObject, IEquatable<V4CharPrompt>
{
    private string prompt = string.Empty;
    private string uc = string.Empty;
    private Point center = new Point(0.5, 0.5);

    [JsonIgnore]
    public int Id { get; set; }

    [JsonIgnore] 
    public string Title => $"Character {Id}";

    [JsonIgnore]
    public ICommand? MoveUpCommand { get; set; }

    [JsonIgnore]
    public ICommand? MoveDownCommand { get; set; }

    [JsonIgnore]
    public ICommand? RemoveSelfCommand { get; set; }

    public string Prompt
    {
        get => prompt;
        set => this.RaiseAndSetIfChanged(ref prompt, value);
    }

    public string Uc
    {
        get => uc;
        set => this.RaiseAndSetIfChanged(ref uc, value);
    }

    public Point Center
    {
        get => center;
        set => this.RaiseAndSetIfChanged(ref center, value);
    }

    public bool Equals(V4CharPrompt? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((V4CharPrompt)obj);
    }

    public override int GetHashCode() => Id;
}

public class V4CharCaption
{
    public string CharCaption { get; set; } = string.Empty;
    public Point[] Centers { get; set; } = [new Point(0.5, 0.5)];
}
