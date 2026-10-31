using System;
using System.Drawing;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class MainForm : Form
    {
        public const string AppVersion = "1.0.9";
        private AppConfig config;
        private TabControl tabControl;

        private ToolStripButton btnBack;
        private ToolStripButton btnForward;
        private ToolStripButton btnRefresh;
        private ToolStripButton btnHome;
        private ToolStripButton btnNewTab;
        private ToolStripTextBox txtUrl;
        private ToolStripButton btnGo;
        private ToolStripButton btnZoomOut;
        private ToolStripButton btnZoomReset;
        private ToolStripButton btnZoomIn;
        private ToolStripButton btnClearSession;
        private ToolStripButton btnKeepAlive;
        private ToolStripButton btnSettings;
        private ProgressBar progressBar;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripStatusLabel lblZoom;
        private ToolStripStatusLabel lblKeepAlive;
        private ToolStripStatusLabel lblArch;

        private int currentZoom = 100;
        private Timer keepAliveTimer;
        private bool isKeepAliveEnabled = true;

        public MainForm(string initialUrl = null)
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch {}
            config = ConfigManager.Load();
            if (!string.IsNullOrEmpty(initialUrl))
            {
                config.HomeUrl = initialUrl;
            }
            currentZoom = config.DefaultZoom;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = string.Format("{0} - [版本: v{1}]", config.AppTitle, AppVersion);
            this.Size = new Size(1300, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // --- Top Toolbar ---
            ToolStrip toolStrip = new ToolStrip();
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Padding = new Padding(8, 6, 8, 6);
            toolStrip.BackColor = Color.FromArgb(248, 250, 252);
            toolStrip.RenderMode = ToolStripRenderMode.System;

            btnBack = new ToolStripButton("◀ 后退");
            btnBack.ToolTipText = "后退 (Alt+Left)";
            btnBack.Enabled = false;
            btnBack.Click += delegate { ExtendedWebBrowser b = GetCurrentBrowser(); if (b != null && b.CanGoBack) b.GoBack(); };

            btnForward = new ToolStripButton("▶ 前进");
            btnForward.ToolTipText = "前进 (Alt+Right)";
            btnForward.Enabled = false;
            btnForward.Click += delegate { ExtendedWebBrowser b = GetCurrentBrowser(); if (b != null && b.CanGoForward) b.GoForward(); };

            btnRefresh = new ToolStripButton("🔄 刷新");
            btnRefresh.ToolTipText = "刷新页面 (F5)";
            btnRefresh.Click += delegate { ExtendedWebBrowser b = GetCurrentBrowser(); if (b != null) b.Refresh(); };

            btnHome = new ToolStripButton("🏠 燃气门户");
            btnHome.ToolTipText = "返回登录门户首页";
            btnHome.Font = new Font(this.Font, FontStyle.Bold);
            btnHome.ForeColor = Color.FromArgb(0, 91, 172);
            btnHome.Click += delegate { NavigateTo(config.HomeUrl); };

            btnNewTab = new ToolStripButton("➕ 新页签");
            btnNewTab.ToolTipText = "新建标签页 (Ctrl+T)";
            btnNewTab.Click += delegate { CreateNewTab("新页签", config.HomeUrl, true); };

            ToolStripLabel lblAddress = new ToolStripLabel("地址:");
            lblAddress.ForeColor = Color.FromArgb(100, 116, 139);

            txtUrl = new ToolStripTextBox();
            txtUrl.Size = new Size(420, 26);
            txtUrl.ToolTipText = "输入网址后按回车跳转";
            txtUrl.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    NavigateTo(txtUrl.Text);
                    e.SuppressKeyPress = true;
                }
            };

            btnGo = new ToolStripButton("转到");
            btnGo.Click += delegate { NavigateTo(txtUrl.Text); };

            btnZoomOut = new ToolStripButton("🔍-");
            btnZoomOut.ToolTipText = "缩小页面 (Ctrl+ -)";
            btnZoomOut.Click += delegate { ChangeZoom(-10); };

            btnZoomReset = new ToolStripButton("100%");
            btnZoomReset.ToolTipText = "重置缩放 (Ctrl+ 0)";
            btnZoomReset.Click += delegate { SetZoom(100); };

            btnZoomIn = new ToolStripButton("🔍+");
            btnZoomIn.ToolTipText = "放大页面 (Ctrl+ +)";
            btnZoomIn.Click += delegate { ChangeZoom(10); };

            btnKeepAlive = new ToolStripButton("⚡ 防超时: 开");
            btnKeepAlive.ToolTipText = "自动在后台刷新会话与防挂机心跳，防止 EBS 登录失效（点击可开启/关闭）";
            btnKeepAlive.ForeColor = Color.FromArgb(16, 128, 64);
            btnKeepAlive.Font = new Font(this.Font, FontStyle.Bold);
            btnKeepAlive.Click += BtnKeepAlive_Click;

            btnClearSession = new ToolStripButton("🧹 清理会话");
            btnClearSession.ToolTipText = "重置浏览器会话与Cookie（当EBS登录失效或Token卡住时点击）";
            btnClearSession.Click += BtnClearSession_Click;

            btnSettings = new ToolStripButton("⚙ 设置");
            btnSettings.ToolTipText = "修改首页网址与默认缩放";
            btnSettings.Click += BtnSettings_Click;

            toolStrip.Items.Add(btnBack);
            toolStrip.Items.Add(btnForward);
            toolStrip.Items.Add(btnRefresh);
            toolStrip.Items.Add(btnHome);
            toolStrip.Items.Add(btnNewTab);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(lblAddress);
            toolStrip.Items.Add(txtUrl);
            toolStrip.Items.Add(btnGo);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnZoomOut);
            toolStrip.Items.Add(btnZoomReset);
            toolStrip.Items.Add(btnZoomIn);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnKeepAlive);
            toolStrip.Items.Add(btnClearSession);
            toolStrip.Items.Add(btnSettings);

            ToolStripLabel lblVersionBadge = new ToolStripLabel(string.Format("版本: v{0}", AppVersion));
            lblVersionBadge.ForeColor = Color.FromArgb(40, 110, 40);
            lblVersionBadge.Font = new Font(this.Font, FontStyle.Bold);
            lblVersionBadge.Alignment = ToolStripItemAlignment.Right;
            lblVersionBadge.Padding = new Padding(0, 0, 10, 0);
            toolStrip.Items.Add(lblVersionBadge);

            // --- Progress Bar ---
            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 3;
            progressBar.Visible = false;
            progressBar.Style = ProgressBarStyle.Continuous;

            // --- Bottom Status Strip ---
            statusStrip = new StatusStrip();
            statusStrip.BackColor = Color.FromArgb(241, 245, 249);
            statusStrip.Padding = new Padding(4, 2, 4, 2);

            lblStatus = new ToolStripStatusLabel("初始化中...");
            lblStatus.Spring = true;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            lblZoom = new ToolStripStatusLabel(string.Format("缩放: {0}%", currentZoom));
            lblZoom.BorderSides = ToolStripStatusLabelBorderSides.Left;
            lblZoom.Padding = new Padding(8, 0, 8, 0);

            lblKeepAlive = new ToolStripStatusLabel("⚡ 防超时: 激活");
            lblKeepAlive.BorderSides = ToolStripStatusLabelBorderSides.Left;
            lblKeepAlive.ForeColor = Color.FromArgb(16, 128, 64);
            lblKeepAlive.Padding = new Padding(8, 0, 8, 0);
            lblKeepAlive.ToolTipText = "点击可立即手动触发一次保活检测并查看状态";
            lblKeepAlive.Click += delegate { SendKeepAlive(); };

            lblArch = new ToolStripStatusLabel(string.Format("当前版本: v{0} (32位 IE11)", AppVersion));
            lblArch.BorderSides = ToolStripStatusLabelBorderSides.Left;
            lblArch.ForeColor = Color.FromArgb(40, 110, 40);
            lblArch.Font = new Font(this.Font, FontStyle.Bold);
            lblArch.Padding = new Padding(8, 0, 8, 0);

            statusStrip.Items.Add(lblStatus);
            statusStrip.Items.Add(lblArch);
            statusStrip.Items.Add(lblZoom);
            statusStrip.Items.Add(lblKeepAlive);

            // --- Tab Control ---
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.ItemSize = new Size(180, 28);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.Padding = new Point(14, 4);
            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // Layout assembly
            this.Controls.Add(tabControl);
            this.Controls.Add(progressBar);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);

            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Create first tab
            CreateNewTab("中国燃气门户", config.HomeUrl, true);

            // Initialize Keep-Alive timer
            keepAliveTimer = new Timer();
            keepAliveTimer.Interval = Math.Max(1, config.KeepAliveMinutes) * 60 * 1000;
            keepAliveTimer.Tick += KeepAliveTimer_Tick;
            keepAliveTimer.Start();

            // Start Tier 3 Intelligent Session Guard
            SessionGuard.OnSessionRescued += SessionGuard_OnSessionRescued;
            SessionGuard.Start(config);
        }

        private void SessionGuard_OnSessionRescued(string msg)
        {
            try
            {
                this.BeginInvoke(new Action(delegate
                {
                    lblKeepAlive.Text = "⚡ [已自动续订] " + msg;
                    lblKeepAlive.ForeColor = Color.FromArgb(0, 102, 204);
                    lblStatus.Text = msg;
                }));
            }
            catch
            {
            }
        }

        #region Tab Management

        public ExtendedWebBrowser GetCurrentBrowser()
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Tag is ExtendedWebBrowser)
            {
                return (ExtendedWebBrowser)tabControl.SelectedTab.Tag;
            }
            return null;
        }

        public ExtendedWebBrowser CreateNewTab(string title, string url, bool selectImmediately)
        {
            TabPage page = new TabPage(title);
            ExtendedWebBrowser b = new ExtendedWebBrowser();
            b.Dock = DockStyle.Fill;
            page.Tag = b;
            page.Controls.Add(b);
            b.CreateControl();

            // Wire browser events
            b.Navigated += delegate(object s, WebBrowserNavigatedEventArgs args)
            {
                if (args.Url != null)
                {
                    NativeMethods.Log("Navigated", string.Format("Tab '{0}' navigated to: {1}", page.Text, args.Url));
                    if (tabControl.SelectedTab == page)
                    {
                        txtUrl.Text = args.Url.ToString();
                    }
                }
            };

            b.DocumentCompleted += delegate(object s, WebBrowserDocumentCompletedEventArgs args)
            {
                if (tabControl.SelectedTab == page)
                {
                    if (b.Url != null) txtUrl.Text = b.Url.ToString();
                    lblStatus.Text = "完成";
                    progressBar.Visible = false;
                }
                if (currentZoom != 100)
                {
                    b.SetZoom(currentZoom);
                }

                // Attempt auto-filling saved login credentials
                TryAutoFillCredentials(b);
            };

            b.DocumentTitleChanged += delegate(object s, EventArgs args)
            {
                string docTitle = b.DocumentTitle;
                if (!string.IsNullOrEmpty(docTitle))
                {
                    // Truncate tab text if too long
                    string tabText = docTitle.Length > 12 ? docTitle.Substring(0, 12) + ".." : docTitle;
                    page.Text = tabText;
                    if (tabControl.SelectedTab == page)
                    {
                        this.Text = string.Format("{0} - {1} (v{2})", docTitle, config.AppTitle, AppVersion);
                    }
                }
            };

            b.StatusTextChanged += delegate(object s, EventArgs args)
            {
                if (tabControl.SelectedTab == page)
                {
                    lblStatus.Text = b.StatusText ?? "就绪";
                }
            };

            b.ProgressChangeEvent += delegate(object s, ProgressChangeEventArgs args)
            {
                if (tabControl.SelectedTab == page)
                {
                    if (args.ProgressMax > 0 && args.Progress >= 0 && args.Progress <= args.ProgressMax)
                    {
                        progressBar.Visible = true;
                        progressBar.Maximum = args.ProgressMax;
                        progressBar.Value = args.Progress;
                        if (args.Progress == args.ProgressMax)
                        {
                            progressBar.Visible = false;
                        }
                    }
                    else
                    {
                        progressBar.Visible = false;
                    }
                }
            };

            b.CanGoBackChanged += delegate
            {
                if (tabControl.SelectedTab == page) btnBack.Enabled = b.CanGoBack;
            };

            b.CanGoForwardChanged += delegate
            {
                if (tabControl.SelectedTab == page) btnForward.Enabled = b.CanGoForward;
            };

            DateTime tabCreatedTime = DateTime.Now;

            // Intercept popup windows and turn them into tabs!
            b.NewWindow3Event += delegate(object s, NewWindow3EventArgs args)
            {
                try
                {
                    NativeMethods.Log("NewWindow3", string.Format("Popup requested. Target URL: {0}, Context: {1}", args.Url, args.UrlContext));
                    if (!string.IsNullOrEmpty(args.Url) && !args.Url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                    {
                        // Direct tab creation and navigation: 100% reliable across all IE/EBS versions!
                        args.Cancel = true;
                        string targetUrl = args.Url;
                        try
                        {
                            if (b.Url != null && !targetUrl.Contains("://"))
                            {
                                targetUrl = new Uri(b.Url, targetUrl).ToString();
                            }
                        }
                        catch {}
                        this.BeginInvoke(new Action(delegate
                        {
                            CreateNewTab("加载中...", targetUrl, true);
                        }));
                        NativeMethods.Log("NewWindow3", "Handled via direct tab navigation to: " + targetUrl);
                    }
                    else
                    {
                        ExtendedWebBrowser newTabBrowser = CreateNewTab("加载中...", null, true);
                        if (newTabBrowser != null)
                        {
                            args.PPDisp = newTabBrowser.ActiveXInstance;
                            NativeMethods.Log("NewWindow3", "Assigned PPDisp for blank popup.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    NativeMethods.Log("NewWindow3_Error", ex.Message);
                }
            };

            b.NewWindow2Event += delegate(object s, NewWindow2EventArgs args)
            {
                try
                {
                    NativeMethods.Log("NewWindow2", "Popup requested via NewWindow2");
                    ExtendedWebBrowser newTabBrowser = CreateNewTab("加载中...", null, true);
                    if (newTabBrowser != null)
                    {
                        args.PPDisp = newTabBrowser.ActiveXInstance;
                        NativeMethods.Log("NewWindow2", "Assigned PPDisp for NewWindow2.");
                    }
                }
                catch (Exception ex)
                {
                    NativeMethods.Log("NewWindow2_Error", ex.Message);
                }
            };

            b.WindowClosingEvent += delegate(object s, WindowClosingEventArgs args)
            {
                try
                {
                    NativeMethods.Log("WindowClosing", string.Format("window.close() called on tab: '{0}', URL: '{1}'", page.Text, b.Url));
                    // CRITICAL: Always keep Cancel = true for COM so MSHTML does not send WM_CLOSE to MainForm!
                    args.Cancel = true;

                    // NEVER close if it's the only tab or the primary home tab:
                    if (page == tabControl.TabPages[0] || tabControl.TabPages.Count <= 1)
                    {
                        NativeMethods.Log("WindowClosing", "Ignored close request for primary tab.");
                        return;
                    }

                    string tabUrl = (b.Url != null) ? b.Url.ToString() : "";
                    string tabTitle = b.DocumentTitle ?? "";
                    bool isEbs = (tabUrl.IndexOf("/OA_HTML/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tabUrl.IndexOf("frmservlet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tabUrl.IndexOf("/forms/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tabTitle.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  tabTitle.IndexOf("EBS", StringComparison.OrdinalIgnoreCase) >= 0);

                    // If it is an EBS Form or was created recently (< 30s) or is busy, NEVER auto-close it!
                    if (isEbs || (DateTime.Now - tabCreatedTime).TotalSeconds < 30 || b.IsBusy)
                    {
                        NativeMethods.Log("WindowClosing", "Ignored close request for active/recent EBS session.");
                        return;
                    }

                    // Only transient helper popups (e.g. intermediate OAuth / SSO popup that finished) can be closed:
                    this.BeginInvoke(new Action(delegate { CloseTab(page, false); }));
                }
                catch (Exception ex)
                {
                    NativeMethods.Log("WindowClosing_Error", ex.Message);
                }
            };

            tabControl.TabPages.Add(page);

            if (selectImmediately)
            {
                tabControl.SelectedTab = page;
            }

            if (!string.IsNullOrEmpty(url))
            {
                b.Navigate(url);
            }

            return b;
        }

        private void CloseTab(TabPage page, bool promptConfirm)
        {
            if (page == null) return;

            ExtendedWebBrowser b = page.Tag as ExtendedWebBrowser;
            if (promptConfirm && b != null)
            {
                string url = (b.Url != null) ? b.Url.ToString() : "";
                string title = b.DocumentTitle ?? "";
                if (url.IndexOf("/OA_HTML/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    url.IndexOf("frmservlet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("EBS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    DialogResult dr = MessageBox.Show(
                        "该页签正在运行 Oracle EBS 业务，关闭将导致 Java 表单立即退出！\r\n\r\n确定要关闭此页签吗？",
                        "安全关闭确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (dr != DialogResult.Yes) return;
                }
            }

            if (tabControl.TabPages.Count <= 1)
            {
                // If it's the last tab, don't close, reset to Home
                if (b != null) b.Navigate(config.HomeUrl);
                page.Text = "中国燃气门户";
            }
            else
            {
                tabControl.TabPages.Remove(page);
                if (b != null)
                {
                    b.Dispose();
                }
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;

            TabPage page = tabControl.TabPages[e.Index];
            Rectangle r = tabControl.GetTabRect(e.Index);
            bool isSelected = (e.Index == tabControl.SelectedIndex);

            // Tab background
            using (SolidBrush bgBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(241, 245, 249)))
            {
                e.Graphics.FillRectangle(bgBrush, r);
            }

            // Top highlight line for selected tab
            if (isSelected)
            {
                using (Pen topPen = new Pen(Color.FromArgb(0, 91, 172), 3))
                {
                    e.Graphics.DrawLine(topPen, r.Left, r.Top + 1, r.Right, r.Top + 1);
                }
            }

            // Tab text
            Rectangle textRect = new Rectangle(r.Left + 8, r.Top + 4, r.Width - 32, r.Height - 6);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
            Color textColor = isSelected ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);
            Font textFont = isSelected ? new Font(this.Font, FontStyle.Bold) : this.Font;
            TextRenderer.DrawText(e.Graphics, page.Text, textFont, textRect, textColor, flags);

            // Close button [x]
            Rectangle closeRect = new Rectangle(r.Right - 18, r.Top + (r.Height - 12) / 2, 12, 12);
            using (Font closeFont = new Font("Arial", 8.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, "×", closeFont, closeRect, Color.FromArgb(148, 163, 184), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                Rectangle r = tabControl.GetTabRect(i);
                Rectangle closeRect = new Rectangle(r.Right - 18, r.Top + (r.Height - 12) / 2, 14, 14);

                // Left click on close button
                if (e.Button == MouseButtons.Left && closeRect.Contains(e.Location))
                {
                    CloseTab(tabControl.TabPages[i], true);
                    return;
                }
                // Middle click on tab header
                if (e.Button == MouseButtons.Middle && r.Contains(e.Location))
                {
                    CloseTab(tabControl.TabPages[i], true);
                    return;
                }
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            ExtendedWebBrowser b = GetCurrentBrowser();
            if (b != null)
            {
                txtUrl.Text = (b.Url != null) ? b.Url.ToString() : "";
                btnBack.Enabled = b.CanGoBack;
                btnForward.Enabled = b.CanGoForward;
                lblStatus.Text = b.StatusText ?? "就绪";
                if (!string.IsNullOrEmpty(b.DocumentTitle))
                {
                    this.Text = b.DocumentTitle + " - " + config.AppTitle;
                }
            }
        }

        #endregion

        #region Navigation & Actions

        private void NavigateTo(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            string target = url.Trim();
            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                target = "https://" + target;
            }

            ExtendedWebBrowser b = GetCurrentBrowser();
            if (b != null)
            {
                try
                {
                    txtUrl.Text = target;
                    lblStatus.Text = "正在加载: " + target;
                    b.Navigate(target);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("网址跳转失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ChangeZoom(int delta)
        {
            int target = currentZoom + delta;
            if (target >= 50 && target <= 250)
            {
                SetZoom(target);
            }
        }

        private void SetZoom(int zoom)
        {
            currentZoom = zoom;
            ExtendedWebBrowser b = GetCurrentBrowser();
            if (b != null)
            {
                b.SetZoom(zoom);
            }
            btnZoomReset.Text = zoom + "%";
            lblZoom.Text = string.Format("缩放: {0}%", zoom);
        }

        private void BtnClearSession_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("是否确认清除当前浏览器会话与 Cookie？\r\n（清除后将清理后台残留的 Java 进程并重新加载登录页面）", "重置会话确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                NativeMethods.ClearSession();
                NativeMethods.CleanZombieJavaProcesses();
                NavigateTo(config.HomeUrl);
            }
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using (SettingsForm sf = new SettingsForm(config))
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    config = ConfigManager.Load();
                    this.Text = config.AppTitle;
                    SetZoom(config.DefaultZoom);
                    keepAliveTimer.Interval = Math.Max(1, config.KeepAliveMinutes) * 60 * 1000;
                    SessionGuard.UpdateConfig(config);

                    // If credentials were just saved, attempt auto-fill on active browser
                    ExtendedWebBrowser cur = GetCurrentBrowser();
                    if (cur != null)
                    {
                        TryAutoFillCredentials(cur);
                    }
                }
            }
        }

        private void TryAutoFillCredentials(ExtendedWebBrowser targetBrowser)
        {
            try
            {
                if (!config.AutoFillLogin) return;
                if (string.IsNullOrEmpty(config.SavedUsername) || string.IsNullOrEmpty(config.SavedPasswordCipher)) return;
                if (targetBrowser == null || targetBrowser.Document == null || targetBrowser.Url == null) return;

                string url = targetBrowser.Url.ToString().ToLower();
                if (url.IndexOf("portal.chinagasholdings.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    url.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    url.IndexOf("frontend", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string plainPassword = CredentialManager.DecryptPassword(config.SavedPasswordCipher);
                    if (string.IsNullOrEmpty(plainPassword)) return;

                    string safeUser = config.SavedUsername.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
                    string safePwd = plainPassword.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");

                    string script = "(function(){" +
                        "try{" +
                        "  var user = '" + safeUser + "';" +
                        "  var pwd = '" + safePwd + "';" +
                        "  var pwdInputs = document.querySelectorAll('input[type=\"password\"]');" +
                        "  if(pwdInputs.length > 0){" +
                        "    var pwdInp = pwdInputs[0];" +
                        "    var form = pwdInp.form || document;" +
                        "    var txtInputs = form.querySelectorAll('input[type=\"text\"], input:not([type])');" +
                        "    for(var i=0; i<txtInputs.length; i++){" +
                        "      var inp = txtInputs[i];" +
                        "      if(inp.offsetParent !== null && !inp.readOnly && !inp.disabled){" +
                        "        inp.value = user;" +
                        "        var evt1 = document.createEvent('HTMLEvents'); evt1.initEvent('input', true, true); inp.dispatchEvent(evt1);" +
                        "        var evt2 = document.createEvent('HTMLEvents'); evt2.initEvent('change', true, true); inp.dispatchEvent(evt2);" +
                        "        break;" +
                        "      }" +
                        "    }" +
                        "    pwdInp.value = pwd;" +
                        "    var pevt1 = document.createEvent('HTMLEvents'); pevt1.initEvent('input', true, true); pwdInp.dispatchEvent(pevt1);" +
                        "    var pevt2 = document.createEvent('HTMLEvents'); pevt2.initEvent('change', true, true); pwdInp.dispatchEvent(pevt2);" +
                        "  }" +
                        "}catch(e){}" +
                        "})();";
                    targetBrowser.Document.InvokeScript("eval", new object[] { script });
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Keep-Alive Logic

        private void KeepAliveTimer_Tick(object sender, EventArgs e)
        {
            if (!isKeepAliveEnabled) return;
            SendKeepAlive();
        }

        private void SendKeepAlive()
        {
            try
            {
                NativeMethods.PreventSleep();

                // 1. Native C# background HTTP keepalive to refresh database ICX_SESSIONS timestamp directly
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    NativeMethods.SendNativeEbsPing();
                    NativeMethods.SendNativePortalPing();
                });

                // 2. Forms AWT Canvas and input message pump pulse
                int pulsedForms = NativeMethods.PulseFormsActivity();

                // 3. Tab browser dynamic script refresh
                int pingedTabs = 0;
                foreach (TabPage page in tabControl.TabPages)
                {
                    ExtendedWebBrowser b = page.Tag as ExtendedWebBrowser;
                    if (b != null && ExecuteKeepAlivePing(b))
                    {
                        pingedTabs++;
                    }
                }

                string formsStatus = pulsedForms > 0 ? string.Format("Forms在线({0})", pulsedForms) : "未检测到Forms";
                lblKeepAlive.Text = string.Format("⚡ 防超时: 在线 ({0:HH:mm} | {1})", DateTime.Now, formsStatus);
                lblKeepAlive.ToolTipText = string.Format("上次保活运行: {0:yyyy-MM-dd HH:mm:ss}\r\n- 底层C#与数据库会话直接续订: 成功\r\n- Web页签会话刷新数: {1}\r\n- Forms业务窗口看护数: {2}\r\n- 智能会话看护防掉线引擎: 运行中\r\n（点击可立即手动执行一次保活）", DateTime.Now, pingedTabs, pulsedForms);
            }
            catch
            {
            }
        }

        private bool ExecuteKeepAlivePing(ExtendedWebBrowser targetBrowser)
        {
            try
            {
                if (targetBrowser != null && targetBrowser.Document != null && targetBrowser.Url != null)
                {
                    // Send dynamic XHR request to OA.jsp to refresh ICX_SESSIONS.LAST_CONNECT in Oracle database
                    string script = "(function(){" +
                        "try{" +
                        "  var host = window.location.protocol + '//' + window.location.host;" +
                        "  var href = window.location.href;" +
                        "  if(host.indexOf('ebs.') !== -1 || href.indexOf('/OA_HTML/') !== -1 || href.indexOf('/forms/') !== -1){" +
                        "    var xhr = new XMLHttpRequest();" +
                        "    xhr.open('GET', host + '/OA_HTML/OA.jsp?OAFunc=OANEWHOMEPAGE&_ka=' + (new Date()).getTime(), true);" +
                        "    xhr.withCredentials = true;" +
                        "    xhr.send(null);" +
                        "    var img = new Image();" +
                        "    img.src = host + '/OA_HTML/cabo/images/skyros/t.gif?_t=' + (new Date()).getTime();" +
                        "  }" +
                        "  if(host.indexOf('portal.') !== -1 || href.indexOf('chinagas') !== -1){" +
                        "    var xhr2 = new XMLHttpRequest();" +
                        "    xhr2.open('GET', host + '/group/frontend?_ka=' + (new Date()).getTime(), true);" +
                        "    xhr2.withCredentials = true;" +
                        "    xhr2.send(null);" +
                        "    var xhr3 = new XMLHttpRequest();" +
                        "    xhr3.open('GET', host + '/c/portal/extend_session?_t=' + (new Date()).getTime(), true);" +
                        "    xhr3.withCredentials = true;" +
                        "    xhr3.send(null);" +
                        "  }" +
                        "}catch(e){}" +
                        "})();";
                    targetBrowser.Document.InvokeScript("eval", new object[] { script });
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void BtnKeepAlive_Click(object sender, EventArgs e)
        {
            isKeepAliveEnabled = !isKeepAliveEnabled;
            if (isKeepAliveEnabled)
            {
                btnKeepAlive.Text = "⚡ 防超时: 开";
                btnKeepAlive.ForeColor = Color.FromArgb(16, 128, 64);
                lblKeepAlive.Text = "⚡ 防超时: 激活";
                keepAliveTimer.Start();
                SessionGuard.Start(config);
                SendKeepAlive();
            }
            else
            {
                btnKeepAlive.Text = "⚡ 防超时: 关";
                btnKeepAlive.ForeColor = Color.FromArgb(120, 120, 120);
                lblKeepAlive.Text = "⚡ 防超时: 已关闭";
                keepAliveTimer.Stop();
                SessionGuard.Stop();
            }
        }

        #endregion

        #region Keyboard Shortcuts

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.T)
            {
                CreateNewTab("新页签", config.HomeUrl, true);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.W)
            {
                if (tabControl.SelectedTab != null)
                {
                    CloseTab(tabControl.SelectedTab, true);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ExtendedWebBrowser b = GetCurrentBrowser();
                if (b != null) b.Refresh();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Left)
            {
                ExtendedWebBrowser b = GetCurrentBrowser();
                if (b != null && b.CanGoBack) b.GoBack();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                ExtendedWebBrowser b = GetCurrentBrowser();
                if (b != null && b.CanGoForward) b.GoForward();
                e.Handled = true;
            }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                ChangeZoom(10);
                e.Handled = true;
            }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                ChangeZoom(-10);
                e.Handled = true;
            }
            else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
            {
                SetZoom(100);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.L)
            {
                txtUrl.Focus();
                txtUrl.SelectAll();
                e.Handled = true;
            }
        }

        #endregion

        #region Form Lifecycle & Protection

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            NativeMethods.Log("FormClosing", string.Format("OnFormClosing called, CloseReason: {0}", e.CloseReason));

            // Block external / COM / MSHTML initiated closures (e.g. window.close() sending WM_CLOSE to host form)
            if (e.CloseReason == CloseReason.None)
            {
                e.Cancel = true;
                NativeMethods.Log("FormClosing", "BLOCKED unexpected external CloseReason.None from closing MainForm.");
                return;
            }

            if (e.CloseReason == CloseReason.UserClosing)
            {
                bool hasEbs = false;
                foreach (TabPage p in tabControl.TabPages)
                {
                    ExtendedWebBrowser eb = p.Tag as ExtendedWebBrowser;
                    if (eb != null)
                    {
                        string u = (eb.Url != null) ? eb.Url.ToString() : "";
                        string t = eb.DocumentTitle ?? "";
                        if (u.IndexOf("/OA_HTML/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            u.IndexOf("frmservlet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.IndexOf("EBS", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasEbs = true;
                            break;
                        }
                    }
                }

                if (hasEbs)
                {
                    DialogResult dr = MessageBox.Show(
                        "当前正在运行 Oracle EBS 业务表单，退出工作台将导致表单立即断开并可能丢失未保存数据！\r\n\r\n确定要退出吗？",
                        "退出确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (dr != DialogResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            // Legitimate exit allowed
            NativeMethods.AllowProcessExit = true;
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_CLOSE = 0x0010;
            if (m.Msg == WM_CLOSE)
            {
                NativeMethods.Log("WndProc", "WM_CLOSE message received.");
            }
            base.WndProc(ref m);
        }

        #endregion
    }
}
