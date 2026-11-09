using System.Diagnostics;
using System.Text.Json.Nodes;
using IceyTuner.Core;

namespace IceyTuner;

public sealed class MainForm : Form
{
    private readonly TextBox _txtDmg = new();
    private readonly TextBox _txtHp = new();
    private readonly TextBox _txtCoin = new();
    private readonly RichTextBox _log = new();
    private readonly Label _lblGame = new();
    private readonly Label _lblSave = new();
    private readonly Label _lblProc = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private string? _gameDir;
    private string? _savePath;
    private DllPatcher? _dll;
    private SaveFile? _save;

    private const int Pad = 18;

    public MainForm()
    {
        Text = "ICEY Tuner — 伤害 / 生命 / 金币 一键修改";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(830, 700);
        MinimumSize = new Size(800, 640);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(250, 250, 252);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        Detect();

        _timer.Interval = 1500;
        _timer.Tick += (_, _) => UpdateProcLabel();
        _timer.Start();
    }

    private void BuildUi()
    {
        int contentWidth = ClientSize.Width - Pad * 2;

        var title = new Label
        {
            Text = "ICEY Tuner",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(Pad - 2, 12),
        };
        var sub = new Label
        {
            Text = "伤害 · 生命上限 · 金币",
            ForeColor = Color.FromArgb(135, 135, 145),
            AutoSize = true,
            Location = new Point(Pad + 214, 36),
        };
        _lblProc.Text = "● 未运行";
        _lblProc.ForeColor = Color.Gray;
        _lblProc.AutoSize = true;
        _lblProc.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblProc.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        _lblProc.Location = new Point(ClientSize.Width - 120, 30);

        Controls.AddRange(new Control[] { title, sub, _lblProc });

        // ---- 环境检测 ----
        var gbEnv = new GroupBox
        {
            Text = " 环境检测 ",
            Location = new Point(Pad, 64),
            Size = new Size(contentWidth, 92),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        _lblGame.Text = "游戏目录：检测中…";
        _lblGame.AutoSize = true;
        _lblGame.Location = new Point(16, 30);
        _lblSave.Text = "存档文件：检测中…";
        _lblSave.AutoSize = true;
        _lblSave.Location = new Point(16, 58);
        gbEnv.Controls.AddRange(new Control[] { _lblGame, _lblSave });
        Controls.Add(gbEnv);

        // ---- 数值设置 ----
        var gbVal = new GroupBox
        {
            Text = " 数值设置 ",
            Location = new Point(Pad, 166),
            Size = new Size(contentWidth, 174),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        AddRow(gbVal, 0, "伤害倍率", _txtDmg, "1 = 原始，建议 2~20", "10");
        AddRow(gbVal, 1, "生命上限", _txtHp, "游戏原值 200", "2000");
        AddRow(gbVal, 2, "金币数量", _txtCoin, "商店消费用", "99999");
        Controls.Add(gbVal);

        // ---- 按钮 ----
        var flow = new FlowLayoutPanel
        {
            Location = new Point(Pad, 348),
            Size = new Size(contentWidth, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            WrapContents = false,
        };
        flow.Controls.Add(MakeButton("应用并启动游戏", true, (_, _) => OnLaunch()));
        flow.Controls.Add(MakeButton("仅应用（下次启动生效）", false, (_, _) => OnApply()));
        flow.Controls.Add(MakeButton("立即内存注入", false, (_, _) => OnInject()));
        flow.Controls.Add(MakeButton("还原备份", false, (_, _) => OnRestore()));
        Controls.Add(flow);

        // ---- 日志 ----
        var gbLog = new GroupBox
        {
            Text = " 日志 ",
            Location = new Point(Pad, 400),
            Size = new Size(contentWidth, ClientSize.Height - 400 - Pad),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = RichTextBoxScrollBars.Vertical;
        _log.BackColor = Color.FromArgb(252, 252, 253);
        _log.Font = new Font("Consolas", 9F);
        _log.BorderStyle = BorderStyle.None;
        _log.Dock = DockStyle.Fill;
        gbLog.Controls.Add(_log);
        Controls.Add(gbLog);
    }

    private static Button MakeButton(string text, bool primary, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 6, 14, 6),
            Margin = new Padding(0, 0, 10, 0),
            FlatStyle = FlatStyle.System,
            Font = new Font("Microsoft YaHei UI", 9F, primary ? FontStyle.Bold : FontStyle.Regular),
        };
        b.Click += onClick;
        return b;
    }

    private static void AddRow(Control parent, int row, string label, TextBox box,
                               string hint, string def)
    {
        int y = 32 + row * 44;
        parent.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Location = new Point(18, y + 5),
        });

        box.Text = def;
        box.Location = new Point(110, y);
        box.Size = new Size(150, 28);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Consolas", 11F);
        box.TextAlign = HorizontalAlignment.Left;
        parent.Controls.Add(box);

        parent.Controls.Add(new Label
        {
            Text = hint,
            AutoSize = true,
            ForeColor = Color.FromArgb(145, 145, 155),
            Location = new Point(280, y + 5),
        });
    }

