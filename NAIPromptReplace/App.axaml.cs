using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace NAIPromptReplace;

public partial class App : Application
{
    public static IStorageProvider? StorageProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
