using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NAIPromptReplace.Models;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Views;

public partial class MainView : LayoutTransformControl
{
    protected const string CONFIG_FILE = "config.json";
    protected virtual string ConfigPath => CONFIG_FILE;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        App.StorageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        InitializeDataContext();
    }

    protected virtual void InitializeDataContext()
    {
        DataContext = new MainViewModel
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

    public void SaveConfig()
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(((MainViewModel)DataContext).Config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
        }
    }
}
