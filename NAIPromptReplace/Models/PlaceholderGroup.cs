using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NAIPromptReplace.Models;

public class PlaceholderGroup : INotifyPropertyChanged, IEquatable<PlaceholderGroup>
{
    private string name = "Unnamed";
    public string Name
    {
        get => name;
        set
        {
            if (value == name)
                return;

            name = value;
            OnPropertyChanged();
        }
    }

    public string Keyword { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int RandomBrackets { get; set; }

    public int RandomBracketsMax { get; set; }

    public double MultipleProb { get; set; } = 0.5;

    public int MultipleNum { get; set; } = 2;

    [JsonIgnore]
    public int SingleSequentialNum { get; set; }

    public bool Shuffled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<SelectionMethod>))]
    public SelectionMethod SelectionMethod { get; set; }

    public bool Equals(PlaceholderGroup? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Name == other.Name && Keyword == other.Keyword;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != this.GetType())
            return false;
        return Equals((PlaceholderGroup)obj);
    }

    public override int GetHashCode() => HashCode.Combine(Name, Keyword);

    public static bool operator ==(PlaceholderGroup? left, PlaceholderGroup? right) => Equals(left, right);

    public static bool operator !=(PlaceholderGroup? left, PlaceholderGroup? right) => !Equals(left, right);
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
    
public enum SelectionMethod
{
    All,
    SingleSequential,
    MultipleNum,
    MultipleProb
}
