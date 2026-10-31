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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessDEPPolicy(uint dwFlags);

        public static void DisableDEP()
        {
            try
            {
                // Disables hardware Data Execution Prevention (DEP) for this 32-bit process,
                // allowing Java 6 JIT code generation and native thunks to run without OS faults.
                SetProcessDEPPolicy(0);
            }
            catch
            {
            }
        }

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
                // Use PROCESS_SYSTEM_DPI_AWARE (1) instead of PROCESS_PER_MONITOR_DPI_AWARE (2).
                // Java 6 (jre6) was built in 2011 and crashes with EXCEPTION_ACCESS_VIOLATION in awt.dll
                // when receiving modern Windows 10/11 PerMonitorV2 WM_DPICHANGED messages on high-DPI laptops.
                // System DPI awareness keeps the UI sharp and clear while keeping Java 6 completely stable.
                SetProcessDpiAwareness(1);
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

        #region Native Crash Interceptor & ExitProcess Protector

        public static volatile bool AllowProcessExit = false;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr AddVectoredExceptionHandler(uint First, VectoredHandlerCallback Handler);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int VectoredHandlerCallback(IntPtr ExceptionInfo);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetModuleHandleEx(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

        [DllImport("kernel32.dll")]
        private static extern void ExitProcess(uint uExitCode);

        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 4;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExitProcessDelegate(uint uExitCode);

        private static VectoredHandlerCallback vehCallback;
        private static ExitProcessDelegate exitProcessHookDelegate;
        private static byte[] originalExitProcessBytes = new byte[5];
        private static IntPtr pExitProcess = IntPtr.Zero;

        public static void InstallNativeCrashHandler()
        {
            try
            {
                // 1. Install Vectored Exception Handler (VEH) to log native crashes (Access Violations, DEP, etc.)
                vehCallback = new VectoredHandlerCallback(OnVectoredException);
                AddVectoredExceptionHandler(1, vehCallback);

                // 2. Install ExitProcess hook to prevent external DLLs / MSHTML from abruptly terminating the browser
                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                if (hKernel32 != IntPtr.Zero)
                {
                    pExitProcess = GetProcAddress(hKernel32, "ExitProcess");
                    if (pExitProcess != IntPtr.Zero)
                    {
                        Marshal.Copy(pExitProcess, originalExitProcessBytes, 0, 5);

                        exitProcessHookDelegate = new ExitProcessDelegate(HookedExitProcess);
                        IntPtr pHook = Marshal.GetFunctionPointerForDelegate(exitProcessHookDelegate);

                        int offset = (int)pHook - (int)pExitProcess - 5;
                        byte[] patch = new byte[5];
                        patch[0] = 0xE9; // JMP opcode
                        BitConverter.GetBytes(offset).CopyTo(patch, 1);

                        uint oldProt;
                        if (VirtualProtect(pExitProcess, (UIntPtr)5, PAGE_EXECUTE_READWRITE, out oldProt))
                        {
                            Marshal.Copy(patch, 0, pExitProcess, 5);
                            VirtualProtect(pExitProcess, (UIntPtr)5, oldProt, out oldProt);
                            Log("System", "ExitProcess hook installed successfully.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("CrashHandlerInit", ex.Message);
            }
        }

        private static int OnVectoredException(IntPtr pExceptionInfo)
        {
            try
            {
                if (pExceptionInfo == IntPtr.Zero) return 0;

                // ExceptionRecord is at offset 0
                IntPtr pRecord = Marshal.ReadIntPtr(pExceptionInfo, 0);
                if (pRecord == IntPtr.Zero) return 0;

                uint code = (uint)Marshal.ReadInt32(pRecord, 0);
                // Filter out non-fatal / expected debug & RPC exceptions
                if (code == 0x406D1388 || code == 0x000006BA || code == 0xE0434352 || code == 0x8007000E)
                {
                    return 0; // EXCEPTION_CONTINUE_SEARCH
                }

                // Log fatal errors (0xC0000005 = Access Violation, 0xC00000FD = Stack Overflow, 0xC0000409 = FastFail)
                if (code == 0xC0000005 || code == 0xC00000FD || code == 0xC0000409 || code == 0xC000001D)
                {
                    IntPtr addr = Marshal.ReadIntPtr(pRecord, 12); // ExceptionAddress
                    string modName = GetModuleNameFromAddress(addr);

                    string msg = string.Format("[VEH FATAL EXCEPTION] Code: 0x{0:X8}, Address: 0x{1:X8}, Module: {2}", code, (int)addr, modName);
                    Log("NativeCrash", msg);

                    try
                    {
                        string crashPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                        System.IO.File.AppendAllText(crashPath, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, msg));
                    }
                    catch {}
                }
            }
            catch {}
            return 0; // EXCEPTION_CONTINUE_SEARCH
        }

        private static void HookedExitProcess(uint uExitCode)
        {
            try
            {
                if (AllowProcessExit)
                {
                    Log("ExitProcess", string.Format("Legitimate exit confirmed with code {0}. Exiting cleanly.", uExitCode));
                    // Restore original bytes and exit
                    uint oldProt;
                    VirtualProtect(pExitProcess, (UIntPtr)5, PAGE_EXECUTE_READWRITE, out oldProt);
                    Marshal.Copy(originalExitProcessBytes, 0, pExitProcess, 5);
                    VirtualProtect(pExitProcess, (UIntPtr)5, oldProt, out oldProt);
                    ExitProcess(uExitCode);
                    return;
                }

                // If exit was NOT requested by the user, block it!
                Log("ExitProcess", string.Format("BLOCKED unauthorized ExitProcess(code={0})! Application remains alive.", uExitCode));
            }
            catch (Exception ex)
            {
                Log("HookedExitProcess", ex.Message);
            }
        }

        private static string GetModuleNameFromAddress(IntPtr addr)
        {
            try
            {
                IntPtr hMod;
                if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, addr, out hMod) && hMod != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(260);
                    if (GetModuleFileName(hMod, sb, sb.Capacity) > 0)
                    {
                        return System.IO.Path.GetFileName(sb.ToString());
                    }
                }
            }
            catch {}
            return "UnknownModule";
        }

        public static void Log(string category, string message)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
                string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}\r\n", DateTime.Now, category, message);
                System.IO.File.AppendAllText(path, line);
            }
            catch {}
        }

        #endregion
    }
}
