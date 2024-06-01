using System.Windows.Input;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class VibeTransferViewModel : ReferenceImageViewModel, IEquatable<VibeTransferViewModel>
{
    public VibeTransferViewModel()
    {
        Title = "Vibe Transfer";
    }

    public double ReferenceStrength
    {
        get => referenceStrength;
        set => this.RaiseAndSetIfChanged(ref referenceStrength, value);
    }

    public double ReferenceInformationExtracted
    {
        get => referenceInformationExtracted;
        set => this.RaiseAndSetIfChanged(ref referenceInformationExtracted, value);
    }

    public ICommand? RemoveSelfCommand { get; set; }
    public int Id { get; set; }
    public IDisposable? Subscription { get; set; }
    private double referenceInformationExtracted = 1;
    private double referenceStrength = 0.6;

    protected override void RemoveReferenceImage()
    {
        base.RemoveReferenceImage();
        RemoveSelfCommand?.Execute(this);
    }

    public bool Equals(VibeTransferViewModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((VibeTransferViewModel)obj);
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(VibeTransferViewModel? left, VibeTransferViewModel? right) => Equals(left, right);

    public static bool operator !=(VibeTransferViewModel? left, VibeTransferViewModel? right) => !Equals(left, right);
}
