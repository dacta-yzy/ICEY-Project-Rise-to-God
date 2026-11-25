namespace IceyTuner.Core;

/// <summary>改 Assembly-CSharp.dll 里的两处常量：玩家伤害倍率、伤害上限。</summary>
internal sealed class DllPatcher
{
    // PlayerAtk(): 方法体头 + ldc.r4 操作码，全文件唯一
    private static readonly byte[] MultSig =
    {
        0x13, 0x30, 0x07, 0x00, 0x89, 0x00, 0x00, 0x00,
        0x2F, 0x00, 0x00, 0x11, 0x22
    };

    // GetFinalDamage(): ldc.r4 0.0 ; ldc.r4 999.0 ; call Mathf.Clamp ; stloc.1
    // 前 6 字节与后 6 字节稳定，中间 4 字节是上限值本身
    private static readonly byte[] CapHead = { 0x22, 0x00, 0x00, 0x00, 0x00, 0x22 };
    private static readonly byte[] CapTail = { 0x28, 0x39, 0x01, 0x00, 0x0A, 0x0B };

    public const float CapDefault = 999f;
    public const float CapUnlimited = 999999f;

    public string DllPath { get; }
    public string BakPath => DllPath + ".bak";
    private const string Rel = @"ICEY_Data\Managed\Assembly-CSharp.dll";

    public DllPatcher(string gameDir) : this(Path.Combine(gameDir, Rel), true) { }

    private DllPatcher(string dllPath, bool _) => DllPath = dllPath;

    /// <summary>直接指向某个 DLL 文件（测试用）。</summary>
    public static DllPatcher FromDll(string dllPath) => new(dllPath, true);

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != needle[0]) continue;
            bool ok = true;
            for (int j = 1; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    private static int FindCapOffset(byte[] b)
    {
        int fallback = -1;
        for (int i = 0; i + 16 < b.Length; i++)
        {
            if (b[i] != CapHead[0]) continue;
            bool ok = true;
            for (int j = 1; j < CapHead.Length; j++)
                if (b[i + j] != CapHead[j]) { ok = false; break; }
            if (!ok) continue;
            for (int j = 0; j < CapTail.Length; j++)
                if (b[i + 10 + j] != CapTail[j]) { ok = false; break; }
            if (!ok) continue;

            // 同一段 IL 模式在别处也出现过（值为 1.0 的那种），用数值区分
            float v = BitConverter.ToSingle(b, i + 6);
            if (Math.Abs(v - CapDefault) < 0.5f || Math.Abs(v - CapUnlimited) < 1f)
                return i + 6;
            if (fallback < 0 && v >= 100f && v <= 1e7f)
                fallback = i + 6;
        }
        return fallback;
    }

    /// <summary>读取当前伤害上限；null 表示找不到常量。</summary>
    public float? ReadCap()
    {
        if (!File.Exists(DllPath)) return null;
        var b = File.ReadAllBytes(DllPath);
        int off = FindCapOffset(b);
        return off < 0 ? null : BitConverter.ToSingle(b, off);
    }

    public float WriteCap(bool unlimited)
    {
        var b = File.ReadAllBytes(DllPath);
        int off = FindCapOffset(b);
        if (off < 0) throw new InvalidOperationException("DLL 中找不到伤害上限常量，游戏版本可能不同。");

        if (!File.Exists(BakPath)) File.Copy(DllPath, BakPath, true);

        float v = unlimited ? CapUnlimited : CapDefault;
        BitConverter.GetBytes(v).CopyTo(b, off);
        using (var fs = new FileStream(DllPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            fs.Write(b, 0, b.Length);
        return ReadCap() ?? v;
    }

    public float? ReadMultiplier()
    {
        if (!File.Exists(DllPath)) return null;
        var b = File.ReadAllBytes(DllPath);
        int i = IndexOf(b, MultSig);
        if (i < 0) return null;
        return BitConverter.ToSingle(b, i + MultSig.Length);
    }

    public float WriteMultiplier(float mult)
    {
        var b = File.ReadAllBytes(DllPath);
        int i = IndexOf(b, MultSig);
        if (i < 0) throw new InvalidOperationException("DLL 中找不到 PlayerAtk 特征码，游戏版本可能不同。");

        if (!File.Exists(BakPath)) File.Copy(DllPath, BakPath, true);

        BitConverter.GetBytes(mult).CopyTo(b, i + MultSig.Length);
        using (var fs = new FileStream(DllPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            fs.Write(b, 0, b.Length);
        return ReadMultiplier() ?? mult;
    }

    public bool Restore()
    {
        if (!File.Exists(BakPath)) return false;
        File.Copy(BakPath, DllPath, true);
        return true;
    }
}
