using System;
using System.Drawing;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class PopupWindow : Form
    {
        public ExtendedWebBrowser Browser { get; private set; }
        private ProgressBar progressBar;
        private ToolStripStatusLabel lblStatus;

        public PopupWindow()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch {}
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "EBS 业务窗口";
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);

            // Top Toolbar for convenience
            ToolStrip toolStrip = new ToolStrip();
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Padding = new Padding(6, 4, 6, 4);
            toolStrip.BackColor = Color.FromArgb(245, 247, 250);
            toolStrip.RenderMode = ToolStripRenderMode.System;

            ToolStripButton btnRefresh = new ToolStripButton("🔄 刷新");
            btnRefresh.Click += delegate { if (Browser != null) Browser.Refresh(); };

            ToolStripButton btnZoomIn = new ToolStripButton("🔍+");
            btnZoomIn.ToolTipText = "放大 10%";
            btnZoomIn.Click += delegate { ChangeZoom(10); };

            ToolStripButton btnZoomOut = new ToolStripButton("🔍-");
            btnZoomOut.ToolTipText = "缩小 10%";
            btnZoomOut.Click += delegate { ChangeZoom(-10); };

            ToolStripButton btnZoomReset = new ToolStripButton("100%");
            btnZoomReset.ToolTipText = "恢复 100%";
            btnZoomReset.Click += delegate { SetZoom(100); };

            toolStrip.Items.Add(btnRefresh);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnZoomOut);
            toolStrip.Items.Add(btnZoomReset);
            toolStrip.Items.Add(btnZoomIn);

            // Progress bar
            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 3;
            progressBar.Visible = false;
            progressBar.Style = ProgressBarStyle.Continuous;

            // Status strip
            StatusStrip statusStrip = new StatusStrip();
            statusStrip.BackColor = Color.FromArgb(240, 242, 245);
            lblStatus = new ToolStripStatusLabel("准备就绪");
            statusStrip.Items.Add(lblStatus);

            // Extended Browser
            Browser = new ExtendedWebBrowser();
            Browser.Dock = DockStyle.Fill;
            Browser.DocumentTitleChanged += Browser_DocumentTitleChanged;
            Browser.StatusTextChanged += Browser_StatusTextChanged;
            Browser.ProgressChangeEvent += Browser_ProgressChangeEvent;
            Browser.WindowClosingEvent += Browser_WindowClosingEvent;
            Browser.NewWindow3Event += Browser_NewWindow3Event;
            Browser.NewWindow2Event += Browser_NewWindow2Event;

            this.Controls.Add(Browser);
            this.Controls.Add(progressBar);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);
        }

        private int currentZoom = 100;

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
            Browser.SetZoom(zoom);
            lblStatus.Text = string.Format("缩放: {0}%", currentZoom);
        }

        private void Browser_DocumentTitleChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Browser.DocumentTitle))
            {
                this.Text = Browser.DocumentTitle + " - EBS 业务窗口";
            }
        }

        private void Browser_StatusTextChanged(object sender, EventArgs e)
        {
            lblStatus.Text = Browser.StatusText ?? "就绪";
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

        private void Browser_WindowClosingEvent(object sender, WindowClosingEventArgs e)
        {
            // Close popup form when JavaScript calls window.close()
            e.Cancel = false;
            this.BeginInvoke(new Action(delegate { this.Close(); }));
        }

        private void Browser_NewWindow3Event(object sender, NewWindow3EventArgs e)
        {
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
    }
}
