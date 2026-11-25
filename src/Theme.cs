using System.Drawing.Drawing2D;
using System.Reflection;

namespace IceyTuner;

/// <summary>配色、字体与绘制辅助。整体风格：近黑底 + 猩红点缀 + 锐利直角。</summary>
internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(236, 238, 244);
    public static readonly Color Muted = Color.FromArgb(150, 152, 168);
    public static readonly Color Dim = Color.FromArgb(110, 112, 128);

    public static readonly Color Crimson = Color.FromArgb(255, 42, 76);
    public static readonly Color CrimsonDeep = Color.FromArgb(176, 14, 44);
    public static readonly Color Ok = Color.FromArgb(90, 220, 170);
    public static readonly Color Warn = Color.FromArgb(255, 176, 74);
    public static readonly Color Bad = Color.FromArgb(255, 96, 96);

    public static readonly Color Card = Color.FromArgb(226, 12, 12, 18);
    public static readonly Color CardBorder = Color.FromArgb(58, 255, 70, 100);
    public static readonly Color Field = Color.FromArgb(255, 20, 20, 28);
    public static readonly Color FieldBorder = Color.FromArgb(70, 255, 255, 255);

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

    public static Image? LoadBackground()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith("bg.png", StringComparison.OrdinalIgnoreCase)) continue;
            using var s = asm.GetManifestResourceStream(name);
            if (s == null) continue;
            using var tmp = Image.FromStream(s);
            return new Bitmap(tmp);
        }
        return null;
    }
}
