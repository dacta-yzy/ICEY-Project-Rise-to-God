using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace IceyTuner;

/// <summary>
/// 运行时绘制的背景（不依赖任何图片文件）。
/// 取参考图的氛围：青黑底、左侧斜切光带、右侧巨幅「攻」字水印、HUD 刻度、扫描线。
/// </summary>
internal static class Backdrop
{
    private static readonly Color Deep = Color.FromArgb(7, 12, 13);
    private static readonly Color Mid = Color.FromArgb(18, 38, 42);
    private static readonly Color Cyan = Color.FromArgb(47, 230, 238);
    private static readonly Color Magenta = Color.FromArgb(176, 58, 106);

    public static Bitmap Render(Size size)
    {
        var bmp = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        int w = bmp.Width, h = bmp.Height;

        // 底色渐变
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Deep, Mid, 90f))
            g.FillRectangle(lg, 0, 0, w, h);

        // 右侧青绿光团
        DrawGlow(g, new PointF(w * 0.78f, h * 0.42f), w * 0.55f, Color.FromArgb(46, 26, 132, 146));

        // 斜切光带（参考图左下那道亮青）
        using (var beam = new GraphicsPath())
        {
            beam.AddPolygon(new[]
            {
                new PointF(-w * 0.10f, h * 0.94f),
                new PointF(w * 0.30f, h * 0.62f),
                new PointF(w * 0.34f, h * 0.665f),
                new PointF(-w * 0.06f, h * 0.985f),
            });
            using var bg = new LinearGradientBrush(
                new RectangleF(0, h * 0.6f, w * 0.4f, h * 0.45f),
                Color.FromArgb(120, 90, 240, 250), Color.FromArgb(0, 90, 240, 250), 45f);
            g.FillPath(bg, beam);
        }

        // 细网格
        using (var grid = new Pen(Color.FromArgb(11, 140, 220, 230), 1f))
        {
            for (int x = 0; x < w; x += 44) g.DrawLine(grid, x, 0, x, h);
            for (int y = 0; y < h; y += 44) g.DrawLine(grid, 0, y, w, y);
        }

        // 右侧科技字标水印（几何笔画 + 荧光）
        DrawEmblem(g, new RectangleF(w * 0.50f, h * 0.655f, w * 0.42f, w * 0.24f), 165);
        // ICEY 字样
        DrawWordmark(g, "ICEY", new RectangleF(-w * 0.03f, h * 0.62f, w * 0.55f, h * 0.42f), Color.FromArgb(13, 120, 220, 232));

        // HUD 刻度
        using (var tick = new Pen(Color.FromArgb(70, 120, 220, 232), 1f))
        {
            for (int x = 24; x < w - 24; x += 22)
            {
                int len = (x / 22) % 5 == 0 ? 10 : 5;
                g.DrawLine(tick, x, h - 8, x, h - 8 - len);
            }
            for (int y = 24; y < h - 24; y += 22)
            {
                int len = (y / 22) % 5 == 0 ? 10 : 5;
                g.DrawLine(tick, 8, y, 8 + len, y);
            }
        }

        // 四角框
        using (var corner = new Pen(Color.FromArgb(120, 70, 220, 235), 2f))
        {
            int L = 26;
            g.DrawLines(corner, new[] { new PointF(14, 14 + L), new PointF(14, 14), new PointF(14 + L, 14) });
            g.DrawLines(corner, new[] { new PointF(w - 14 - L, 14), new PointF(w - 14, 14), new PointF(w - 14, 14 + L) });
            g.DrawLines(corner, new[] { new PointF(14, h - 14 - L), new PointF(14, h - 14), new PointF(14 + L, h - 14) });
            g.DrawLines(corner, new[] { new PointF(w - 14 - L, h - 14), new PointF(w - 14, h - 14), new PointF(w - 14, h - 14 - L) });
        }

        // 小字标注
        using (var mono = new Font("Consolas", 7.5f))
        using (var dim = new SolidBrush(Color.FromArgb(70, 150, 220, 230)))
        {
            g.DrawString("SYS//ICEY-TUNER", mono, dim, 20, 22);
            g.DrawString("SECTOR 07", mono, dim, 20, 34);
            g.DrawString("LINK: STABLE", mono, dim, w - 130, h - 40);
            g.DrawString("v1.3", mono, dim, w - 46, 22);
        }

        // 品红细线（参考图左侧那抹红）
        using (var mg = new Pen(Color.FromArgb(90, Magenta), 2f))
            g.DrawLine(mg, 0, h * 0.21f, w * 0.12f, h * 0.21f);

        // 标题栏下方的分隔线 + 小刻度
        using (var sep = new Pen(Color.FromArgb(50, 90, 220, 235), 1f))
        {
            float y = 100;
            g.DrawLine(sep, 28, y, w - 28, y);
            for (int x = 28; x < w - 28; x += 60)
                g.DrawLine(sep, x, y, x, y + 4);
        }

        // 扫描线
        using (var scan = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
            for (int y = 0; y < h; y += 3) g.FillRectangle(scan, 0, y, w, 1);

        // 暗角
        using (var vig = new GraphicsPath())
        {
            vig.AddEllipse(-w * 0.35f, -h * 0.45f, w * 1.7f, h * 1.9f);
            using var pgb = new PathGradientBrush(vig)
            {
                CenterColor = Color.FromArgb(0, 0, 0, 0),
                SurroundColors = new[] { Color.FromArgb(165, 0, 0, 0) },
            };
            g.FillRectangle(pgb, 0, 0, w, h);
        }

        return bmp;
    }

    private static void DrawGlow(Graphics g, PointF c, float radius, Color color)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(c.X - radius, c.Y - radius * 0.8f, radius * 2, radius * 1.6f);
        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = color,
            SurroundColors = new[] { Color.FromArgb(0, color.R, color.G, color.B) },
        };
        g.FillPath(pgb, path);
    }

    /// <summary>
    /// “ICEY” 艺术字标：用粗笔画拼出 I / C / E / Y 并加两端下划线，
    /// 白色笔芯 + 三层青色荧光。坐标在「字高 = 1」的归一化空间里。
    /// </summary>
    private static readonly (float X, float Y)[][] WordStrokes =
    {
        // _ 左
        new[] { (-0.42f, 0.94f), (-0.08f, 0.94f) },
        // I
        new[] { (0.10f, 0.00f), (0.10f, 1.00f) },
        // C
        new[] { (1.10f, 0.00f), (0.50f, 0.00f), (0.50f, 1.00f), (1.10f, 1.00f) },
        // E
        new[] { (1.34f, 0.00f), (1.34f, 1.00f) },
        new[] { (1.34f, 0.00f), (1.90f, 0.00f) },
        new[] { (1.34f, 0.50f), (1.78f, 0.50f) },
        new[] { (1.34f, 1.00f), (1.90f, 1.00f) },
        // Y
        new[] { (2.08f, 0.00f), (2.42f, 0.46f) },
        new[] { (2.76f, 0.00f), (2.42f, 0.46f) },
        new[] { (2.42f, 0.46f), (2.42f, 1.00f) },
        // _ 右
        new[] { (2.94f, 0.94f), (3.28f, 0.94f) },
    };

    private const float WordMinX = -0.42f;
    private const float WordMaxX = 3.28f;

    private static readonly Lazy<Image?> Glyph = new(Theme.LoadGlyph);

    private static void DrawEmblem(Graphics g, RectangleF box, int alpha)
    {
        var img = Glyph.Value;
        if (img == null) return;

        // 等比缩放到 box 宽度，垂直居中
        float scale = box.Width / img.Width;
        float w = box.Width;
        float h = img.Height * scale;
        var dest = new RectangleF(box.X, box.Y + (box.Height - h) / 2f, w, h);

        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha / 255f });

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(img, Rectangle.Round(dest), 0, 0, img.Width, img.Height,
            GraphicsUnit.Pixel, ia);
    }

    private static void DrawWordmark(Graphics g, string text, RectangleF box, Color color)
    {
        Font font;
        try { font = new Font("Bahnschrift", 170f, FontStyle.Bold, GraphicsUnit.Pixel); }
        catch { font = new Font("Arial", 170f, FontStyle.Bold, GraphicsUnit.Pixel); }

        using (font)
        using (var b = new SolidBrush(color))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            g.DrawString(text, font, b, box, fmt);
    }
}
