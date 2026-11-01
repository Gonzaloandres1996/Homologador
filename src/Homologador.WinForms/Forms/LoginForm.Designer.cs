#nullable enable
namespace Homologador.WinForms.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.Panel _cardPanel = null!;
    private System.Windows.Forms.Label _lblBrand = null!;
    private System.Windows.Forms.Label _lblTitle = null!;
    private System.Windows.Forms.Label _lblEmail = null!;
    private System.Windows.Forms.Panel _pnlEmailBox = null!;
    private System.Windows.Forms.TextBox _txtEmail = null!;
    private System.Windows.Forms.Label _lblPassword = null!;
    private System.Windows.Forms.Panel _pnlPasswordBox = null!;
    private System.Windows.Forms.TextBox _txtPassword = null!;
    private System.Windows.Forms.Button _btnLogin = null!;
    private System.Windows.Forms.Label _lblStatus = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this._cardPanel = new System.Windows.Forms.Panel();
        this._lblBrand = new System.Windows.Forms.Label();
        this._lblTitle = new System.Windows.Forms.Label();
        this._lblEmail = new System.Windows.Forms.Label();
        this._pnlEmailBox = new System.Windows.Forms.Panel();
        this._txtEmail = new System.Windows.Forms.TextBox();
        this._lblPassword = new System.Windows.Forms.Label();
        this._pnlPasswordBox = new System.Windows.Forms.Panel();
        this._txtPassword = new System.Windows.Forms.TextBox();
        this._btnLogin = new System.Windows.Forms.Button();
        this._lblStatus = new System.Windows.Forms.Label();
        this._cardPanel.SuspendLayout();
        this._pnlEmailBox.SuspendLayout();
        this._pnlPasswordBox.SuspendLayout();
        this.SuspendLayout();
        // 
        // _cardPanel
        // 
        this._cardPanel.Anchor = System.Windows.Forms.AnchorStyles.None;
        this._cardPanel.BackColor = System.Drawing.Color.White;
        this._cardPanel.Controls.Add(this._lblBrand);
        this._cardPanel.Controls.Add(this._lblTitle);
        this._cardPanel.Controls.Add(this._lblEmail);
        this._cardPanel.Controls.Add(this._pnlEmailBox);
        this._cardPanel.Controls.Add(this._lblPassword);
        this._cardPanel.Controls.Add(this._pnlPasswordBox);
        this._cardPanel.Controls.Add(this._btnLogin);
        this._cardPanel.Controls.Add(this._lblStatus);
        this._cardPanel.Location = new System.Drawing.Point(80, 60);
        this._cardPanel.Name = "_cardPanel";
        this._cardPanel.Size = new System.Drawing.Size(420, 405);
        this._cardPanel.TabIndex = 0;
        // 
        // _lblBrand
        // 
        this._lblBrand.AutoSize = true;
        this._lblBrand.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold);
        this._lblBrand.ForeColor = System.Drawing.Color.FromArgb(0, 46, 255);
        this._lblBrand.Location = new System.Drawing.Point(36, 36);
        this._lblBrand.Name = "_lblBrand";
        this._lblBrand.Size = new System.Drawing.Size(230, 19);
        this._lblBrand.TabIndex = 0;
        this._lblBrand.Text = "Homologador carga masiva DDC";
        // 
        // _lblTitle
        // 
        this._lblTitle.AutoSize = true;
        this._lblTitle.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold);
        this._lblTitle.ForeColor = System.Drawing.Color.FromArgb(20, 24, 35);
        this._lblTitle.Location = new System.Drawing.Point(34, 64);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(185, 37);
        this._lblTitle.TabIndex = 1;
        this._lblTitle.Text = "Iniciar sesión";
        // 
        // _lblEmail
        // 
        this._lblEmail.AutoSize = true;
        this._lblEmail.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular);
        this._lblEmail.ForeColor = System.Drawing.Color.FromArgb(55, 65, 81);
        this._lblEmail.Location = new System.Drawing.Point(36, 126);
        this._lblEmail.Name = "_lblEmail";
        this._lblEmail.Size = new System.Drawing.Size(51, 17);
        this._lblEmail.TabIndex = 2;
        this._lblEmail.Text = "Usuario";
        // 
        // _pnlEmailBox
        // 
        this._pnlEmailBox.BackColor = System.Drawing.Color.White;
        this._pnlEmailBox.Controls.Add(this._txtEmail);
        this._pnlEmailBox.Location = new System.Drawing.Point(36, 148);
        this._pnlEmailBox.Name = "_pnlEmailBox";
        this._pnlEmailBox.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
        this._pnlEmailBox.Size = new System.Drawing.Size(348, 38);
        this._pnlEmailBox.TabIndex = 3;
        // 
        // _txtEmail
        // 
        this._txtEmail.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this._txtEmail.Dock = System.Windows.Forms.DockStyle.Fill;
        this._txtEmail.Font = new System.Drawing.Font("Segoe UI", 10.5F);
        this._txtEmail.ForeColor = System.Drawing.Color.FromArgb(20, 24, 35);
        this._txtEmail.Location = new System.Drawing.Point(10, 8);
        this._txtEmail.Name = "_txtEmail";
        this._txtEmail.Size = new System.Drawing.Size(328, 19);
        this._txtEmail.TabIndex = 0;
        // 
        // _lblPassword
        // 
        this._lblPassword.AutoSize = true;
        this._lblPassword.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular);
        this._lblPassword.ForeColor = System.Drawing.Color.FromArgb(55, 65, 81);
        this._lblPassword.Location = new System.Drawing.Point(36, 202);
        this._lblPassword.Name = "_lblPassword";
        this._lblPassword.Size = new System.Drawing.Size(73, 17);
        this._lblPassword.TabIndex = 4;
        this._lblPassword.Text = "Contraseña";
        // 
        // _pnlPasswordBox
        // 
        this._pnlPasswordBox.BackColor = System.Drawing.Color.White;
        this._pnlPasswordBox.Controls.Add(this._txtPassword);
        this._pnlPasswordBox.Location = new System.Drawing.Point(36, 224);
        this._pnlPasswordBox.Name = "_pnlPasswordBox";
        this._pnlPasswordBox.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
        this._pnlPasswordBox.Size = new System.Drawing.Size(348, 38);
        this._pnlPasswordBox.TabIndex = 5;
        // 
        // _txtPassword
        // 
        this._txtPassword.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this._txtPassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this._txtPassword.Font = new System.Drawing.Font("Segoe UI", 10.5F);
        this._txtPassword.ForeColor = System.Drawing.Color.FromArgb(20, 24, 35);
        this._txtPassword.Location = new System.Drawing.Point(10, 8);
        this._txtPassword.Name = "_txtPassword";
        this._txtPassword.Size = new System.Drawing.Size(328, 19);
        this._txtPassword.TabIndex = 0;
        this._txtPassword.UseSystemPasswordChar = true;
        // 
        // _btnLogin
        // 
        this._btnLogin.BackColor = System.Drawing.Color.FromArgb(0, 46, 255);
        this._btnLogin.Cursor = System.Windows.Forms.Cursors.Hand;
        this._btnLogin.FlatAppearance.BorderSize = 0;
        this._btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this._btnLogin.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold);
        this._btnLogin.ForeColor = System.Drawing.Color.White;
        this._btnLogin.Location = new System.Drawing.Point(36, 286);
        this._btnLogin.Name = "_btnLogin";
        this._btnLogin.Size = new System.Drawing.Size(348, 44);
        this._btnLogin.TabIndex = 6;
        this._btnLogin.Text = "Ingresar";
        this._btnLogin.UseVisualStyleBackColor = false;
        this._btnLogin.Click += new System.EventHandler(this.OnBtnLoginClick);
        // 
        // _lblStatus
        // 
        this._lblStatus.Font = new System.Drawing.Font("Segoe UI", 8.8F);
        this._lblStatus.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28);
        this._lblStatus.Location = new System.Drawing.Point(36, 340);
        this._lblStatus.Name = "_lblStatus";
        this._lblStatus.Size = new System.Drawing.Size(348, 48);
        this._lblStatus.TabIndex = 7;
        this._lblStatus.Text = "";
        this._lblStatus.TextAlign = System.Drawing.ContentAlignment.TopCenter;
        // 
        // LoginForm
        // 
        this.AcceptButton = this._btnLogin;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(238, 242, 249);
        this.ClientSize = new System.Drawing.Size(580, 525);
        this.Controls.Add(this._cardPanel);
        this.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "LoginForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Homologador carga masiva DDC — Iniciar sesión";
        this._cardPanel.ResumeLayout(false);
        this._cardPanel.PerformLayout();
        this._pnlEmailBox.ResumeLayout(false);
        this._pnlEmailBox.PerformLayout();
        this._pnlPasswordBox.ResumeLayout(false);
        this._pnlPasswordBox.PerformLayout();
        this.ResumeLayout(false);
    }

    #endregion
}
