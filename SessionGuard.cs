using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace EBSBrowser
{
    /// <summary>
    /// Tier 3: Session Guard (rewritten in v1.1.0).
    ///
    /// When EBS shows "您的登录会话不再有效。您是否希望重新登录…" the ICX session is already dead and
    /// the only way to keep the unsaved Forms data is to answer 【是】 and log in again.
    ///
    /// Safety rules (the old version auto-pressed Y/Enter on ANY "提示/Warning" window):
    ///  1. Never act unless KeepAliveService has CONFIRMED the session is Expired.
    ///  2. Only press Enter after verifying the Java window really is the foreground window.
    ///  3. Never act when the desktop is locked (input would go nowhere / somewhere else).
    ///  4. At most MaxAttempts per expiry episode, spaced by AttemptSpacingSeconds.
    ///
    /// Also contains a diagnostic window probe that logs every Java window (class/title/size) once,
    /// so we can see whether the Forms "决定" alert is a native dialog or a lightweight component.
    /// </summary>
    public static class SessionGuard
    {
        private const int MaxAttempts = 3;
        private const int AttemptSpacingSeconds = 20;
        private const int RescueWindowMinutes = 5;

        private static Timer guardTimer;
        private static AppConfig currentConfig;

        private static readonly HashSet<IntPtr> seenWindows = new HashSet<IntPtr>();
        private static int attemptsThisEpisode = 0;
        private static DateTime lastAttemptTime = DateTime.MinValue;
        private static DateTime lastDialogCheckRequest = DateTime.MinValue;
        private static bool episodeNotified = false;
        private static bool lockedNotified = false;

        /// <summary>Raised (UI thread) with a human-readable message whenever the guard acts or needs attention.</summary>
        public static event Action<string> OnSessionRescued;

        /// <summary>Raised (UI thread) once per expiry episode when the session is confirmed dead.</summary>
        public static event Action<string> OnSessionExpired;

        /// <summary>Until this time, login pages opened in any tab are auto-filled AND auto-submitted.</summary>
        public static DateTime RescueActiveUntil { get; private set; }

        public static bool IsRescueActive
        {
            get { return DateTime.Now < RescueActiveUntil; }
        }

        public static void Start(AppConfig config)
        {
            currentConfig = config;
            if (guardTimer == null)
            {
                guardTimer = new Timer();
                guardTimer.Interval = 2000;
                guardTimer.Tick += GuardTimer_Tick;
                guardTimer.Start();
                NativeMethods.Log("SessionGuard", "Started. AutoRescue=" + (config != null && config.AutoRescue));
            }
        }

        public static void Stop()
        {
            if (guardTimer != null)
            {
                guardTimer.Stop();
                guardTimer.Dispose();
                guardTimer = null;
                NativeMethods.Log("SessionGuard", "Stopped.");
            }
        }

        public static void UpdateConfig(AppConfig config)
        {
            currentConfig = config;
        }

        /// <summary>Called by MainForm when a login page in a tab was auto-submitted.</summary>
        public static void NotifyLoginSubmitted(string url)
        {
            NativeMethods.Log("SessionGuard", "Re-login form auto-submitted: " + url);
            Raise(OnSessionRescued, "已自动提交重新登录表单，等待 EBS 恢复会话…");
            KeepAliveService.CheckNowAsync("post-login-submit");
        }

        private const int MaxLoginSubmitsPerEpisode = 2;
        private static int loginSubmitsThisEpisode = 0;

        /// <summary>
        /// Returns true if another automatic login submit is allowed in this expiry episode.
        /// Hard cap prevents a wrong saved password from locking the account.
        /// </summary>
        public static bool TryConsumeLoginSubmit()
        {
            if (loginSubmitsThisEpisode >= MaxLoginSubmitsPerEpisode)
            {
                NativeMethods.Log("SessionGuard", "Auto login submit skipped: limit reached for this episode.");
                return false;
            }
            loginSubmitsThisEpisode++;
            return true;
        }

        private static void GuardTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                NativeMethods.Log("SessionGuard_Error", ex.Message);
            }
        }

        private class JavaWindows
        {
            public IntPtr DecisionDialog = IntPtr.Zero;  // native dialog titled 决定/Decision
            public IntPtr FormsFrame = IntPtr.Zero;      // main Oracle Applications frame
        }

        private static HashSet<uint> GetJavaPids()
        {
            HashSet<uint> pids = new HashSet<uint>();
            foreach (string name in new string[] { "jp2launcher", "java", "javaw" })
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(name))
                    {
                        pids.Add((uint)p.Id);
                        p.Dispose();
                    }
                }
                catch { }
            }
            return pids;
        }

        private static JavaWindows ScanJavaWindows()
        {
            JavaWindows result = new JavaWindows();
            HashSet<uint> javaPids = GetJavaPids();
            HashSet<IntPtr> alive = new HashSet<IntPtr>();
            if (javaPids.Count == 0)
            {
                seenWindows.Clear();
                return result;
            }

            NativeMethods.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (!javaPids.Contains(pid)) return true;

                alive.Add(hWnd);
                StringBuilder sbTitle = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, sbTitle, 256);
                string title = sbTitle.ToString();
                StringBuilder sbClass = new StringBuilder(256);
                NativeMethods.GetClassName(hWnd, sbClass, 256);
                string cls = sbClass.ToString();

                if (!seenWindows.Contains(hWnd))
                {
                    seenWindows.Add(hWnd);
                    LogWindowTree(hWnd, pid);
                }

                if (title.IndexOf("决定", StringComparison.Ordinal) >= 0 ||
                    title.IndexOf("Decision", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.DecisionDialog = hWnd;
                }
                else if (title.IndexOf("Oracle Applications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         title.IndexOf("Oracle Forms", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         title.IndexOf("EBSPROD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         title.IndexOf("Oracle 应用", StringComparison.Ordinal) >= 0)
                {
                    result.FormsFrame = hWnd;
                }
                else if (result.FormsFrame == IntPtr.Zero && cls == "SunAwtFrame")
                {
                    NativeMethods.RECT r;
                    if (NativeMethods.GetWindowRect(hWnd, out r) && (r.Right - r.Left) > 600 && (r.Bottom - r.Top) > 400)
                    {
                        result.FormsFrame = hWnd;
                    }
                }
                return true;
            }, IntPtr.Zero);

            seenWindows.RemoveWhere(h => !alive.Contains(h));
            return result;
        }

        private static void LogWindowTree(IntPtr hWnd, uint pid)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("New Java window pid=").Append(pid).Append(" ").Append(NativeMethods.DescribeWindow(hWnd));
                int count = 0;
                NativeMethods.EnumChildWindows(hWnd, delegate (IntPtr child, IntPtr lp)
                {
                    if (count++ >= 25) return false;
                    sb.Append("\r\n        child ").Append(NativeMethods.DescribeWindow(child));
                    return true;
                }, IntPtr.Zero);
                NativeMethods.Log("WindowProbe", sb.ToString());
            }
            catch { }
        }

        private static void Tick()
        {
            JavaWindows jw = ScanJavaWindows();

            // A native decision dialog appeared => verify the session immediately (debounced)
            if (jw.DecisionDialog != IntPtr.Zero && (DateTime.Now - lastDialogCheckRequest).TotalSeconds > 15)
            {
                lastDialogCheckRequest = DateTime.Now;
                NativeMethods.Log("SessionGuard", "Decision dialog visible: " + NativeMethods.DescribeWindow(jw.DecisionDialog) + " -> verifying session");
                KeepAliveService.CheckNowAsync("decision-dialog");
            }

            SessionState state = KeepAliveService.State;

            if (state == SessionState.Alive)
            {
                if (attemptsThisEpisode > 0 || episodeNotified)
                {
                    NativeMethods.Log("SessionGuard", "Session is alive again. Episode closed after " + attemptsThisEpisode + " attempt(s).");
                    if (attemptsThisEpisode > 0) Raise(OnSessionRescued, "会话已恢复在线 ✔");
                }
                attemptsThisEpisode = 0;
                loginSubmitsThisEpisode = 0;
                episodeNotified = false;
                lockedNotified = false;
                return;
            }

            if (state != SessionState.Expired) return;

            // While expired, re-verify every 30 s (instead of every keep-alive period) for fast reaction
            if ((DateTime.Now - KeepAliveService.LastCheckTime).TotalSeconds > 30)
            {
                KeepAliveService.CheckNowAsync("expired-recheck");
            }

            // Stale verdicts are not trusted
            if ((DateTime.Now - KeepAliveService.LastCheckTime).TotalMinutes > 6) return;

            if (!episodeNotified)
            {
                episodeNotified = true;
                NativeMethods.Log("SessionGuard", "Session CONFIRMED expired: " + KeepAliveService.LastDetail);
                Raise(OnSessionExpired, "EBS 登录会话已失效：" + KeepAliveService.LastDetail);
            }

            if (currentConfig == null || !currentConfig.AutoRescue) return;
            if (attemptsThisEpisode >= MaxAttempts) return;
            if ((DateTime.Now - lastAttemptTime).TotalSeconds < AttemptSpacingSeconds) return;

            // Target: native dialog if visible; otherwise (lightweight in-frame alert) the Forms frame,
            // but only after TWO consecutive Expired verdicts to rule out a one-off misjudgement.
            IntPtr target = jw.DecisionDialog;
            string mode = "native-dialog";
            if (target == IntPtr.Zero)
            {
                if (jw.FormsFrame == IntPtr.Zero || KeepAliveService.ConsecutiveExpired < 2) return;
                target = jw.FormsFrame;
                mode = "forms-frame";
            }

            if (NativeMethods.IsDesktopLocked())
            {
                if (!lockedNotified)
                {
                    lockedNotified = true;
                    NativeMethods.Log("SessionGuard", "Desktop is LOCKED - cannot click 【是】. Waiting for unlock.");
                    Raise(OnSessionRescued, "会话已失效，但屏幕已锁定，无法自动点击【是】，解锁后将自动处理");
                }
                return;
            }

            lastAttemptTime = DateTime.Now;
            attemptsThisEpisode++;

            bool fgOk = NativeMethods.ForceForeground(target);
            IntPtr fg = NativeMethods.GetForegroundWindow();
            uint fgPid;
            NativeMethods.GetWindowThreadProcessId(fg, out fgPid);
            uint targetPid;
            NativeMethods.GetWindowThreadProcessId(target, out targetPid);

            if (!fgOk || fgPid != targetPid)
            {
                NativeMethods.Log("SessionGuard", string.Format("Attempt {0}/{1} ({2}) ABORTED: could not activate target. fg={3}",
                    attemptsThisEpisode, MaxAttempts, mode, NativeMethods.DescribeWindow(fg)));
                return;
            }

            // Default button of the Forms alert is 【是】 -> Enter.
            NativeMethods.PressEnter();
            RescueActiveUntil = DateTime.Now.AddMinutes(RescueWindowMinutes);
            NativeMethods.Log("SessionGuard", string.Format("Attempt {0}/{1} ({2}): pressed Enter on {3}",
                attemptsThisEpisode, MaxAttempts, mode, NativeMethods.DescribeWindow(target)));
            Raise(OnSessionRescued, string.Format("会话已失效，已自动点击【是】(第{0}次)，正在自动重新登录…", attemptsThisEpisode));

            // Re-verify soon so a successful re-login closes the episode quickly
            KeepAliveService.CheckNowAsync("post-rescue");
        }

        private static void Raise(Action<string> h, string msg)
        {
            try
            {
                if (h != null) h(msg);
            }
            catch { }
        }
    }
}
