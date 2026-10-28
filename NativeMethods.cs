using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EBSBrowser
{
    internal static class NativeMethods
    {
        public const int INTERNET_OPTION_END_BROWSER_SESSION = 42;
        public const int OLECMDID_OPTICAL_ZOOM = 63;
        public const int OLECMDEXECOPT_DONTPROMPTUSER = 2;

        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;
        public const uint ES_CONTINUOUS = 0x80000000;

        private const byte VK_SHIFT = 0x10;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int awareness);

        public static void PreventSleep()
        {
            try
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
            }
            catch
            {
            }
        }

        public static void PulseFormsActivity()
        {
            try
            {
                bool foundForms = false;
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);
                    string title = sb.ToString();
                    if (!string.IsNullOrEmpty(title) &&
                       (title.IndexOf("Oracle Applications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Oracle Developer Forms", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Oracle Forms", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        foundForms = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (foundForms)
                {
                    // Emit a microscopic, harmless key state refresh to reset Java Forms idle timer
                    keybd_event(VK_SHIFT, 0, 0, 0);
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch
            {
            }
        }

        public static void EnableHighDpi()
        {
            try
            {
                // Try Shcore.dll SetProcessDpiAwareness (Windows 8.1+)
                // 1 = Process_System_DPI_Aware, 2 = Process_Per_Monitor_DPI_Aware
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try
                {
                    // Fallback to User32.dll SetProcessDPIAware (Windows Vista+)
                    SetProcessDPIAware();
                }
                catch
                {
                    // Ignore on older systems
                }
            }
        }

        public static void ClearSession()
        {
            try
            {
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_END_BROWSER_SESSION, IntPtr.Zero, 0);
            }
            catch
            {
            }
        }

        public static void CleanZombieJavaProcesses()
        {
            try
            {
                string[] processNames = new string[] { "jp2launcher", "java", "javaw" };
                foreach (string name in processNames)
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    foreach (Process p in procs)
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
