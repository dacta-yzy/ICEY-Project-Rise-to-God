using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json.Nodes;
using IceyTuner.Core;

namespace IceyTuner;

public sealed class MainForm : Form
{
    private readonly FieldBox _fDmg = new("10");
    private readonly FieldBox _fHp = new("2000");
    private readonly FieldBox _fCoin = new("99999");
    private readonly ToggleSwitch _tglCap = new();
    private readonly HistoryView _history = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private readonly Card _cardEnv = new() { Title = "环境检测" };
    private readonly Card _cardVal = new() { Title = "数值设置" };
    private readonly Card _cardLog = new() { Title = "修改记录" };

    private string? _gameDir;
    private string? _savePath;
    private DllPatcher? _dll;
    private SaveFile? _save;
    private Image? _bg;

    private string _statusText = "未运行";
    private Color _statusColor = Theme.Dim;
    private bool _permissionHintShown;
    private bool _initializing = true;

    private const int Pad = 28;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));

            // 标题栏染成深色（Win11 22H2+）
            int caption = 0x00170F0A;
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            int captionText = 0x00F0F0F0;
            DwmSetWindowAttribute(Handle, 36, ref captionText, sizeof(int));
        }
        catch { }
    }

    public MainForm()
    {
        Text = "ICEY Tuner — 伤害 / 生命 / 金币";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(900, 812);
        Font = Theme.Body;
        BackColor = Color.FromArgb(10, 10, 14);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        _bg = Backdrop.Render(ClientSize);

        BuildUi();
        Detect();

        _timer.Interval = 1500;
        _timer.Tick += (_, _) => UpdateStatus();
        _timer.Start();
    }

    // ------------------------------------------------------------------ 布局
    private void BuildUi()
    {
        int w = ClientSize.Width - Pad * 2;

        // 环境检测
        _cardEnv.SetBounds(Pad, 112, w, 88);
        _cardEnv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_cardEnv);

        _lblGame = MakeLabel("", Theme.Small, Theme.Muted);
        _lblGame.Location = new Point(18, 44);
        _cardEnv.Controls.Add(_lblGame);

        _lblSave = MakeLabel("", Theme.Small, Theme.Muted);
        _lblSave.Location = new Point(18, 64);
        _cardEnv.Controls.Add(_lblSave);

        // 数值设置
        _cardVal.SetBounds(Pad, 212, w, 232);
        _cardVal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_cardVal);

        AddRow(_cardVal, 0, "伤害倍率", _fDmg, "1 = 原版，建议 2 ~ 20");
        AddRow(_cardVal, 1, "生命上限", _fHp, "游戏原值 200");
        AddRow(_cardVal, 2, "金币数量", _fCoin, "商店消费用");

        var divider = new Panel
        {
            BackColor = Color.FromArgb(40, 255, 255, 255),
            Location = new Point(18, 180),
            Size = new Size(w - 36, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        _cardVal.Controls.Add(divider);

        var capLabel = MakeLabel("解除 999 伤害上限", Theme.Body, Theme.Ink);
        capLabel.Location = new Point(18, 198);
        _cardVal.Controls.Add(capLabel);

        _tglCap.Location = new Point(198, 192);
        _tglCap.CheckedChanged += (_, _) =>
        {
            if (_initializing) return;
            Note(_tglCap.Checked ? "伤害上限：解除（999 → 999999）" : "伤害上限：恢复 999", Theme.Warn);
        };
        _cardVal.Controls.Add(_tglCap);

        var capHint = MakeLabel("开启后单次伤害不再被截断", Theme.Small, Theme.Dim);
        capHint.Location = new Point(270, 198);
        _cardVal.Controls.Add(capHint);

        // 按钮
        _btnLaunch = new NeonButton("应用并启动游戏", true) { Location = new Point(Pad, 460), Size = new Size(200, 42) };
        _btnLaunch.Click += (_, _) => OnLaunch();

        _btnApply = new NeonButton("仅应用（下次生效）", false) { Location = new Point(Pad + 212, 460), Size = new Size(200, 42) };
        _btnApply.Click += (_, _) => OnApply();

        _btnInject = new NeonButton("立即内存注入", false) { Location = new Point(Pad + 424, 460), Size = new Size(170, 42) };
        _btnInject.Click += (_, _) => OnInject();

        _btnRestore = new NeonButton("还原备份", false) { Location = new Point(Pad + 606, 460), Size = new Size(140, 42) };
        _btnRestore.Click += (_, _) => OnRestore();

        Controls.AddRange(new Control[] { _btnLaunch, _btnApply, _btnInject, _btnRestore });

        // 修改记录
        _cardLog.SetBounds(Pad, 518, w, 266);
        _cardLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_cardLog);

        _history.Location = new Point(18, 44);
        _history.Size = new Size(w - 40, 206);
        _history.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _cardLog.Controls.Add(_history);
    }

    private static Label MakeLabel(string text, Font font, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
    };

    private void AddRow(Card card, int index, string label, FieldBox field, string hint)
    {
        int y = 44 + index * 46;

        card.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Font = Theme.Body,
            ForeColor = Theme.Ink,
            BackColor = Color.Transparent,
            Location = new Point(18, y + 8),
        });

        field.Location = new Point(118, y);
        field.Size = new Size(170, 38);
        card.Controls.Add(field);

        card.Controls.Add(new Label
        {
            Text = hint,
            AutoSize = true,
            Font = Theme.Small,
            ForeColor = Theme.Dim,
            BackColor = Color.Transparent,
            Location = new Point(306, y + 10),
        });
    }

    private Label _lblGame = new();
    private Label _lblSave = new();
    private NeonButton _btnLaunch = null!;
    private NeonButton _btnApply = null!;
    private NeonButton _btnInject = null!;
    private NeonButton _btnRestore = null!;

    // ------------------------------------------------------------------ 绘制
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (_bg != null)
            g.DrawImage(_bg, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
        else
            g.Clear(Color.FromArgb(12, 12, 18));

        using var shade = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
        g.FillRectangle(shade, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 标题
        TextRenderer.DrawText(g, "ICEY TUNER", Theme.Title, new Point(Pad - 2, 12), Theme.Ink,
            TextFormatFlags.NoPadding);
        using (var b = new SolidBrush(Theme.Accent))
            g.FillRectangle(b, Pad, 64, 54, 3);

        TextRenderer.DrawText(g, "伤害 · 生命上限 · 金币 · 伤害上限", Theme.Subtitle, new Point(Pad, 74),
            Theme.Muted, TextFormatFlags.NoPadding);

        // 状态
        var size = TextRenderer.MeasureText(_statusText, Theme.Body, new Size(400, 24), TextFormatFlags.NoPadding);
        int right = ClientSize.Width - Pad;
        TextRenderer.DrawText(g, _statusText, Theme.Body,
            new Rectangle(right - size.Width, 34, size.Width, 24), _statusColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using (var dot = new SolidBrush(_statusColor))
            g.FillEllipse(dot, right - size.Width - 16, 42, 8, 8);
    }

    // ------------------------------------------------------------------ 逻辑
    private void Note(string text, Color color) => _history.Add(text, color);

    private void Detect()
    {
        _gameDir = Paths.FindGameDir();
        if (_gameDir == null)
        {
            _lblGame.Text = "游戏目录：未找到，请确认 ICEY 已安装";
            _lblGame.ForeColor = Theme.Bad;
            _lblSave.Text = "存档文件：—";
            _lblSave.ForeColor = Theme.Bad;
            Note("未找到游戏目录", Theme.Bad);
            return;
        }

        _dll = new DllPatcher(_gameDir);
        _savePath = Paths.FindSaveFile(_gameDir);
        if (_savePath != null) _save = new SaveFile(_savePath);

        _lblGame.Text = "游戏目录： " + _gameDir;
        _lblGame.ForeColor = Theme.Ok;

        if (_savePath != null)
        {
            _lblSave.Text = "存档文件： " + _savePath;
            _lblSave.ForeColor = Theme.Ok;
        }
        else
        {
            _lblSave.Text = "存档文件：未找到，先运行一次游戏生成存档";
            _lblSave.ForeColor = Theme.Bad;
        }

        try
        {
            var m = _dll.ReadMultiplier();
            if (m.HasValue)
            {
                _fDmg.Box.Text = m.Value.ToString("0.###");
                Note($"读取当前伤害倍率 {m.Value:0.###}×", Theme.Muted);
            }

            var cap = _dll.ReadCap();
            if (cap.HasValue)
            {
                _tglCap.Checked = cap.Value > DllPatcher.CapDefault + 1f;
                Note(_tglCap.Checked ? "伤害上限：已解除" : "伤害上限：999（默认）", Theme.Muted);
            }
        }
        catch (Exception ex)
        {
            Note("读取 DLL 失败：" + ex.Message, Theme.Bad);
        }

        _initializing = false;
    }

    private void UpdateStatus()
    {
        int? pid = Native.FindPid();
        string text = pid.HasValue ? $"运行中 · PID {pid}" : "未运行";
        Color color = pid.HasValue ? Theme.Ok : Theme.Dim;
        if (text == _statusText && color == _statusColor) return;
        _statusText = text;
        _statusColor = color;
        Invalidate(new Rectangle(ClientSize.Width - 268, 26, 250, 40));
    }

    private void HintPermission(Exception ex)
    {
        if (ex is not UnauthorizedAccessException && ex is not IOException) return;
        if (_permissionHintShown) return;
        _permissionHintShown = true;
        BeginInvoke(new Action(() =>
            MessageBox.Show(
                "没有写入权限。\n\n可能是程序被以受限权限启动，或游戏/Steam 以管理员身份运行。\n\n" +
                "解决办法：\n1) 把 ICEY_Tuner.exe 复制到桌面等普通文件夹，直接双击运行；\n" +
                "2) 或右键 → 以管理员身份运行。",
                "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
    }

    private (float Dmg, int Hp, int Coin) ReadValues()
    {
        if (!float.TryParse(_fDmg.Box.Text, out var dmg) || dmg < 0.1f || dmg > 1000f)
            throw new ArgumentException("伤害倍率需为 0.1 ~ 1000 的数字");
        if (!int.TryParse(_fHp.Box.Text, out var hp) || hp < 1 || hp > 999999)
            throw new ArgumentException("生命上限需为 1 ~ 999999 的整数");
        if (!int.TryParse(_fCoin.Box.Text, out var coin) || coin < 0 || coin > 99999999)
            throw new ArgumentException("金币需为 0 ~ 99999999 的整数");
        return (dmg, hp, coin);
    }

    private bool ApplyFiles(float dmg, int hp, int coin)
    {
        bool ok = true;
        try
        {
            var real = _dll!.WriteMultiplier(dmg);
            Note($"伤害倍率 → {real:0.###}×", Theme.Ok);
        }
        catch (Exception ex)
        {
            ok = false;
            Note("伤害倍率写入失败：" + ex.Message, Theme.Bad);
            HintPermission(ex);
        }

        try
        {
            var cap = _dll!.WriteCap(_tglCap.Checked);
            Note(cap > DllPatcher.CapDefault + 1f ? "伤害上限 → 999999" : "伤害上限 → 999", Theme.Ok);
        }
        catch (Exception ex)
        {
            ok = false;
            Note("伤害上限写入失败：" + ex.Message, Theme.Bad);
        }

        if (_save == null)
        {
            Note("未找到存档，生命与金币未写入", Theme.Warn);
            return false;
        }

        try
        {
            _save.Write(maxHp: hp, coin: coin);
            Note($"生命上限 → {hp}，金币 → {coin}", Theme.Ok);
        }
        catch (Exception ex)
        {
            ok = false;
            Note("存档写入失败：" + ex.Message, Theme.Bad);
            HintPermission(ex);
        }
        return ok;
    }

    private void OnApply()
    {
        (float dmg, int hp, int coin) v;
        try { v = ReadValues(); }
        catch (ArgumentException ex) { MessageBox.Show(ex.Message, "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        Task.Run(() =>
        {
            bool ok = ApplyFiles(v.dmg, v.hp, v.coin);
            Note(ok ? "修改已写入，下次启动生效" : "部分修改失败，见上方记录", ok ? Theme.Ok : Theme.Warn);
        });
    }

    private void OnLaunch()
    {
        (float dmg, int hp, int coin) v;
        try { v = ReadValues(); }
        catch (ArgumentException ex) { MessageBox.Show(ex.Message, "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (_gameDir == null || !File.Exists(Path.Combine(_gameDir, Paths.GameExe)))
        {
            MessageBox.Show("找不到 ICEY.exe", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (Native.FindPid().HasValue)
        {
            var r = MessageBox.Show("ICEY 正在运行。要关闭它并重新启动以应用修改吗？\n（未保存的进度会丢失）",
                "游戏已在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            try
            {
                foreach (var p in Process.GetProcessesByName("ICEY")) p.Kill(true);
                Thread.Sleep(2000);
            }
            catch { }
        }

        Task.Run(() =>
        {
            ApplyFiles(v.dmg, v.hp, v.coin);

            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_gameDir!, Paths.GameExe))
                {
                    WorkingDirectory = _gameDir,
                    UseShellExecute = false,
                });
                Note("已启动 ICEY", Theme.Ok);
            }
            catch (Exception ex)
            {
                Note("启动失败：" + ex.Message, Theme.Bad);
                return;
            }

            WaitAndInject(v.hp, v.coin, v.dmg);
        });
    }

    private void WaitAndInject(int hp, int coin, float dmg)
    {
        Note("等待角色载入…", Theme.Muted);
        for (int i = 0; i < 120; i++)
        {
            Thread.Sleep(1000);
            var pid = Native.FindPid();
            if (!pid.HasValue) continue;
            try
            {
                using var mem = new MemoryEngine(pid.Value);
                var pa = mem.FindPlayerAttributes(hp);
                if (pa.Count > 0)
                {
                    WriteAll(mem, pa, hp, coin, dmg);
                    return;
                }
            }
            catch { }
        }
        Note("120 秒内未定位到角色对象，文件修改已生效", Theme.Warn);
    }

    private void OnInject()
    {
        var pid = Native.FindPid();
        if (!pid.HasValue)
        {
            MessageBox.Show("请先启动游戏并进入游戏画面。", "未运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        (float dmg, int hp, int coin) v;
        try { v = ReadValues(); }
        catch (ArgumentException ex) { MessageBox.Show(ex.Message, "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        Task.Run(() =>
        {
            try
            {
                try { _dll?.WriteCap(_tglCap.Checked); } catch { }

                using var mem = new MemoryEngine(pid.Value);
                Note("正在扫描内存…", Theme.Muted);

                int saveHp = v.hp, saveCoin = v.coin;
                try
                {
                    if (_save != null)
                    {
                        var (shp, _, scoin, _) = _save.ReadValues();
                        if (shp > 0) saveHp = shp;
                        saveCoin = scoin;
                    }
                }
                catch { }

                var pa = mem.FindPlayerAttributes(saveHp);
                if (pa.Count == 0) pa = mem.FindPlayerAttributes(v.hp);

                var eq = mem.FindEquipments(v.coin);
                if (eq.Count == 0) eq = mem.FindEquipments(saveCoin);

                if (pa.Count == 0 && eq.Count == 0)
                {
                    Note("未找到角色对象，请确认已进入游戏", Theme.Warn);
                    return;
                }

                WriteAll(mem, pa, v.hp, v.coin, v.dmg, eq);
            }
            catch (Exception ex)
            {
                Note("注入异常：" + ex.Message, Theme.Bad);
            }
        });
    }

    private void WriteAll(MemoryEngine mem, List<PlayerAttr> pa, int hp, int coin, float dmg,
                          List<EquipmentObj>? eq = null)
    {
        int baseAtk = Math.Clamp((int)Math.Round(40 * dmg), 1, 99999);

        if (pa.Count > 0)
        {
            long obj = pa[0].Obj;
            mem.WriteInt(obj + MemoryEngine.PaMaxHp, hp);
            mem.WriteInt(obj + MemoryEngine.PaCurHp, hp);
            mem.WriteInt(obj + MemoryEngine.PaBaseAtk, baseAtk);
            Note($"内存注入：生命 {hp}，攻击 {baseAtk}", Theme.Ok);
        }

        if (eq is { Count: > 0 })
        {
            mem.WriteInt(eq[0].Obj + MemoryEngine.EqCoin, coin);
            Note($"内存注入：金币 {coin}", Theme.Ok);
        }
        else if (_save != null)
        {
            try
            {
                _save.Write(coin: coin);
                Note($"金币已写入存档：{coin}", Theme.Ok);
            }
            catch (Exception ex)
            {
                Note("金币写入失败：" + ex.Message, Theme.Bad);
            }
        }
    }

    private void OnRestore()
    {
        if (MessageBox.Show("将把 DLL 和存档恢复到首次修改前的备份，继续？", "还原",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        int n = 0;
        if (_dll != null && _dll.Restore()) { n++; Note("DLL 已还原", Theme.Ok); }
        if (_save != null && _save.Restore()) { n++; Note("存档已还原", Theme.Ok); }
        if (n == 0) Note("没有找到备份文件", Theme.Warn);
    }
}
