using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NAIPromptReplace.Models;
using NAIPromptReplace.ViewModels;
using NAIPromptReplace.Views;

namespace NAIPromptReplace;

public partial class App : Application
{
    protected const string CONFIG_FILE = "config.json";
    protected virtual string ConfigPath => CONFIG_FILE;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        loadConfig();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var mainView = new MainView();
            var vm = new MainViewViewModel(mainWindow.StorageProvider)
            {
                Config = loadConfig()
            };
            mainWindow.DataContext = vm;
            mainWindow.MainView = mainView;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Config loadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }

        return new Config();
    }
}
