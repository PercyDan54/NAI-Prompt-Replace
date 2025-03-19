using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NAIPromptReplace.Desktop.Platform.Windows;
using NAIPromptReplace.ViewModels;

namespace NAIPromptReplace.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainView.AddHandler(DragDrop.DropEvent, onDrop);

        if (MainView.DataContext is MainViewModel vm)
        {
            if (OperatingSystem.IsWindows())
            {
                IntPtr? hwnd = GetTopLevel(this)?.TryGetPlatformHandle()?.Handle;

                if (hwnd.HasValue)
                    vm.PlatformProgressNotifier = new WindowsProgressNotifier(hwnd.Value);
            }
        }
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
