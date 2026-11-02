using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EBSBrowser
{
    public enum SessionState
    {
        Unknown,   // not logged in yet / network error / cannot decide
        Alive,     // EBS confirmed the ICX session is valid
        Expired    // EBS redirected to a login page => ICX session is dead
    }

    /// <summary>
    /// Tier 1 keep-alive with REAL verification.
    /// - Discovers the actual EBS host from tab navigations (no hard-coded host).
    /// - Native C# ping with full WinINet cookies, manual redirects and login-page detection.
    /// - A hidden WebBrowser that periodically loads the EBS home page using the very same
    ///   WinINet cookie jar as the Forms applet (most faithful way to refresh ICX_SESSIONS).
    /// </summary>
    public static class KeepAliveService
    {
        private const string DefaultEbsBase = "https://ebs.chinagasholdings.com";
        private const string PortalBase = "https://portal.chinagasholdings.com";
        private const string HomePath = "/OA_HTML/OA.jsp?OAFunc=OANEWHOMEPAGE";

        private static readonly object sync = new object();
        private static string ebsBase;              // discovered scheme://host[:port]
        private static int pingRunning = 0;
        private static int consecutiveExpired = 0;

        private static WebBrowser hiddenBrowser;
        private static string hiddenHost;

        public static SessionState State { get; private set; }
        public static DateTime LastCheckTime { get; private set; }
        public static DateTime LastAliveTime { get; private set; }
        public static string LastDetail { get; private set; }
        public static bool EbsSeen { get { return ebsBase != null; } }

        /// <summary>Number of consecutive pings that reported Expired (0 when Alive/Unknown).</summary>
        public static int ConsecutiveExpired { get { return consecutiveExpired; } }

        /// <summary>Raised on a thread-pool thread after every ping. Marshal to UI yourself.</summary>
        public static event Action<SessionState, SessionState> StateChecked;

        static KeepAliveService()
        {
            State = SessionState.Unknown;
            LastCheckTime = DateTime.MinValue;
            LastAliveTime = DateTime.MinValue;
            LastDetail = "尚未检测";
        }

        public static string EbsBase
        {
            get { return ebsBase ?? DefaultEbsBase; }
        }

        /// <summary>
        /// Called for every tab navigation. Learns the real EBS scheme/host/port.
        /// </summary>
        public static void ObserveUrl(Uri u)
        {
            try
            {
                if (u == null || !(u.Scheme == "http" || u.Scheme == "https")) return;
                string path = u.AbsolutePath;
                if (path.IndexOf("/OA_HTML/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("frmservlet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/forms/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string b = u.GetLeftPart(UriPartial.Authority);
                    lock (sync)
                    {
                        if (!string.Equals(b, ebsBase, StringComparison.OrdinalIgnoreCase))
                        {
                            NativeMethods.Log("KeepAlive", "EBS host discovered: " + b + " (from " + u.GetLeftPart(UriPartial.Path) + ")");
                            ebsBase = b;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Runs one full keep-alive cycle. Must be called on the UI thread.
        /// </summary>
        public static void RunCycle(Control uiOwner)
        {
            CheckNowAsync("timer");
            RefreshHiddenBrowser(uiOwner);
        }

        /// <summary>
        /// Starts a background ping (ignored if one is already running).
        /// </summary>
        public static void CheckNowAsync(string reason)
        {
            if (Interlocked.CompareExchange(ref pingRunning, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    SessionState old = State;
                    string detail;
                    SessionState s = PingEbs(out detail);
                    LastCheckTime = DateTime.Now;
                    LastDetail = detail;
                    if (s == SessionState.Alive)
                    {
                        LastAliveTime = DateTime.Now;
                        consecutiveExpired = 0;
                    }
                    else if (s == SessionState.Expired)
                    {
                        consecutiveExpired++;
                    }
                    else
                    {
                        consecutiveExpired = 0;
                    }
                    State = s;
                    NativeMethods.Log("KeepAlive", string.Format("[{0}] EBS ping => {1} ({2})", reason, s, detail));

                    PingPortal();

                    Action<SessionState, SessionState> h = StateChecked;
                    if (h != null) h(old, s);
                }
                catch (Exception ex)
                {
                    NativeMethods.Log("KeepAlive_Error", ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref pingRunning, 0);
                }
            });
        }

        private static void PrepareTls()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            ServicePointManager.Expect100Continue = false;
        }

        private static bool LooksLikeLogin(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string u = url.ToLowerInvariant();
            return u.Contains("appslogin") || u.Contains("appslocallogin") || u.Contains("/sso/") ||
                   u.Contains("fnd/sso") || u.Contains("login") || u.Contains("logout") ||
                   u.Contains("sessionexpired") || u.Contains("portal.chinagasholdings.com");
        }

        private static bool BodyLooksLikeLogin(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            string b = body.ToLowerInvariant();
            return b.Contains("type=\"password\"") || b.Contains("type='password'") || b.Contains("type=password") ||
                   b.Contains("appslocallogin") || b.Contains("appslogin");
        }

        /// <summary>
        /// Requests the EBS home page with the browser's own cookies and decides whether the ICX session is valid.
        /// Redirects are followed manually (max 4 hops) so a bounce to a login page is detected instead of being
        /// mistaken for "200 OK".
        /// </summary>
        public static SessionState PingEbs(out string detail)
        {
            detail = "";
            try
            {
                PrepareTls();
                string url = EbsBase + HomePath + "&_ka=" + DateTime.Now.Ticks;
                string firstCookies = null;

                for (int hop = 0; hop < 5; hop++)
                {
                    Uri uri = new Uri(url);
                    string cookies = NativeMethods.GetCookies(uri.GetLeftPart(UriPartial.Authority) + "/");
                    if (hop == 0)
                    {
                        firstCookies = cookies;
                        if (string.IsNullOrEmpty(cookies))
                        {
                            detail = "无 EBS Cookie（尚未登录 EBS）host=" + uri.Host;
                            return SessionState.Unknown;
                        }
                    }

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                    req.Method = "GET";
                    req.AllowAutoRedirect = false;
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                    req.Accept = "text/html, */*";
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 15000;
                    if (!string.IsNullOrEmpty(cookies)) req.Headers.Add("Cookie", cookies);

                    HttpWebResponse resp;
                    try
                    {
                        resp = (HttpWebResponse)req.GetResponse();
                    }
                    catch (WebException wex)
                    {
                        resp = wex.Response as HttpWebResponse;
                        if (resp == null)
                        {
                            detail = "网络错误: " + wex.Status + " " + wex.Message;
                            return SessionState.Unknown;
                        }
                    }

                    using (resp)
                    {
                        int code = (int)resp.StatusCode;
                        string location = resp.Headers["Location"];
                        if (code >= 300 && code < 400 && !string.IsNullOrEmpty(location))
                        {
                            string next = new Uri(uri, location).ToString();
                            if (LooksLikeLogin(next))
                            {
                                detail = string.Format("HTTP {0} -> 登录页 {1} | cookies={2}", code, Trim(next), NativeMethods.CookieNames(firstCookies));
                                return SessionState.Expired;
                            }
                            url = next;
                            continue;
                        }
                        if (code == 401 || code == 403)
                        {
                            detail = "HTTP " + code + " 未授权";
                            return SessionState.Expired;
                        }
                        if (code != 200)
                        {
                            detail = "HTTP " + code;
                            return SessionState.Unknown;
                        }

                        string body = ReadHead(resp, 128 * 1024);
                        if (LooksLikeLogin(resp.ResponseUri.ToString()) && resp.ResponseUri.AbsolutePath.IndexOf("OA.jsp", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            detail = "落在登录页 " + Trim(resp.ResponseUri.ToString());
                            return SessionState.Expired;
                        }
                        if (BodyLooksLikeLogin(body))
                        {
                            detail = "返回内容为登录表单 " + Trim(resp.ResponseUri.ToString());
                            return SessionState.Expired;
                        }
                        detail = string.Format("HTTP 200 {0}B hops={1} | cookies={2}", body.Length, hop, NativeMethods.CookieNames(firstCookies));
                        return SessionState.Alive;
                    }
                }
                detail = "重定向次数过多";
                return SessionState.Unknown;
            }
            catch (Exception ex)
            {
                detail = "异常: " + ex.Message;
                return SessionState.Unknown;
            }
        }

        private static void PingPortal()
        {
            try
            {
                string cookies = NativeMethods.GetCookies(PortalBase + "/");
                if (string.IsNullOrEmpty(cookies)) return;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(PortalBase + "/group/frontend?_ka=" + DateTime.Now.Ticks);
                req.Method = "GET";
                req.AllowAutoRedirect = false;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                req.Timeout = 10000;
                req.Headers.Add("Cookie", cookies);
                HttpWebResponse resp;
                try { resp = (HttpWebResponse)req.GetResponse(); }
                catch (WebException wex) { resp = wex.Response as HttpWebResponse; }
                if (resp != null)
                {
                    using (resp)
                    {
                        NativeMethods.Log("KeepAlive", string.Format("Portal ping => HTTP {0} {1}", (int)resp.StatusCode, Trim(resp.Headers["Location"] ?? "")));
                    }
                }
            }
            catch
            {
            }
        }

        private static string ReadHead(HttpWebResponse resp, int max)
        {
            try
            {
                Encoding enc = Encoding.UTF8;
                try { if (!string.IsNullOrEmpty(resp.CharacterSet)) enc = Encoding.GetEncoding(resp.CharacterSet); } catch { }
                using (Stream s = resp.GetResponseStream())
                {
                    byte[] buf = new byte[max];
                    int total = 0, n;
                    while (total < max && (n = s.Read(buf, total, max - total)) > 0) total += n;
                    return enc.GetString(buf, 0, total);
                }
            }
            catch
            {
                return "";
            }
        }

        private static string Trim(string s)
        {
            if (s == null) return "";
            return s.Length > 160 ? s.Substring(0, 160) + "..." : s;
        }

        #region Hidden keep-alive browser

        /// <summary>
        /// Loads the EBS home page in an invisible WebBrowser that shares the WinINet cookie jar with the
        /// Forms applet. Navigation to any non-EBS host (e.g. portal SSO) is cancelled so it can never
        /// silently replace the ICX session cookie that the running Forms session is bound to.
        /// </summary>
        private static void RefreshHiddenBrowser(Control uiOwner)
        {
            try
            {
                if (ebsBase == null) return;                 // only once a real EBS host is known
                if (State == SessionState.Expired) return;   // never touch the jar while a rescue may be pending

                if (hiddenBrowser == null || hiddenBrowser.IsDisposed)
                {
                    hiddenBrowser = new WebBrowser();
                    hiddenBrowser.ScriptErrorsSuppressed = true; // no dialogs from an invisible page
                    hiddenBrowser.Size = new System.Drawing.Size(1, 1);
                    hiddenBrowser.Location = new System.Drawing.Point(-100, -100);
                    hiddenBrowser.TabStop = false;
                    hiddenBrowser.NewWindow += delegate (object s, System.ComponentModel.CancelEventArgs e) { e.Cancel = true; };
                    hiddenBrowser.Navigating += delegate (object s, WebBrowserNavigatingEventArgs e)
                    {
                        try
                        {
                            if (e.Url == null || hiddenHost == null) return;
                            if (e.Url.Scheme != "http" && e.Url.Scheme != "https") return;
                            if (!string.Equals(e.Url.Host, hiddenHost, StringComparison.OrdinalIgnoreCase))
                            {
                                e.Cancel = true;
                                NativeMethods.Log("KeepAlive", "Hidden browser blocked off-EBS redirect: " + Trim(e.Url.ToString()));
                            }
                        }
                        catch { }
                    };
                    hiddenBrowser.DocumentCompleted += delegate (object s, WebBrowserDocumentCompletedEventArgs e)
                    {
                        try
                        {
                            string u = e.Url != null ? e.Url.ToString() : "";
                            bool login = false;
                            try
                            {
                                if (hiddenBrowser.Document != null)
                                {
                                    foreach (HtmlElement inp in hiddenBrowser.Document.GetElementsByTagName("input"))
                                    {
                                        if (string.Equals(inp.GetAttribute("type"), "password", StringComparison.OrdinalIgnoreCase)) { login = true; break; }
                                    }
                                }
                            }
                            catch { }
                            NativeMethods.Log("KeepAlive", string.Format("Hidden browser loaded: {0} loginForm={1}", Trim(u), login));
                            if (login) CheckNowAsync("hidden-browser-login");
                        }
                        catch { }
                    };
                    uiOwner.Controls.Add(hiddenBrowser);
                    hiddenBrowser.SendToBack();
                }

                Uri target = new Uri(ebsBase + HomePath + "&_kh=" + DateTime.Now.Ticks);
                hiddenHost = target.Host;
                hiddenBrowser.Navigate(target);
            }
            catch (Exception ex)
            {
                NativeMethods.Log("KeepAlive_Error", "Hidden browser: " + ex.Message);
            }
        }

        public static void DisposeHiddenBrowser()
        {
            try
            {
                if (hiddenBrowser != null)
                {
                    hiddenBrowser.Dispose();
                    hiddenBrowser = null;
                }
            }
            catch
            {
            }
        }

        #endregion
    }
}
