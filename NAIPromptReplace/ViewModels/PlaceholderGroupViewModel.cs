using System.Windows.Input;
using NAIPromptReplace.Models;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class PlaceholderGroupViewModel : ReactiveObject, IEquatable<PlaceholderGroupViewModel>
{
    public static SelectionMethod[] SelectionMethods = Enum.GetValues<SelectionMethod>();
    
    private PlaceholderGroup group = new PlaceholderGroup();
    public PlaceholderGroup Group
    {
        get => group;
        set => this.RaiseAndSetIfChanged(ref group, value);
    }
    public ICommand? RemoveCommand { get; init; }
    
    public bool Equals(PlaceholderGroupViewModel? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Group == other.Group;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((PlaceholderGroup)obj);
    }

    public override int GetHashCode() => Group.GetHashCode();

    public static bool operator ==(PlaceholderGroupViewModel? left, PlaceholderGroupViewModel? right) => Equals(left, right);

    public static bool operator !=(PlaceholderGroupViewModel? left, PlaceholderGroupViewModel? right) => !Equals(left, right);
}
