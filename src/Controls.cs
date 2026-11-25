using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace IceyTuner;

/// <summary>自绘按钮：主按钮为猩红渐变，次按钮为暗底描边，带悬停/按下反馈。</summary>
internal sealed class NeonButton : Control
{
    private bool _hover;
    private bool _down;

    public bool Primary { get; set; }

    public NeonButton(string text, bool primary)
    {
        Text = text;
        Primary = primary;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = primary ? Theme.BodyBold : Theme.Body;
        Cursor = Cursors.Hand;
        Size = new Size(190, 40);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(1, _down ? 2 : 1, Width - 3, Height - 3);

        if (Primary)
        {
            using var lg = new LinearGradientBrush(r, Theme.Crimson, Theme.CrimsonDeep, 90f);
            using var path = Theme.Round(r, 6);
            g.FillPath(lg, path);
            if (_hover)
            {
                using var glow = new SolidBrush(Color.FromArgb(48, 255, 255, 255));
                g.FillPath(glow, path);
            }
            using var p = new Pen(Color.FromArgb(_hover ? 220 : 140, 255, 120, 140), 1f);
            g.DrawPath(p, path);
        }
        else
        {
            Theme.FillRound(g, r, 6, _hover ? Color.FromArgb(120, 24, 24, 34) : Color.FromArgb(90, 16, 16, 22));
            Theme.StrokeRound(g, r, 6, _hover ? Color.FromArgb(190, 255, 60, 90) : Color.FromArgb(70, 220, 225, 240), 1f);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, Text, Font, r, Primary ? Color.White : Theme.Ink, flags);
    }
}

/// <summary>拨动开关：轨道 + 圆钮，开启为猩红。</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _on;
    private bool _hover;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _on;
        set { if (_on == value) return; _on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(56, 28);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new Rectangle(1, 4, Width - 3, Height - 9);
        var trackColor = _on ? Theme.Crimson : Color.FromArgb(255, 62, 62, 78);
        using (var path = Theme.Round(track, track.Height / 2))
        using (var b = new SolidBrush(trackColor))
            g.FillPath(b, path);

        using (var path = Theme.Round(track, track.Height / 2))
        using (var p = new Pen(_on ? Color.FromArgb(220, 255, 140, 160) : Color.FromArgb(110, 200, 205, 225), 1f))
            g.DrawPath(p, path);

        if (_hover)
            using (var path = Theme.Round(track, track.Height / 2))
            using (var p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
                g.DrawPath(p, path);

        int d = track.Height - 6;
        int cx = _on ? track.Right - d - 3 : track.Left + 3;
        using (var knob = new SolidBrush(_on ? Color.White : Color.FromArgb(220, 200, 204, 214)))
            g.FillEllipse(knob, cx, track.Top + 3, d, d);
    }
}

/// <summary>半透明圆角卡片，带标题与左侧猩红标记。</summary>
internal sealed class Card : Panel
{
    private string _title = "";

    public string Title
    {
        get => _title;
        set { _title = value; Invalidate(); }
    }

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Padding = new Padding(18, 40, 18, 16);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, 10, Theme.Card);
        Theme.StrokeRound(g, r, 10, Theme.CardBorder, 1f);

        using (var accent = new SolidBrush(Theme.Crimson))
            g.FillRectangle(accent, 18, 16, 3, 12);

        TextRenderer.DrawText(g, Title, Theme.CardTitle, new Point(28, 14), Theme.Ink,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        base.OnPaint(e);
    }
}

/// <summary>圆角输入框容器，内部放一个无边框 TextBox。</summary>
internal sealed class FieldBox : Panel
{
    public TextBox Box { get; } = new();
    private bool _focus;

    public FieldBox(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(170, 38);

        Box.Text = text;
        Box.BorderStyle = BorderStyle.None;
        Box.BackColor = Theme.Field;
        Box.ForeColor = Theme.Ink;
        Box.Font = Theme.Mono;
        Box.Location = new Point(12, 8);
        Box.Width = Width - 24;

        Box.Enter += (_, _) => { _focus = true; Invalidate(); };
        Box.Leave += (_, _) => { _focus = false; Invalidate(); };
        Box.TextChanged += (_, _) => Invalidate();
        Controls.Add(Box);

        Layout += (_, _) => CenterBox();
        CenterBox();
    }

    private void CenterBox()
    {
        Box.Left = 12;
        Box.Width = Width - 24;
        Box.Top = Math.Max(0, (Height - Box.Height) / 2);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterBox();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, 6, Theme.Field);
        Theme.StrokeRound(g, r, 6, _focus ? Theme.Crimson : Theme.FieldBorder, _focus ? 1.6f : 1f);
    }
}

/// <summary>修改记录：自绘条目，无时间戳，最新在最上方。</summary>
internal sealed class HistoryView : Control
{
    private readonly List<(string Text, Color Color)> _items = new();
    private int _top;

    private const int RowH = 26;
    private const int Bullet = 6;

    public HistoryView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Body;
    }

    public void Add(string text, Color color)
    {
        _items.Insert(0, (text, color));
        if (_items.Count > 300) _items.RemoveAt(_items.Count - 1);
        _top = 0;
        Invalidate();
    }

    public void ClearAll()
    {
        _items.Clear();
        _top = 0;
        Invalidate();
    }

    private int VisibleRows => Math.Max(1, Height / RowH);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_items.Count > VisibleRows)
        {
            _top = Math.Clamp(_top - Math.Sign(e.Delta), 0, _items.Count - VisibleRows);
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, "还没有任何修改记录", Theme.Small, new Point(2, 6), Theme.Dim,
                TextFormatFlags.NoPadding);
            return;
        }

        int y = 0;
        for (int i = _top; i < _items.Count && y < Height; i++, y += RowH)
        {
            var (txt, col) = _items[i];
            using (var b = new SolidBrush(col))
                g.FillRectangle(b, 2, y + RowH / 2 - Bullet / 2, Bullet, Bullet);

            var bounds = new Rectangle(18, y, Width - 24, RowH);
            TextRenderer.DrawText(g, txt, i == _top && _top == 0 ? Theme.Body : Theme.Small, bounds,
                i == _top && _top == 0 ? Theme.Ink : Color.FromArgb(210, 206, 212, 226),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        if (_items.Count > VisibleRows)
        {
            int trackH = Height - 8;
            int thumbH = Math.Max(24, trackH * VisibleRows / _items.Count);
            int thumbY = 4 + (trackH - thumbH) * _top / Math.Max(1, _items.Count - VisibleRows);
            Theme.FillRound(g, new Rectangle(Width - 6, thumbY, 4, thumbH), 2,
                Color.FromArgb(120, 255, 255, 255));
        }
    }
}
