using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Models;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Views;

public partial class MainView : LayoutTransformControl
{
    protected IStorageProvider? StorageProvider => TopLevel.GetTopLevel(this)?.StorageProvider;

    protected const string CONFIG_FILE = "config.json";
    protected virtual string ConfigPath => CONFIG_FILE;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DataContext = new MainViewModel(StorageProvider)
        {
            Config = LoadConfig()
        };
    }

    private void ReplacementDataGrid_OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Width = DataGridLength.Auto;
    }

    protected Config LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }

        return new Config();
    }
}
