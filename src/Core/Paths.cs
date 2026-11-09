using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace IceyTuner.Core;

internal static class Paths
{
    public const string AppId = "553640";
    public const string GameExe = "ICEY.exe";

    public static string? FindGameDir()
    {
        foreach (var root in SteamRoots())
        {
            var libs = new List<string> { Path.Combine(root, "steamapps", "common") };
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    var txt = File.ReadAllText(vdf);
                    foreach (Match m in Regex.Matches(txt, "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common"));
                }
                catch { /* ignore */ }
            }
            foreach (var lib in libs)
            {
                var g = Path.Combine(lib, "ICEY");
                if (File.Exists(Path.Combine(g, GameExe)))
                    return g;
            }
        }
        return null;
    }

    public static IEnumerable<string> SteamRoots()
    {
        var list = new List<string>();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (k?.GetValue("SteamPath") is string p && !string.IsNullOrWhiteSpace(p))
                list.Add(p.Replace('/', '\\'));
        }
        catch { /* ignore */ }

        foreach (var d in "CDEFGH")
        {
            var s = d + @":\steam";
            if (Directory.Exists(s)) list.Add(s);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
            if (seen.Add(s)) yield return s;
    }

    public static string? FindSaveFile(string? gameDir)
    {
        if (gameDir == null) return null;
        var dir = new DirectoryInfo(gameDir);
        for (int i = 0; i < 5 && dir != null; i++)
        {
            dir = dir.Parent;
            if (dir == null) break;
            var ud = Path.Combine(dir.FullName, "userdata");
            if (!Directory.Exists(ud)) continue;

            var cands = new List<string>();
            foreach (var acc in Directory.GetDirectories(ud))
            {
                var p = Path.Combine(acc, AppId, "remote", "save_data.bin");
                if (File.Exists(p)) cands.Add(p);
            }
            if (cands.Count > 0)
                return cands.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }
        return null;
    }
}
