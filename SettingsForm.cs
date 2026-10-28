using System;
using System.Drawing;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class SettingsForm : Form
    {
        private AppConfig config;
        private TextBox txtHomeUrl;
        private NumericUpDown numZoom;
        private TextBox txtTitle;

        public SettingsForm(AppConfig currentConfig)
        {
            this.config = currentConfig;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "浏览器设置";
            this.Size = new Size(520, 260);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);

            Label lblTitle = new Label() { Text = "窗口标题:", Location = new Point(25, 25), AutoSize = true };
            txtTitle = new TextBox() { Location = new Point(110, 22), Size = new Size(360, 24), Text = config.AppTitle };

            Label lblUrl = new Label() { Text = "门户首页:", Location = new Point(25, 65), AutoSize = true };
            txtHomeUrl = new TextBox() { Location = new Point(110, 62), Size = new Size(360, 24), Text = config.HomeUrl };

            Label lblZoom = new Label() { Text = "默认缩放:", Location = new Point(25, 105), AutoSize = true };
            numZoom = new NumericUpDown() { Location = new Point(110, 102), Size = new Size(80, 24), Minimum = 50, Maximum = 250, Value = config.DefaultZoom, Increment = 5 };
            Label lblPercent = new Label() { Text = "%", Location = new Point(195, 105), AutoSize = true };

            Button btnSave = new Button() { Text = "保存", DialogResult = DialogResult.OK, Location = new Point(285, 160), Size = new Size(85, 30) };
            btnSave.Click += BtnSave_Click;

            Button btnCancel = new Button() { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(385, 160), Size = new Size(85, 30) };

            this.Controls.Add(lblTitle);
            this.Controls.Add(txtTitle);
            this.Controls.Add(lblUrl);
            this.Controls.Add(txtHomeUrl);
            this.Controls.Add(lblZoom);
            this.Controls.Add(numZoom);
            this.Controls.Add(lblPercent);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtHomeUrl.Text))
            {
                config.HomeUrl = txtHomeUrl.Text.Trim();
            }
            if (!string.IsNullOrEmpty(txtTitle.Text))
            {
                config.AppTitle = txtTitle.Text.Trim();
            }
            config.DefaultZoom = (int)numZoom.Value;

            ConfigManager.Save(config);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
