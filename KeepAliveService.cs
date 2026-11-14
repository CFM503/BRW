using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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

    public sealed class KeepAliveSnapshot
    {
        public SessionState State { get; set; }
        public DateTime LastCheckTime { get; set; }
        public DateTime LastAliveTime { get; set; }
        public string LastDetail { get; set; }
        public int ConsecutiveExpired { get; set; }
        public string EbsBase { get; set; }
        public bool EbsSeen { get; set; }
    }

    /// <summary>
    /// Tier 1 keep-alive with REAL verification.
    /// - Discovers the actual EBS host from tab navigations (no hard-coded host).
    /// - Native C# ping with full WinINet cookies, manual redirects and login-page detection.
    /// - A hidden ExtendedWebBrowser that periodically loads the EBS home page using the very same
    ///   WinINet cookie jar as the Forms applet (most faithful way to refresh ICX_SESSIONS).
    /// </summary>
    public static class KeepAliveService
    {
        private const string DefaultEbsBase = "https://ebs.chinagasholdings.com";
        private const string PortalBase = "https://portal.chinagasholdings.com";
        private const string HomePath = "/OA_HTML/OA.jsp?OAFunc=OANEWHOMEPAGE";

        private static readonly object sync = new object();
        private static readonly HashSet<string> warnedCertHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string ebsBase;              // discovered scheme://host[:port]
        private static string configuredHomeUrl;    // user-configured portal HomeUrl
        private static int pingRunning = 0;
        private static int consecutiveExpired = 0;
        private static bool tlsConfigured = false;

        private static ExtendedWebBrowser hiddenBrowser;
        private static string hiddenHost;

        private static SessionState state = SessionState.Unknown;
        private static DateTime lastCheckTime = DateTime.MinValue;
        private static DateTime lastAliveTime = DateTime.MinValue;
        private static string lastDetail = "尚未检测";

        public static SessionState State { get { lock (sync) { return state; } } }
        public static DateTime LastCheckTime { get { lock (sync) { return lastCheckTime; } } }
        public static DateTime LastAliveTime { get { lock (sync) { return lastAliveTime; } } }
        public static string LastDetail { get { lock (sync) { return lastDetail; } } }
        public static bool EbsSeen { get { lock (sync) { return ebsBase != null; } } }

        /// <summary>Number of consecutive pings that reported Expired (0 when Alive/Unknown).</summary>
        public static int ConsecutiveExpired { get { lock (sync) { return consecutiveExpired; } } }

        /// <summary>Raised on a thread-pool thread after every ping. Marshal to UI yourself.</summary>
        public static event Action<SessionState, SessionState> StateChecked;

        public static string EbsBase
        {
            get { lock (sync) { return ebsBase ?? DefaultEbsBase; } }
        }

        public static KeepAliveSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new KeepAliveSnapshot
                {
                    State = state,
                    LastCheckTime = lastCheckTime,
                    LastAliveTime = lastAliveTime,
                    LastDetail = lastDetail,
                    ConsecutiveExpired = consecutiveExpired,
                    EbsBase = ebsBase ?? DefaultEbsBase,
                    EbsSeen = ebsBase != null
                };
            }
        }

        public static void SetConfiguredHomeUrl(string homeUrl)
        {
            lock (sync)
            {
                configuredHomeUrl = homeUrl;
            }
        }

        /// <summary>
        /// Checks whether a host belongs to China Gas enterprise domains or configured portal/EBS hosts.
        /// </summary>
        public static bool IsTrustedEnterpriseHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            if (host.Equals("chinagasholdings.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".chinagasholdings.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string curEbs, curHome;
            lock (sync)
            {
                curEbs = ebsBase;
                curHome = configuredHomeUrl;
            }

            Uri u;
            if (!string.IsNullOrEmpty(curEbs) && Uri.TryCreate(curEbs, UriKind.Absolute, out u) &&
                host.Equals(u.Host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(curHome) && Uri.TryCreate(curHome, UriKind.Absolute, out u) &&
                host.Equals(u.Host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether a URI is safe for credential auto-fill / auto-submit.
        /// Requires HTTPS unless the exact host matches the user's configured HomeUrl or discovered EbsBase.
        /// </summary>
        public static bool IsTrustedCredentialUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri) return false;
            if (!IsTrustedEnterpriseHost(uri.Host)) return false;

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                string curEbs, curHome;
                lock (sync)
                {
                    curEbs = ebsBase;
                    curHome = configuredHomeUrl;
                }
                Uri u;
                if (!string.IsNullOrEmpty(curHome) && Uri.TryCreate(curHome, UriKind.Absolute, out u) &&
                    string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(u.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(curEbs) && Uri.TryCreate(curEbs, UriKind.Absolute, out u) &&
                    string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(u.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
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
                    SessionState old;
                    lock (sync) { old = state; }
                    string detail;
                    SessionState s = PingEbs(out detail);
                    DateTime now = DateTime.Now;
                    lock (sync)
                    {
                        lastCheckTime = now;
                        lastDetail = detail;
                        if (s == SessionState.Alive)
                        {
                            lastAliveTime = now;
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
                        state = s;
                    }
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
            lock (sync)
            {
                if (tlsConfigured) return;
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.ServerCertificateValidationCallback = ValidateEnterpriseCertificate;
                tlsConfigured = true;
            }
        }

        private static bool ValidateEnterpriseCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            HttpWebRequest req = sender as HttpWebRequest;
            if (req != null && req.Address != null && IsTrustedEnterpriseHost(req.Address.Host))
            {
                bool shouldLog = false;
                lock (warnedCertHosts)
                {
                    if (!warnedCertHosts.Contains(req.Address.Host))
                    {
                        warnedCertHosts.Add(req.Address.Host);
                        shouldLog = true;
                    }
                }
                if (shouldLog)
                {
                    NativeMethods.Log("TLS", string.Format("Accepted internal/untrusted certificate for trusted enterprise host '{0}' (errors={1}).", req.Address.Host, sslPolicyErrors));
                }
                return true;
            }
            return false;
        }

        private static bool LooksLikeLogin(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            Uri u;
            if (Uri.TryCreate(url, UriKind.Absolute, out u))
            {
                string host = u.Host.ToLowerInvariant();
                string path = u.AbsolutePath.ToLowerInvariant();
                if (host.Contains("portal.chinagasholdings.com") && !path.Contains("oa.jsp")) return true;
                return path.Contains("appslogin") || path.Contains("appslocallogin") || path.Contains("/sso/") ||
                       path.Contains("fnd/sso") || path.Contains("login") || path.Contains("logout") ||
                       path.Contains("sessionexpired");
            }
            string raw = url.ToLowerInvariant();
            return raw.Contains("appslogin") || raw.Contains("appslocallogin") || raw.Contains("/sso/") ||
                   raw.Contains("fnd/sso") || raw.Contains("sessionexpired");
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
                    // Query cookies using full path (not just "/") so path-scoped cookies (e.g. Path=/OA_HTML) are included
                    string cookies = NativeMethods.GetCookies(uri.GetLeftPart(UriPartial.Path));
                    if (string.IsNullOrEmpty(cookies))
                    {
                        cookies = NativeMethods.GetCookies(uri.GetLeftPart(UriPartial.Authority) + "/");
                    }
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
                PrepareTls();
                string portalUrl = PortalBase + "/group/frontend";
                string curHome;
                lock (sync) { curHome = configuredHomeUrl; }
                Uri hu;
                if (!string.IsNullOrEmpty(curHome) && Uri.TryCreate(curHome, UriKind.Absolute, out hu) &&
                    (hu.Scheme == Uri.UriSchemeHttp || hu.Scheme == Uri.UriSchemeHttps))
                {
                    portalUrl = hu.GetLeftPart(UriPartial.Path);
                }

                string cookies = NativeMethods.GetCookies(portalUrl);
                if (string.IsNullOrEmpty(cookies))
                {
                    Uri pu = new Uri(portalUrl);
                    cookies = NativeMethods.GetCookies(pu.GetLeftPart(UriPartial.Authority) + "/");
                }
                if (string.IsNullOrEmpty(cookies)) return;

                string sep = portalUrl.Contains("?") ? "&" : "?";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(portalUrl + sep + "_ka=" + DateTime.Now.Ticks);
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
                    hiddenBrowser = new ExtendedWebBrowser();
                    hiddenBrowser.SuppressDialogs = true; // no alert/script dialogs from an invisible page
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
