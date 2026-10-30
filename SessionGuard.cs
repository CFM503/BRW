using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EBSBrowser
{
    /// <summary>
    /// Tier 3: Intelligent Session Guard
    /// Automatically detects Oracle Forms session timeout dialogs ("您的登录会话不再有效")
    /// and safely re-authenticates to prevent any work/draft loss, enabling permanent hanging.
    /// </summary>
    public static class SessionGuard
    {
        private static Timer guardTimer;
        private static AppConfig currentConfig;
        private static DateTime lastRescueTime = DateTime.MinValue;

        public static event Action<string> OnSessionRescued;

        private const byte VK_Y = 0x59;
        private const byte VK_RETURN = 0x0D;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP = 0x0105;

        public static void Start(AppConfig config)
        {
            currentConfig = config;
            if (guardTimer == null)
            {
                guardTimer = new Timer();
                guardTimer.Interval = 2500; // Watch every 2.5 seconds
                guardTimer.Tick += GuardTimer_Tick;
                guardTimer.Start();
            }
        }

        public static void Stop()
        {
            if (guardTimer != null)
            {
                guardTimer.Stop();
                guardTimer.Dispose();
                guardTimer = null;
            }
        }

        public static void UpdateConfig(AppConfig config)
        {
            currentConfig = config;
        }

        private static void GuardTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                ScanAndRescueDialogs();
            }
            catch
            {
            }
        }

        private static void ScanAndRescueDialogs()
        {
            NativeMethods.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return true;

                try
                {
                    Process proc = Process.GetProcessById((int)pid);
                    string procName = proc.ProcessName.ToLower();
                    if (procName != "java" && procName != "jp2launcher") return true;

                    StringBuilder sbTitle = new StringBuilder(256);
                    NativeMethods.GetWindowText(hWnd, sbTitle, 256);
                    string title = sbTitle.ToString();

                    StringBuilder sbClass = new StringBuilder(256);
                    NativeMethods.GetClassName(hWnd, sbClass, 256);
                    string cls = sbClass.ToString();

                    // Check for Oracle Forms timeout / decision dialog
                    // In Chinese EBS, this dialog is titled "决定" or "提示"
                    if (title.IndexOf("决定", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("提示", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Decision", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Avoid triggering repeatedly within 5 seconds for the same event
                        if ((DateTime.Now - lastRescueTime).TotalSeconds > 5)
                        {
                            lastRescueTime = DateTime.Now;

                            // 1. Bring dialog to front
                            NativeMethods.SetForegroundWindow(hWnd);

                            // 2. Send 'Y' (for 是(Y)) to agree to re-login and retain uncommitted changes
                            NativeMethods.PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_Y, IntPtr.Zero);
                            NativeMethods.PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_Y, (IntPtr)0xC0000001);

                            // Also send Alt+Y
                            NativeMethods.PostMessage(hWnd, WM_SYSKEYDOWN, (IntPtr)VK_Y, (IntPtr)0x20000000);
                            NativeMethods.PostMessage(hWnd, WM_SYSKEYUP, (IntPtr)VK_Y, (IntPtr)0xE0000001);

                            // Also simulate Enter key (default button on decision dialog is "是")
                            NativeMethods.PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                            NativeMethods.PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_RETURN, (IntPtr)0xC0000001);

                            if (OnSessionRescued != null)
                            {
                                OnSessionRescued(string.Format("捕获到会话到期对话框({0})，已自动代点【是(Y)】续期会话！", title));
                            }
                        }
                    }
                    // Check for Oracle Applications re-login dialog
                    else if (title.IndexOf("Oracle 应用程序登录", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             title.IndexOf("Oracle Applications Login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             title.IndexOf("重新登录", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if ((DateTime.Now - lastRescueTime).TotalSeconds > 5 && currentConfig != null && !string.IsNullOrEmpty(currentConfig.SavedPasswordCipher))
                        {
                            lastRescueTime = DateTime.Now;
                            string plainPwd = CredentialManager.DecryptPassword(currentConfig.SavedPasswordCipher);
                            if (!string.IsNullOrEmpty(plainPwd))
                            {
                                NativeMethods.SetForegroundWindow(hWnd);
                                SendKeys.SendWait(plainPwd + "{ENTER}");

                                if (OnSessionRescued != null)
                                {
                                    OnSessionRescued("检测到 Forms 重新认证窗口，已自动填入密码完成续订！");
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
