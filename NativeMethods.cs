using System;
using System.Runtime.InteropServices;

namespace EBSBrowser
{
    internal static class NativeMethods
    {
        public const int INTERNET_OPTION_END_BROWSER_SESSION = 42;
        public const int OLECMDID_OPTICAL_ZOOM = 63;
        public const int OLECMDEXECOPT_DONTPROMPTUSER = 2;

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int awareness);

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
    }
}
