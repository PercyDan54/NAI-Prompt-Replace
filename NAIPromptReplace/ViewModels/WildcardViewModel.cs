using System.Windows.Input;
using NAIPromptReplace.Models;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class WildcardViewModel : ReactiveObject, IEquatable<WildcardViewModel>
{
    public static SelectionMethod[] SelectionMethods = Enum.GetValues<SelectionMethod>();

    private Wildcard wildcard = new Wildcard();
    public Wildcard Wildcard
    {
        get => wildcard;
        set => this.RaiseAndSetIfChanged(ref wildcard, value);
    }
    public ICommand? RemoveCommand { get; init; }
    
    public bool Equals(WildcardViewModel? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Wildcard == other.Wildcard;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Wildcard)obj);
    }

    public override int GetHashCode() => Wildcard.GetHashCode();

    public static bool operator ==(WildcardViewModel? left, WildcardViewModel? right) => Equals(left, right);

    public static bool operator !=(WildcardViewModel? left, WildcardViewModel? right) => !Equals(left, right);
}
