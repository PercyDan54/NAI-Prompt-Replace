using System.Runtime.InteropServices;

namespace NAIPromptReplace.Desktop.Platform.Windows;

public static class TaskbarFlasher
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private const uint FLASHW_STOP = 0;
    private const uint FLASHW_CAPTION = 0x00000001;
    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_ALL = FLASHW_CAPTION | FLASHW_TRAY;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;

    public static void FlashTaskbar(IntPtr hwnd, bool enable)
    {
        FLASHWINFO fi = new()
        {
            cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
            hwnd = hwnd,
            dwFlags = enable ? FLASHW_TRAY | FLASHW_TIMERNOFG : FLASHW_STOP,
            uCount = uint.MaxValue,
            dwTimeout = 0
        };

        FlashWindowEx(ref fi);
    }
}