    private void Detect()
    {
        _gameDir = Paths.FindGameDir();
        if (_gameDir != null)
        {
            _dll = new DllPatcher(_gameDir);
            _savePath = Paths.FindSaveFile(_gameDir);
            if (_savePath != null) _save = new SaveFile(_savePath);
        }

        if (_gameDir == null)
        {
            _lblGame.Text = "游戏目录：未找到（请确认 ICEY 已安装）";
            _lblGame.ForeColor = Color.Firebrick;
            _lblSave.Text = "存档文件：—";
            _lblSave.ForeColor = Color.Firebrick;
            Log("未找到游戏目录，请确认 Steam 库位置。");
            return;
        }

        _lblGame.Text = "游戏目录：" + _gameDir;
        _lblGame.ForeColor = Color.SeaGreen;

        if (_savePath != null)
        {
            _lblSave.Text = "存档文件：" + _savePath;
            _lblSave.ForeColor = Color.SeaGreen;
        }
        else
        {
            _lblSave.Text = "存档文件：未找到（先运行一次游戏生成存档）";
            _lblSave.ForeColor = Color.Firebrick;
        }

        try
        {
            var m = _dll?.ReadMultiplier();
            if (m.HasValue)
            {
                _txtDmg.Text = m.Value.ToString("0.###");
                Log($"当前 DLL 伤害倍率 = {m.Value:0.###}×");
            }
        }
        catch (Exception ex)
        {
            Log("读取 DLL 失败：" + ex.Message);
        }
    }

    private void UpdateProcLabel()
    {
        int? pid = Native.FindPid();
        if (pid.HasValue)
        {
            _lblProc.Text = $"● 运行中 (PID {pid})";
            _lblProc.ForeColor = Color.SeaGreen;
        }
        else
        {
            _lblProc.Text = "● 未运行";
            _lblProc.ForeColor = Color.Gray;
        }
    }

