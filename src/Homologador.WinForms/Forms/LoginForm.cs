using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Homologador.WinForms.ApiClient;
using Homologador.WinForms.Theme;

namespace Homologador.WinForms.Forms;

public partial class LoginForm : Form
{
    private readonly HomologadorApiClient? _api;
    private bool _emailFocused;
    private bool _passwordFocused;

    private static readonly Color BrandBlue = Color.FromArgb(0, 46, 255);
    private static readonly Color BrandBlueHover = Color.FromArgb(0, 36, 204);
    private static readonly Color BorderNormal = Color.FromArgb(209, 213, 219);

    public LoginForm()
    {
        InitializeComponent();
        ApplyStyling();
    }

    public LoginForm(HomologadorApiClient api) : this()
    {
        _api = api;
    }

    private void ApplyStyling()
    {
        try
        {
            var icon = DdcTheme.LoadBrandIcon("ddc-isotipo.ico");
            if (icon != null) Icon = icon;
        }
        catch { }

        _btnLogin.MouseEnter += (_, _) => _btnLogin.BackColor = BrandBlueHover;
        _btnLogin.MouseLeave += (_, _) => _btnLogin.BackColor = BrandBlue;

        _txtEmail.Enter += (_, _) => { _emailFocused = true; _pnlEmailBox.Invalidate(); };
        _txtEmail.Leave += (_, _) => { _emailFocused = false; _pnlEmailBox.Invalidate(); };

        _txtPassword.Enter += (_, _) => { _passwordFocused = true; _pnlPasswordBox.Invalidate(); };
        _txtPassword.Leave += (_, _) => { _passwordFocused = false; _pnlPasswordBox.Invalidate(); };

        _pnlEmailBox.Paint += (_, e) => DrawInputBorder(e.Graphics, _pnlEmailBox.ClientRectangle, _emailFocused);
        _pnlPasswordBox.Paint += (_, e) => DrawInputBorder(e.Graphics, _pnlPasswordBox.ClientRectangle, _passwordFocused);
        _cardPanel.Paint += (_, e) => DrawCardBorder(e.Graphics, _cardPanel.ClientRectangle);

        Resize += (_, _) => CenterCard();
        CenterCard();
    }

    private void CenterCard()
    {
        _cardPanel.Location = new Point(
            Math.Max(16, (ClientSize.Width - _cardPanel.Width) / 2),
            Math.Max(16, (ClientSize.Height - _cardPanel.Height) / 2));
    }

    private static void DrawInputBorder(Graphics g, Rectangle bounds, bool focused)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var color = focused ? BrandBlue : BorderNormal;
        using var pen = new Pen(color, focused ? 1.6f : 1.0f);
        var rect = new Rectangle(1, 1, bounds.Width - 3, bounds.Height - 3);
        using var path = CreateRoundedRect(rect, 6);
        g.DrawPath(pen, path);
    }

    private static void DrawCardBorder(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
        var rect = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);
        using var path = CreateRoundedRect(rect, 10);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private async void OnBtnLoginClick(object? sender, EventArgs e)
    {
        await HandleLoginAsync();
    }

    private async Task HandleLoginAsync()
    {
        if (_api == null) return;

        var userText = _txtEmail.Text.Trim();
        var pwdText = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(userText) || string.IsNullOrWhiteSpace(pwdText))
        {
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = "Ingrese su usuario y contraseña.";
            return;
        }

        _btnLogin.Enabled = false;
        _lblStatus.ForeColor = BrandBlue;
        _lblStatus.Text = "Iniciando sesión...";

        try
        {
            await _api.LoginAsync(userText, pwdText);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = ex.Message;
        }
        finally
        {
            _btnLogin.Enabled = true;
        }
    }
}
