using System.Text;
using System.Text.RegularExpressions;

namespace IceyTuner.Core;

/// <summary>
/// Steam 云存档 save_data.bin：7-bit 变长长度前缀 + UTF-8 JSON + 补零。
/// 写入采用「按字段正则替换」，除目标数字外一个字节都不动，
/// 避免 JSON 重新序列化造成的浮点格式漂移。
/// </summary>
internal sealed class SaveFile
{
    public string FilePath { get; }
    public string BakPath => FilePath + ".bak";

    public SaveFile(string path) => FilePath = path;

    private static (int Length, int Prefix) ReadVarint(byte[] b)
    {
        int val = 0, shift = 0, i = 0;
        while (true)
        {
            byte by = b[i];
            val |= (by & 0x7F) << shift;
            shift += 7;
            i++;
            if ((by & 0x80) == 0) return (val, i);
        }
    }

    private static byte[] WriteVarint(int val)
    {
        var outp = new List<byte>();
        while (true)
        {
            byte by = (byte)(val & 0x7F);
            val >>= 7;
            if (val != 0) outp.Add((byte)(by | 0x80));
            else { outp.Add(by); return outp.ToArray(); }
        }
    }

    private (string Text, int Prefix, int Size) ReadText()
    {
        var raw = File.ReadAllBytes(FilePath);
        var (len, prefix) = ReadVarint(raw);
        return (Encoding.UTF8.GetString(raw, prefix, len), prefix, raw.Length);
    }

    public (int MaxHp, int CurrentHp, int Coin, int Size) ReadValues()
    {
        var (text, _, size) = ReadText();
        return (MatchInt(text, "maxHP"), MatchInt(text, "currentHP"), MatchInt(text, "CoinNum"), size);
    }

    private static int MatchInt(string text, string key)
    {
        var m = Regex.Match(text, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
    }

    /// <summary>只替换传入的字段，null 表示不动。</summary>
    public void Write(int? maxHp = null, int? coin = null, bool fillHp = true)
    {
        var (text, _, size) = ReadText();

        if (maxHp.HasValue)
        {
            text = ReplaceInt(text, "maxHP", maxHp.Value);
            if (fillHp) text = ReplaceInt(text, "currentHP", maxHp.Value);
        }
        if (coin.HasValue)
            text = ReplaceInt(text, "CoinNum", coin.Value);

        var body = Encoding.UTF8.GetBytes(text);
        var head = WriteVarint(body.Length);
        if (head.Length + body.Length > size)
            throw new InvalidOperationException("新存档超出原文件大小");

        var outp = new byte[size];
        head.CopyTo(outp, 0);
        body.CopyTo(outp, head.Length);

        if (!File.Exists(BakPath)) File.Copy(FilePath, BakPath, true);
        using var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        fs.Write(outp, 0, outp.Length);
    }

    private static string ReplaceInt(string text, string key, int value) =>
        Regex.Replace(text, "(\"" + key + "\"\\s*:\\s*)-?\\d+", "${1}" + value);

    public bool Restore()
    {
        if (!File.Exists(BakPath)) return false;
        File.Copy(BakPath, FilePath, true);
        return true;
    }
}
