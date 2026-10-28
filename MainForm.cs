using System;
using System.Drawing;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class MainForm : Form
    {
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

        public MainForm()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch {}
            config = ConfigManager.Load();
            currentZoom = config.DefaultZoom;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = config.AppTitle;
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

            lblArch = new ToolStripStatusLabel("内核: 32位 IE11 (多页签) | v1.0.3");
            lblArch.BorderSides = ToolStripStatusLabelBorderSides.Left;
            lblArch.ForeColor = Color.FromArgb(40, 110, 40);
            lblArch.Padding = new Padding(8, 0, 8, 0);

            statusStrip.Items.Add(lblStatus);
            statusStrip.Items.Add(lblZoom);
            statusStrip.Items.Add(lblKeepAlive);
            statusStrip.Items.Add(lblArch);

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
            keepAliveTimer.Interval = 180000; // 3 minutes interval
            keepAliveTimer.Tick += KeepAliveTimer_Tick;
            keepAliveTimer.Start();
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

            // Wire browser events
            b.Navigated += delegate(object s, WebBrowserNavigatedEventArgs args)
            {
                if (tabControl.SelectedTab == page && args.Url != null)
                {
                    txtUrl.Text = args.Url.ToString();
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
                        this.Text = docTitle + " - " + config.AppTitle;
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

            // Intercept popup windows and turn them into tabs!
            b.NewWindow3Event += delegate(object s, NewWindow3EventArgs args)
            {
                ExtendedWebBrowser newTabBrowser = CreateNewTab("加载中...", null, true);
                args.PPDisp = newTabBrowser.ActiveXInstance;
            };

            b.NewWindow2Event += delegate(object s, NewWindow2EventArgs args)
            {
                ExtendedWebBrowser newTabBrowser = CreateNewTab("加载中...", null, true);
                args.PPDisp = newTabBrowser.ActiveXInstance;
            };

            b.WindowClosingEvent += delegate(object s, WindowClosingEventArgs args)
            {
                args.Cancel = false;
                this.BeginInvoke(new Action(delegate { CloseTab(page, false); }));
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
                }
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
                NativeMethods.PulseFormsActivity();

                // Send Keep-Alive ping to all open tab browsers
                foreach (TabPage page in tabControl.TabPages)
                {
                    ExtendedWebBrowser b = page.Tag as ExtendedWebBrowser;
                    if (b != null)
                    {
                        ExecuteKeepAlivePing(b);
                    }
                }

                lblKeepAlive.Text = string.Format("⚡ 保活已刷新 ({0:HH:mm:ss})", DateTime.Now);
            }
            catch
            {
            }
        }

        private void ExecuteKeepAlivePing(ExtendedWebBrowser targetBrowser)
        {
            try
            {
                if (targetBrowser != null && targetBrowser.Document != null && targetBrowser.Url != null)
                {
                    string script = "(function(){" +
                        "try{" +
                        "  var img = new Image();" +
                        "  var host = window.location.protocol + '//' + window.location.host;" +
                        "  if(window.location.href.indexOf('/OA_HTML/') !== -1){" +
                        "    img.src = host + '/OA_HTML/blank.html?_t=' + (new Date()).getTime();" +
                        "  } else {" +
                        "    img.src = host + '/group/frontend?_t=' + (new Date()).getTime();" +
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

        private void BtnKeepAlive_Click(object sender, EventArgs e)
        {
            isKeepAliveEnabled = !isKeepAliveEnabled;
            if (isKeepAliveEnabled)
            {
                btnKeepAlive.Text = "⚡ 防超时: 开";
                btnKeepAlive.ForeColor = Color.FromArgb(16, 128, 64);
                lblKeepAlive.Text = "⚡ 防超时: 激活";
                keepAliveTimer.Start();
                SendKeepAlive();
            }
            else
            {
                btnKeepAlive.Text = "⚡ 防超时: 关";
                btnKeepAlive.ForeColor = Color.FromArgb(120, 120, 120);
                lblKeepAlive.Text = "⚡ 防超时: 已关闭";
                keepAliveTimer.Stop();
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
    }
}
