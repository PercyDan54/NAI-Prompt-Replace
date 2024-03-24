using Avalonia.Controls.ApplicationLifetimes;
using NAIPromptReplace.Android.Views;

namespace NAIPromptReplace.Android;

public class AndroidApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new AndroidMainView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
