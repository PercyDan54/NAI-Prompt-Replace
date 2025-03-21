using System.Numerics;
using System.Text.Json.Serialization;
using System.Windows.Input;
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
    private string uc = "lowres, aliasing, ";
    private int id;
    private float x = 0.5f;
    private float y = 0.5f;

    [JsonIgnore]
    public int Id
    {
        get => id;
        set
        {
            this.RaiseAndSetIfChanged(ref id, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    [JsonIgnore] 
    public string Title => $"Character {Id}";

    [JsonIgnore]
    public ICommand? MoveUpCommand { get; set; }

    [JsonIgnore]
    public ICommand? MoveDownCommand { get; set; }

    [JsonIgnore]
    public ICommand? RemoveSelfCommand { get; set; }

    public bool Enabled { get; set; } = true;

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

    public float X
    {
        get => x;
        set => this.RaiseAndSetIfChanged(ref x, value);
    }

    public float Y
    {
        get => y;
        set => this.RaiseAndSetIfChanged(ref y, value);
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
    public Vector2[] Centers { get; set; } = [new Vector2(0.5f)];
}
