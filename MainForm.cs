using System;
using System.Drawing;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class MainForm : Form
    {
        private AppConfig config;
        private ExtendedWebBrowser browser;
        private ToolStripButton btnBack;
        private ToolStripButton btnForward;
        private ToolStripButton btnRefresh;
        private ToolStripButton btnHome;
        private ToolStripTextBox txtUrl;
        private ToolStripButton btnGo;
        private ToolStripButton btnZoomOut;
        private ToolStripButton btnZoomReset;
        private ToolStripButton btnZoomIn;
        private ToolStripButton btnClearSession;
        private ToolStripButton btnSettings;
        private ProgressBar progressBar;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripStatusLabel lblZoom;
        private ToolStripStatusLabel lblArch;

        private int currentZoom = 100;

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
            btnBack.Click += delegate { if (browser.CanGoBack) browser.GoBack(); };

            btnForward = new ToolStripButton("▶ 前进");
            btnForward.ToolTipText = "前进 (Alt+Right)";
            btnForward.Enabled = false;
            btnForward.Click += delegate { if (browser.CanGoForward) browser.GoForward(); };

            btnRefresh = new ToolStripButton("🔄 刷新");
            btnRefresh.ToolTipText = "刷新页面 (F5)";
            btnRefresh.Click += delegate { browser.Refresh(); };

            btnHome = new ToolStripButton("🏠 燃气门户");
            btnHome.ToolTipText = "返回登录门户首页";
            btnHome.Font = new Font(this.Font, FontStyle.Bold);
            btnHome.ForeColor = Color.FromArgb(0, 91, 172);
            btnHome.Click += delegate { NavigateTo(config.HomeUrl); };

            ToolStripLabel lblAddress = new ToolStripLabel("地址:");
            lblAddress.ForeColor = Color.FromArgb(100, 116, 139);

            txtUrl = new ToolStripTextBox();
            txtUrl.Size = new Size(500, 26);
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
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(lblAddress);
            toolStrip.Items.Add(txtUrl);
            toolStrip.Items.Add(btnGo);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnZoomOut);
            toolStrip.Items.Add(btnZoomReset);
            toolStrip.Items.Add(btnZoomIn);
            toolStrip.Items.Add(new ToolStripSeparator());
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

            lblArch = new ToolStripStatusLabel("内核: 32位 IE11 (兼容 Java 6 JRE)");
            lblArch.BorderSides = ToolStripStatusLabelBorderSides.Left;
            lblArch.ForeColor = Color.FromArgb(40, 110, 40);
            lblArch.Padding = new Padding(8, 0, 8, 0);

            statusStrip.Items.Add(lblStatus);
            statusStrip.Items.Add(lblZoom);
            statusStrip.Items.Add(lblArch);

            // --- Extended Browser Control ---
            browser = new ExtendedWebBrowser();
            browser.Dock = DockStyle.Fill;
            browser.CanGoBackChanged += delegate { btnBack.Enabled = browser.CanGoBack; };
            browser.CanGoForwardChanged += delegate { btnForward.Enabled = browser.CanGoForward; };
            browser.Navigated += Browser_Navigated;
            browser.DocumentCompleted += Browser_DocumentCompleted;
            browser.DocumentTitleChanged += Browser_DocumentTitleChanged;
            browser.StatusTextChanged += Browser_StatusTextChanged;
            browser.ProgressChangeEvent += Browser_ProgressChangeEvent;
            browser.NewWindow3Event += Browser_NewWindow3Event;
            browser.NewWindow2Event += Browser_NewWindow2Event;

            // Layout assembly
            this.Controls.Add(browser);
            this.Controls.Add(progressBar);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);

            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            NavigateTo(config.HomeUrl);
        }

        private void NavigateTo(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            string target = url.Trim();
            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                target = "https://" + target;
            }

            try
            {
                txtUrl.Text = target;
                lblStatus.Text = "正在加载: " + target;
                browser.Navigate(target);
            }
            catch (Exception ex)
            {
                MessageBox.Show("网址跳转失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            browser.SetZoom(zoom);
            btnZoomReset.Text = zoom + "%";
            lblZoom.Text = string.Format("缩放: {0}%", zoom);
        }

        private void Browser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            if (e.Url != null)
            {
                txtUrl.Text = e.Url.ToString();
            }
        }

        private void Browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (browser.Url != null)
            {
                txtUrl.Text = browser.Url.ToString();
            }
            lblStatus.Text = "完成";
            progressBar.Visible = false;

            // Apply default zoom once document is ready
            if (currentZoom != 100)
            {
                browser.SetZoom(currentZoom);
            }
        }

        private void Browser_DocumentTitleChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(browser.DocumentTitle))
            {
                this.Text = browser.DocumentTitle + " - " + config.AppTitle;
            }
        }

        private void Browser_StatusTextChanged(object sender, EventArgs e)
        {
            lblStatus.Text = browser.StatusText ?? "就绪";
        }

        private void Browser_ProgressChangeEvent(object sender, ProgressChangeEventArgs e)
        {
            if (e.ProgressMax > 0 && e.Progress >= 0 && e.Progress <= e.ProgressMax)
            {
                progressBar.Visible = true;
                progressBar.Maximum = e.ProgressMax;
                progressBar.Value = e.Progress;
                if (e.Progress == e.ProgressMax)
                {
                    progressBar.Visible = false;
                }
            }
            else
            {
                progressBar.Visible = false;
            }
        }

        private void Browser_NewWindow3Event(object sender, NewWindow3EventArgs e)
        {
            // Open EBS popup in managed popup window sharing session cookies
            PopupWindow popup = new PopupWindow();
            e.PPDisp = popup.Browser.ActiveXInstance;
            popup.Show();
        }

        private void Browser_NewWindow2Event(object sender, NewWindow2EventArgs e)
        {
            PopupWindow popup = new PopupWindow();
            e.PPDisp = popup.Browser.ActiveXInstance;
            popup.Show();
        }

        private void BtnClearSession_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("是否确认清除当前浏览器会话与 Cookie？\r\n（清除后将重新加载登录页面）", "重置会话确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                NativeMethods.ClearSession();
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

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                browser.Refresh();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Left)
            {
                if (browser.CanGoBack) browser.GoBack();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                if (browser.CanGoForward) browser.GoForward();
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
    }
}
