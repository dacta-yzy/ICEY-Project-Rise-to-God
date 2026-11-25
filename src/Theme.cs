using System.Drawing.Drawing2D;
using System.Reflection;

namespace IceyTuner;

/// <summary>配色、字体与绘制辅助。配色取自参考图：青黑底 + 亮青强调 + 银白文字。</summary>
internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(232, 238, 242);
    public static readonly Color Muted = Color.FromArgb(150, 175, 182);
    public static readonly Color Dim = Color.FromArgb(110, 135, 142);

    public static readonly Color Accent = Color.FromArgb(47, 230, 238);
    public static readonly Color AccentDeep = Color.FromArgb(10, 93, 106);
    public static readonly Color Ok = Color.FromArgb(80, 226, 196);
    public static readonly Color Warn = Color.FromArgb(255, 192, 90);
    public static readonly Color Bad = Color.FromArgb(255, 110, 120);

    public static readonly Color Card = Color.FromArgb(178, 10, 20, 23);
    public static readonly Color CardBorder = Color.FromArgb(58, 80, 220, 235);
    public static readonly Color Field = Color.FromArgb(255, 17, 31, 35);
    public static readonly Color FieldBorder = Color.FromArgb(70, 210, 240, 245);

    public static readonly Font Title = new("Bahnschrift", 26f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Subtitle = new("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Body = new("Microsoft YaHei UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BodyBold = new("Microsoft YaHei UI", 9.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Small = new("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Mono = new("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font CardTitle = new("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(color);
        g.FillPath(b, path);
    }

    public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
    {
        using var path = Round(r, radius);
        using var p = new Pen(color, width);
        g.DrawPath(p, path);
    }
}