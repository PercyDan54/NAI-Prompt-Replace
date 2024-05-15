using System.Windows.Input;
using ReactiveUI;

namespace NAIPromptReplace.ViewModels;

public class VibeTransferViewModel : ReferenceImageViewModel
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

    public int Id;
    private double referenceInformationExtracted = 1;
    private double referenceStrength = 1;

    protected override void RemoveReferenceImage()
    {
        base.RemoveReferenceImage();
        RemoveSelfCommand?.Execute(Id);
    }
}