    private void Log(string msg)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(msg)));
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private (float Dmg, int Hp, int Coin) ReadValues()
    {
        if (!float.TryParse(_txtDmg.Text, out var dmg) || dmg < 0.1f || dmg > 1000f)
            throw new ArgumentException("伤害倍率需为 0.1 ~ 1000 的数字");
        if (!int.TryParse(_txtHp.Text, out var hp) || hp < 1 || hp > 999999)
            throw new ArgumentException("生命上限需为 1 ~ 999999 的整数");
        if (!int.TryParse(_txtCoin.Text, out var coin) || coin < 0 || coin > 99999999)
            throw new ArgumentException("金币需为 0 ~ 99999999 的整数");
        return (dmg, hp, coin);
    }

    private bool _permissionHintShown;

    /// <summary>被沙箱 / 受限令牌启动时，给一次明确提示。</summary>
    private void HintPermission(Exception ex)
    {
        if (ex is not UnauthorizedAccessException && ex is not IOException) return;
        if (_permissionHintShown) return;
        _permissionHintShown = true;
        BeginInvoke(new Action(() =>
            MessageBox.Show(
                "没有写入权限。\n\n" +
                "可能是程序被以受限权限启动（例如从聊天窗口的文件卡片直接打开），\n" +
                "或游戏/Steam 以管理员身份运行。\n\n" +
                "解决办法：\n" +
                "1) 把 ICEY_Tuner.exe 复制到桌面等普通文件夹，直接双击运行；\n" +
                "2) 或右键 → 以管理员身份运行；\n" +
                "3) 不要通过聊天窗口的文件卡片启动本程序。",
                "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
    }

    private bool ApplyFiles(float dmg, int hp, int coin)
    {
        bool ok = true;
        try
        {
            var real = _dll!.WriteMultiplier(dmg);
            Log($"✔ 伤害倍率写入 {real:0.###}× → {_dll.DllPath}");
        }
        catch (Exception ex)
        {
            ok = false;
            Log("✘ 伤害写入失败：" + ex.Message);
            HintPermission(ex);
        }

        if (_save == null)
        {
            Log("! 未找到存档，生命/金币未写入");
            return false;
        }

        try
        {
            _save.Write(maxHp: hp, coin: coin);
            Log($"✔ 存档写入 maxHP={hp}, CoinNum={coin} → {Path.GetFileName(_save.FilePath)}");
        }
        catch (Exception ex)
        {
            ok = false;
            Log("✘ 存档写入失败：" + ex.Message);
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
            Log(ok ? "完成。" : "部分失败，见上。");
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

        var pid = Native.FindPid();
        if (pid.HasValue)
        {
            var r = MessageBox.Show("ICEY 正在运行。要关闭它并重新启动以应用修改吗？\n（未保存的进度会丢失）",
                "游戏已在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            try
            {
                foreach (var p in Process.GetProcessesByName("ICEY")) p.Kill(true);
                Thread.Sleep(2000);
            }
            catch { /* ignore */ }
        }

        Task.Run(() =>
        {
            ApplyFiles(v.dmg, v.hp, v.coin);

            try
            {
                var psi = new ProcessStartInfo(Path.Combine(_gameDir, Paths.GameExe))
                {
                    WorkingDirectory = _gameDir,
                    UseShellExecute = false,
                };
                Process.Start(psi);
                Log("▶ 已启动 ICEY.exe");
            }
            catch (Exception ex)
            {
                Log("✘ 启动失败：" + ex.Message);
                return;
            }

            WaitAndInject(v.hp, v.coin);
        });
    }

    private void WaitAndInject(int hp, int coin)
    {
        Log("等待游戏进入可注入状态…");
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
                    Log("✔ 已定位 PlayerAttribute，开始内存注入");
                    WriteAll(mem, pa, hp, coin);
                    return;
                }
            }
            catch { /* 进程可能还没准备好 */ }
        }
        Log("（120 秒内未定位到角色对象；文件修改已生效，可在游戏内点『立即内存注入』）");
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
                using var mem = new MemoryEngine(pid.Value);
                Log("扫描内存中（约 5~20 秒）…");

                int saveHp = v.hp;
                int saveCoin = v.coin;
                try
                {
                    if (_save != null)
                    {
                        var (shp, _, scoin, _) = _save.ReadValues();
                        if (shp > 0) saveHp = shp;
                        saveCoin = scoin;
                    }
                }
                catch { /* ignore */ }

                var pa = mem.FindPlayerAttributes(saveHp);
                if (pa.Count == 0) pa = mem.FindPlayerAttributes(v.hp);

                var eq = mem.FindEquipments(v.coin);
                if (eq.Count == 0) eq = mem.FindEquipments(saveCoin);

                Log($"PlayerAttribute 命中 {pa.Count} 个，Equipment 命中 {eq.Count} 个");

                if (pa.Count == 0 && eq.Count == 0)
                {
                    Log("未找到角色对象：请确认已进入游戏（不是主菜单），或先让游戏自动存一次档再试。");
                    return;
                }

                WriteAll(mem, pa, v.hp, v.coin, eq, v.dmg);
            }
            catch (Exception ex)
            {
                Log("✘ 注入异常：" + ex.Message);
            }
        });
    }

    private void WriteAll(MemoryEngine mem, List<PlayerAttr> pa, int hp, int coin,
                          List<EquipmentObj>? eq = null, float dmg = 0)
    {
        if (dmg <= 0)
            float.TryParse(_txtDmg.Text, out dmg);
        int baseAtk = Math.Clamp((int)Math.Round(40 * dmg), 1, 99999);

        if (pa.Count > 0)
        {
            long obj = pa[0].Obj;
            mem.WriteInt(obj + MemoryEngine.PaMaxHp, hp);
            mem.WriteInt(obj + MemoryEngine.PaCurHp, hp);
            mem.WriteInt(obj + MemoryEngine.PaBaseAtk, baseAtk);
            Log($"✔ 内存写入 PlayerAttribute@0x{obj:X}: maxHP={hp}, currentHP={hp}, baseAtk={baseAtk}");
        }

        if (eq is { Count: > 0 })
        {
            long obj = eq[0].Obj;
            mem.WriteInt(obj + MemoryEngine.EqCoin, coin);
            Log($"✔ 内存写入 Equipment@0x{obj:X}: CoinNum={coin}");
        }
        else if (_save != null)
        {
            try
            {
                _save.Write(coin: coin);
                Log($"✔ 未定位到 Equipment，已改存档：CoinNum={coin}（下次存档生效）");
            }
            catch (Exception ex)
            {
                Log("✘ 金币写入失败：" + ex.Message);
            }
        }
    }

    private void OnRestore()
    {
        if (MessageBox.Show("将把 DLL 和存档恢复到首次修改前的备份，继续？", "还原",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        int n = 0;
        if (_dll != null && _dll.Restore()) { n++; Log("✔ DLL 已还原"); }
        if (_save != null && _save.Restore()) { n++; Log("✔ 存档已还原"); }
        if (n == 0) Log("没有找到备份文件");
    }
}
