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
        private NumericUpDown numKeepAlive;
        private TextBox txtTitle;

        private TextBox txtUsername;
        private TextBox txtPassword;
        private CheckBox chkAutoFill;
        private Button btnClearCred;

        public SettingsForm(AppConfig currentConfig)
        {
            this.config = currentConfig;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "浏览器与工作台设置";
            this.Size = new Size(550, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);

            // --- 基础参数 ---
            Label lblTitle = new Label() { Text = "窗口标题:", Location = new Point(25, 25), AutoSize = true };
            txtTitle = new TextBox() { Location = new Point(125, 22), Size = new Size(375, 24), Text = config.AppTitle };

            Label lblUrl = new Label() { Text = "门户首页:", Location = new Point(25, 65), AutoSize = true };
            txtHomeUrl = new TextBox() { Location = new Point(125, 62), Size = new Size(375, 24), Text = config.HomeUrl };

            Label lblZoom = new Label() { Text = "默认缩放:", Location = new Point(25, 105), AutoSize = true };
            numZoom = new NumericUpDown() { Location = new Point(125, 102), Size = new Size(75, 24), Minimum = 50, Maximum = 250, Value = config.DefaultZoom, Increment = 5 };
            Label lblPercent = new Label() { Text = "%", Location = new Point(205, 105), AutoSize = true };

            // 心跳保活时间设置
            Label lblKeepAlive = new Label() { Text = "保活心跳间隔:", Location = new Point(255, 105), AutoSize = true };
            numKeepAlive = new NumericUpDown() { Location = new Point(355, 102), Size = new Size(75, 24), Minimum = 1, Maximum = 30, Value = config.KeepAliveMinutes, Increment = 1 };
            Label lblMinutes = new Label() { Text = "分钟", Location = new Point(435, 105), AutoSize = true };

            // --- 账号密码安全凭据存储 ---
            GroupBox grpLogin = new GroupBox();
            grpLogin.Text = "门户登录凭据（Windows DPAPI 安全加密保存在本机）";
            grpLogin.Location = new Point(20, 150);
            grpLogin.Size = new Size(495, 220);
            grpLogin.Font = new Font(this.Font, FontStyle.Regular);

            Label lblUser = new Label() { Text = "登录工号/账号:", Location = new Point(20, 35), AutoSize = true };
            txtUsername = new TextBox() { Location = new Point(125, 32), Size = new Size(340, 24), Text = config.SavedUsername };

            Label lblPwd = new Label() { Text = "登录密码:", Location = new Point(20, 75), AutoSize = true };
            txtPassword = new TextBox() { Location = new Point(125, 72), Size = new Size(340, 24), UseSystemPasswordChar = true };

            // Decrypt password if saved
            if (!string.IsNullOrEmpty(config.SavedPasswordCipher))
            {
                txtPassword.Text = CredentialManager.DecryptPassword(config.SavedPasswordCipher);
            }

            chkAutoFill = new CheckBox()
            {
                Text = "打开门户登录页面时，自动填写账号与密码",
                Location = new Point(125, 115),
                AutoSize = true,
                Checked = config.AutoFillLogin
            };

            btnClearCred = new Button()
            {
                Text = "清空已存密码",
                Location = new Point(125, 150),
                Size = new Size(110, 28)
            };
            btnClearCred.Click += delegate
            {
                txtUsername.Text = "";
                txtPassword.Text = "";
                config.SavedUsername = "";
                config.SavedPasswordCipher = "";
                MessageBox.Show("已清空本地保存的登录凭据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Label lblSecurityTip = new Label()
            {
                Text = "提示：密码采用当前 Windows 用户密钥安全加密，仅本机可用，保护企业账号安全。",
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(20, 185),
                Size = new Size(460, 24),
                AutoSize = false
            };

            grpLogin.Controls.Add(lblUser);
            grpLogin.Controls.Add(txtUsername);
            grpLogin.Controls.Add(lblPwd);
            grpLogin.Controls.Add(txtPassword);
            grpLogin.Controls.Add(chkAutoFill);
            grpLogin.Controls.Add(btnClearCred);
            grpLogin.Controls.Add(lblSecurityTip);

            // --- 底部按钮 ---
            Button btnSave = new Button() { Text = "保存设置", DialogResult = DialogResult.OK, Location = new Point(315, 390), Size = new Size(95, 32) };
            btnSave.Click += BtnSave_Click;

            Button btnCancel = new Button() { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(420, 390), Size = new Size(95, 32) };

            this.Controls.Add(lblTitle);
            this.Controls.Add(txtTitle);
            this.Controls.Add(lblUrl);
            this.Controls.Add(txtHomeUrl);
            this.Controls.Add(lblZoom);
            this.Controls.Add(numZoom);
            this.Controls.Add(lblPercent);
            this.Controls.Add(lblKeepAlive);
            this.Controls.Add(numKeepAlive);
            this.Controls.Add(lblMinutes);
            this.Controls.Add(grpLogin);
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
            config.KeepAliveMinutes = (int)numKeepAlive.Value;
            config.AutoFillLogin = chkAutoFill.Checked;

            // Save encrypted credentials
            config.SavedUsername = txtUsername.Text.Trim();
            if (!string.IsNullOrEmpty(txtPassword.Text))
            {
                config.SavedPasswordCipher = CredentialManager.EncryptPassword(txtPassword.Text);
            }
            else
            {
                config.SavedPasswordCipher = "";
            }

            ConfigManager.Save(config);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
