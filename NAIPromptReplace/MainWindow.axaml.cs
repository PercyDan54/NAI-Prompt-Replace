using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainView.AddHandler(DragDrop.DropEvent, onDrop);
    }

    private async void onDrop(object? sender, DragEventArgs e)
    {
        if (!e.Handled && e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();

            foreach (var item in files)
            {
                if (item is IStorageFile file)
                {
                    await ((MainViewModel)MainView.DataContext).OpenFile(file);
                }
            }
        }
    }

    private void TopLevel_OnClosed(object? sender, EventArgs e)
    {
        MainView.SaveConfig();
    }
}
