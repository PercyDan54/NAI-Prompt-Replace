using System.Runtime.InteropServices;

namespace NAIPromptReplace.Platform.Windows;

public static class TaskbarProgress
{
    public enum TaskbarStates
    {
        NoProgress = 0,
        Indeterminate = 1,
        Normal = 2,
        Error = 4,
        Paused = 8
    }

    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    private static readonly ITaskbarList3? taskbarInstance;

    static TaskbarProgress()
    {
        try
        {
            Type? taskbarType = Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"));
            if (taskbarType != null)
            {
                taskbarInstance = (ITaskbarList3)Activator.CreateInstance(taskbarType)!;
                taskbarInstance.HrInit();
            }
        }
        catch
        {
        }
    }

    public static void SetState(IntPtr hwnd, TaskbarStates state)
    {
        taskbarInstance?.SetProgressState(hwnd, state);
    }

    public static void SetProgress(IntPtr hwnd, ulong completed, ulong total)
    {
        taskbarInstance?.SetProgressValue(hwnd, completed, total);
    }
}
