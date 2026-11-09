using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IceyTuner.Core;

internal readonly record struct PlayerAttr(long Obj, int MaxHp, int CurHp, float Speed, int BaseAtk);
internal readonly record struct EquipmentObj(long Obj, int Coin, byte HasKey);

/// <summary>
/// 附加到 ICEY.exe，按「值扫描 + 结构校验」定位 Mono 对象。
/// 校验不通过绝不写入。
/// </summary>
internal sealed class MemoryEngine : IDisposable
{
    // PlayerAttribute (x64)
    public const int PaFace = 0x10, PaMaxHp = 0x14, PaCurHp = 0x18, PaSpeed = 0x24, PaBaseAtk = 0x40;
    // Equipment
    public const int EqCoin = 0x10, EqHasKey = 0x14;

    private const int Chunk = 0x1000000;

    private readonly IntPtr _h;
    private readonly List<(string Name, long Lo, long Hi)> _mods = new();

    public int Pid { get; }

    public MemoryEngine(int pid)
    {
        Pid = pid;
        _h = Native.OpenProcess(Native.PROCESS_ALL_ACCESS, false, (uint)pid);
        if (_h == IntPtr.Zero)
            _h = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_VM_WRITE |
                                    Native.PROCESS_VM_OPERATION | Native.PROCESS_QUERY_INFORMATION,
                                    false, (uint)pid);
        if (_h == IntPtr.Zero)
            throw new InvalidOperationException("OpenProcess 失败，请以管理员身份运行本程序。");

        IntPtr snap = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPMODULE | Native.TH32CS_SNAPMODULE32, (uint)pid);
        if (snap != IntPtr.Zero && snap != new IntPtr(-1))
        {
            var me = new Native.MODULEENTRY32W { dwSize = (uint)Marshal.SizeOf<Native.MODULEENTRY32W>() };
            if (Native.Module32FirstW(snap, ref me))
            {
                do
                {
                    long b = me.modBaseAddr.ToInt64();
                    if (b != 0) _mods.Add((me.szModule, b, b + me.modBaseSize));
                } while (Native.Module32NextW(snap, ref me));
            }
            Native.CloseHandle(snap);
        }
    }

    public bool InModule(long v) => _mods.Any(m => v >= m.Lo && v < m.Hi);

    public byte[]? Read(long addr, int size)
    {
        var buf = new byte[size];
        return Native.ReadProcessMemory(_h, new IntPtr(addr), buf, new IntPtr(size), out var got) && got.ToInt64() == size
            ? buf : null;
    }

    public bool Write(long addr, byte[] data) =>
        Native.WriteProcessMemory(_h, new IntPtr(addr), data, new IntPtr(data.Length), out var w) &&
        w.ToInt64() == data.Length;

    public bool WriteInt(long addr, int value) => Write(addr, BitConverter.GetBytes(value));

    private IEnumerable<(long Base, long Size)> Regions()
    {
        long addr = 0x10000;
        while (true)
        {
            if (Native.VirtualQueryEx(_h, new IntPtr(addr), out var mbi,
                    new IntPtr(Marshal.SizeOf<Native.MEMORY_BASIC_INFORMATION>())) == IntPtr.Zero)
                yield break;

            long b = mbi.BaseAddress.ToInt64();
            long size = (long)mbi.RegionSize;
            if (size <= 0) yield break;
            addr = b + size;

            if (mbi.State != Native.MEM_COMMIT) continue;
            if ((mbi.Protect & (Native.PAGE_GUARD | Native.PAGE_NOACCESS)) != 0) continue;
            if (size > 0x2000000) continue;
            yield return (b, size);
        }
    }

    public List<long> ScanInt(int value)
    {
        var hits = new List<long>();
        var pat = BitConverter.GetBytes(value);
        foreach (var (baseAddr, size) in Regions())
        {
            long off = 0;
            while (off < size)
            {
                int n = (int)Math.Min(Chunk, size - off);
                var data = Read(baseAddr + off, n);
                if (data == null) break;
                for (int i = 0; i + 4 <= data.Length; i += 4)
                {
                    if (data[i] == pat[0] && data[i + 1] == pat[1] &&
                        data[i + 2] == pat[2] && data[i + 3] == pat[3])
                        hits.Add(baseAddr + off + i);
                }
                off += n;
            }
        }
        return hits;
    }

    public List<PlayerAttr> FindPlayerAttributes(int maxHp)
    {
        var res = new List<PlayerAttr>();
        foreach (var a in ScanInt(maxHp))
        {
            var head = Read(a - 0x10, 0x50);
            if (head == null) continue;

            long vt = BitConverter.ToInt64(head, 0);
            if (!InModule(vt)) continue;

            int face = BitConverter.ToInt32(head, PaFace);
            int mx = BitConverter.ToInt32(head, PaMaxHp);
            int cur = BitConverter.ToInt32(head, PaCurHp);
            float spd = BitConverter.ToSingle(head, PaSpeed);
            int atk = BitConverter.ToInt32(head, PaBaseAtk);

            if ((face == 1 || face == -1) && mx == maxHp && cur >= 0 && cur <= mx &&
                spd > 0f && spd < 60f && atk > 0 && atk < 100000)
                res.Add(new PlayerAttr(a - PaMaxHp, mx, cur, spd, atk));
        }
        return res;
    }

    public List<EquipmentObj> FindEquipments(int coin)
    {
        var res = new List<EquipmentObj>();
        foreach (var a in ScanInt(coin))
        {
            var head = Read(a - EqCoin, 0x20);
            if (head == null) continue;

            long vt = BitConverter.ToInt64(head, 0);
            if (!InModule(vt)) continue;

            int c = BitConverter.ToInt32(head, EqCoin);
            byte hk = head[EqHasKey];
            if (c == coin && (hk == 0 || hk == 1))
                res.Add(new EquipmentObj(a - EqCoin, c, hk));
        }
        return res;
    }

    public void Dispose()
    {
        if (_h != IntPtr.Zero) Native.CloseHandle(_h);
    }
}
