using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Homologador.WinForms.Theme;

/// <summary>
/// Tokens oficiales del Manual de Marca DDC (Págs. 10–13) y utilidades de renderizado nativo para Windows Forms.
/// No introduce controles externos, garantizando 100% de compatibilidad con el Diseñador de Visual Studio.
/// </summary>
public static class DdcTheme
{
    // Paleta Principal (Pág. 10)
    public static readonly Color BluePrimary = Color.FromArgb(0, 61, 165);    // Pantone 293 C
    public static readonly Color GrayBorder = Color.FromArgb(217, 217, 214);   // Cool Gray 1 C

    // Paleta Secundaria (Pág. 11)
    public static readonly Color Navy = Color.FromArgb(44, 45, 101);          // Pantone 2119 C
    public static readonly Color Cyan = Color.FromArgb(65, 182, 230);         // Pantone 298 C
    public static readonly Color Teal = Color.FromArgb(0, 199, 177);          // Pantone 3265 C
    public static readonly Color TealDark = Color.FromArgb(0, 105, 95);       // Verde oscuro accesible para estados OK
    public static readonly Color Coral = Color.FromArgb(255, 157, 110);       // Pantone 163 C (RGB 255/157/110)
    public static readonly Color CoralDark = Color.FromArgb(181, 71, 25);     // Naranja oscuro accesible

    // Tokens Semánticos de Superficie y Texto
    public static readonly Color SurfaceBg = Color.FromArgb(244, 246, 249);
    public static readonly Color SurfaceCard = Color.White;
    public static readonly Color SurfaceSubtle = Color.FromArgb(248, 250, 252);
    public static readonly Color TextMain = Color.FromArgb(30, 41, 59);
    public static readonly Color TextMuted = Color.FromArgb(90, 101, 119);
    public static readonly Color BlueTint = Color.FromArgb(235, 243, 252);
    public static readonly Color NavyTint = Color.FromArgb(236, 238, 245);

    private static readonly string PreferredFontName = ResolveFontFamily();

    private static string ResolveFontFamily()
    {
        try
        {
            var installed = FontFamily.Families.Any(f => string.Equals(f.Name, "Titillium Web", StringComparison.OrdinalIgnoreCase));
            return installed ? "Titillium Web" : "Arial";
        }
        catch
        {
            return "Arial";
        }
    }

    /// <summary>
    /// Resuelve Titillium Web si está instalada en el sistema, o Arial como tipografía auxiliar oficial (Pág. 13).
    /// </summary>
    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            return new Font(PreferredFontName, size, style, GraphicsUnit.Point);
        }
        catch
        {
            return new Font("Arial", size, style, GraphicsUnit.Point);
        }
    }

    public static string GetBrandAssetPath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        var direct = Path.Combine(baseDir, "Brand", filename);
        if (File.Exists(direct)) return direct;

        var local = Path.Combine(baseDir, filename);
        if (File.Exists(local)) return local;

        // Fallback al código fuente para modo desarrollo en Visual Studio
        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Brand", filename));
        if (File.Exists(devPath)) return devPath;

        return direct;
    }

    public static Image? LoadBrandImage(string filename)
    {
        try
        {
            var p = GetBrandAssetPath(filename);
            return File.Exists(p) ? Image.FromFile(p) : null;
        }
        catch
        {
            return null;
        }
    }

    public static Icon? LoadBrandIcon(string filename)
    {
        try
        {
            var p = GetBrandAssetPath(filename);
            return File.Exists(p) ? new Icon(p) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Estiliza grillas DataGridView con la paleta oficial DDC (encabezados azules, filas alternas y bordes neutros).
    /// </summary>
    public static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = SurfaceCard;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = GrayBorder;

        grid.ColumnHeadersDefaultCellStyle.BackColor = BluePrimary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = Font(9.5f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersHeight = 34;

        grid.DefaultCellStyle.BackColor = SurfaceCard;
        grid.DefaultCellStyle.ForeColor = TextMain;
        grid.DefaultCellStyle.SelectionBackColor = BlueTint;
        grid.DefaultCellStyle.SelectionForeColor = BluePrimary;
        grid.DefaultCellStyle.Font = Font(9.0f, FontStyle.Regular);

        grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceSubtle;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = TextMain;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = BlueTint;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = BluePrimary;

        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
    }

    /// <summary>
    /// Pinta el degradé corporativo oficial de DDC (#41B6E6 -> #003DA5 -> #2C2D65) con curvas sutiles del isotipo.
    /// </summary>
    public static void DrawCorporateBackground(Graphics g, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var brush = new LinearGradientBrush(bounds, Cyan, Navy, 135f);
        var blend = new ColorBlend(3)
        {
            Colors = [Cyan, BluePrimary, Navy],
            Positions = [0.0f, 0.48f, 1.0f]
        };
        brush.InterpolationColors = blend;
        g.FillRectangle(brush, bounds);

        // Curvas sutiles inspiradas en el isotipo de la manzana (Pág. 14 del Brand Book)
        using var penCyan = new Pen(Color.FromArgb(40, 255, 255, 255), 2f);
        using var penWhite = new Pen(Color.FromArgb(28, 65, 182, 230), 3f);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.DrawArc(penCyan, bounds.Width - 280, -60, 400, 400, 90, 160);
        g.DrawArc(penWhite, bounds.Width - 340, -100, 520, 520, 85, 170);
        g.DrawArc(penCyan, -120, bounds.Height - 300, 380, 380, 270, 150);
    }

    /// <summary>
    /// Pinta el degradé del header superior DDC (#003DA5 -> #2C2D65) con línea inferior Cyan de 2px.
    /// </summary>
    public static void DrawHeaderBanner(Graphics g, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var brush = new LinearGradientBrush(bounds, BluePrimary, Navy, 0f);
        g.FillRectangle(brush, bounds);

        using var linePen = new Pen(Cyan, 2f);
        g.DrawLine(linePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    /// <summary>
    /// Pinta la franja superior de acento (4px) en tarjetas o paneles.
    /// </summary>
    public static void DrawCardAccent(Graphics g, Rectangle bounds, Color accentColor, int height = 4)
    {
        using var brush = new SolidBrush(accentColor);
        g.FillRectangle(brush, 0, 0, bounds.Width, height);

        using var borderPen = new Pen(GrayBorder, 1f);
        g.DrawRectangle(borderPen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }
}
