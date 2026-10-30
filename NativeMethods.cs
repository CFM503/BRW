using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

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

        public const int INTERNET_COOKIE_HTTPONLY = 0x00002000;

        private const byte VK_SHIFT = 0x10;
        private const byte VK_Y = 0x59;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP = 0x0105;
        private const uint WM_MOUSEMOVE = 0x0200;

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool InternetGetCookieEx(
            string url,
            string cookieName,
            StringBuilder cookieData,
            ref int size,
            int flags,
            IntPtr pReserved);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

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
                // Keep system running and display on to prevent network sleep
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            }
            catch
            {
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

        /// <summary>
        /// Tier 1: Direct native C# background HTTP keepalive to refresh database ICX_SESSIONS timestamp
        /// </summary>
        public static bool SendNativeEbsPing()
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;

                string ebsUrl = "https://ebs.chinagasholdings.com/OA_HTML/OA.jsp?OAFunc=OANEWHOMEPAGE&_ka=" + Environment.TickCount;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ebsUrl);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                req.Timeout = 8000;

                int size = 4096;
                StringBuilder sb = new StringBuilder(size);
                if (InternetGetCookieEx("https://ebs.chinagasholdings.com/", null, sb, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                {
                    req.Headers.Add("Cookie", sb.ToString());
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return (resp.StatusCode == HttpStatusCode.OK);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tier 1: Direct native C# background HTTP keepalive for China Gas Portal
        /// </summary>
        public static void SendNativePortalPing()
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                string portalUrl = "https://portal.chinagasholdings.com/group/frontend?_ka=" + Environment.TickCount;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(portalUrl);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                req.Timeout = 6000;

                int size = 4096;
                StringBuilder sb = new StringBuilder(size);
                if (InternetGetCookieEx("https://portal.chinagasholdings.com/", null, sb, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                {
                    req.Headers.Add("Cookie", sb.ToString());
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Tier 2: Real Forms activity pulse down to SunAwtCanvas and SunAwtFrame message pump
        /// </summary>
        public static int PulseFormsActivity()
        {
            int pulsedCount = 0;
            try
            {
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    StringBuilder sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);
                    string title = sb.ToString();

                    StringBuilder sc = new StringBuilder(256);
                    GetClassName(hWnd, sc, 256);
                    string cls = sc.ToString();

                    if (!string.IsNullOrEmpty(title) &&
                       (title.IndexOf("Oracle Applications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Oracle Developer Forms", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Oracle Forms", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("EBSPROD", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // 1. Post to top-level SunAwtFrame
                        IntPtr pt1 = (IntPtr)((10 << 16) | 10);
                        IntPtr pt2 = (IntPtr)((10 << 16) | 11);
                        PostMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, pt1);
                        PostMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, pt2);

                        PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_SHIFT, IntPtr.Zero);
                        PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_SHIFT, (IntPtr)0xC0000001);

                        // 2. Also post to internal SunAwtCanvas controls
                        EnumChildWindows(hWnd, delegate (IntPtr childHwnd, IntPtr lChild)
                        {
                            StringBuilder csc = new StringBuilder(256);
                            GetClassName(childHwnd, csc, 256);
                            if (csc.ToString().IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                PostMessage(childHwnd, WM_MOUSEMOVE, IntPtr.Zero, pt1);
                                PostMessage(childHwnd, WM_MOUSEMOVE, IntPtr.Zero, pt2);
                            }
                            return true;
                        }, IntPtr.Zero);

                        // 3. Foreground micro pulse
                        keybd_event(VK_SHIFT, 0, 0, 0);
                        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);

                        pulsedCount++;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
            }
            return pulsedCount;
        }

        public static void EnableHighDpi()
        {
            try
            {
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                }
            }
        }
    }
}
