namespace IceyTuner.Core;

/// <summary>改 Assembly-CSharp.dll 里 PlayerDamageCalculate.PlayerAtk() 的基础倍率常量。</summary>
internal sealed class DllPatcher
{
    // PlayerAtk 方法体头 + ldc.r4 操作码，全文件唯一
    private static readonly byte[] Sig =
    {
        0x13, 0x30, 0x07, 0x00, 0x89, 0x00, 0x00, 0x00,
        0x2F, 0x00, 0x00, 0x11, 0x22
    };

    public string DllPath { get; }
    public string BakPath => DllPath + ".bak";
    private const string Rel = @"ICEY_Data\Managed\Assembly-CSharp.dll";

    public DllPatcher(string gameDir) => DllPath = Path.Combine(gameDir, Rel);

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != needle[0]) continue;
            bool ok = true;
            for (int j = 1; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }

    public float? ReadMultiplier()
    {
        if (!File.Exists(DllPath)) return null;
        var b = File.ReadAllBytes(DllPath);
        int i = IndexOf(b, Sig);
        if (i < 0) return null;
        return BitConverter.ToSingle(b, i + Sig.Length);
    }

    public float WriteMultiplier(float mult)
    {
        var b = File.ReadAllBytes(DllPath);
        int i = IndexOf(b, Sig);
        if (i < 0) throw new InvalidOperationException("DLL 中找不到 PlayerAtk 特征码，游戏版本可能不同。");

        if (!File.Exists(BakPath)) File.Copy(DllPath, BakPath, true);

        BitConverter.GetBytes(mult).CopyTo(b, i + Sig.Length);
        using (var fs = new FileStream(DllPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            fs.Write(b, 0, b.Length);
        }
        return ReadMultiplier() ?? mult;
    }

    public bool Restore()
    {
        if (!File.Exists(BakPath)) return false;
        File.Copy(BakPath, DllPath, true);
        return true;
    }
}
