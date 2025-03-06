namespace NAIPromptReplace.Platform.Windows;

public class WindowsProgressNotifier : IPlatformProgressNotifier
{
    private IntPtr hwnd;

    public WindowsProgressNotifier(IntPtr hwnd)
    {
        this.hwnd = hwnd;
    }

    public void SetProgress(int completed, int total)
    {
        if (hwnd == IntPtr.Zero)
            return;

        TaskbarProgress.SetState(hwnd, TaskbarProgress.TaskbarStates.Normal);
        TaskbarProgress.SetProgress(hwnd, (ulong)completed, (ulong)total);
    }

    public void NotifyCompleted(bool error)
    {
        if (hwnd == IntPtr.Zero)
            return;

        TaskbarProgress.SetState(hwnd, error ? TaskbarProgress.TaskbarStates.Error : TaskbarProgress.TaskbarStates.NoProgress);
        TaskbarFlasher.FlashTaskbar(hwnd, true);
    }
}
