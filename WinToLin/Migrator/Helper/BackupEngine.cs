using System.IO;
using System.Threading.Tasks;

namespace WinToLin.Migration;

public static class BackupEngine
{
    public static async Task CopyAsync(System.Collections.Generic.List<string> paths, string usbRoot)
    {
        string target = Path.Combine(usbRoot, "WinToLinBackup", "data");

        foreach (var path in paths)
        {
            if (!Directory.Exists(path)) continue;

            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            string dest = Path.Combine(target, name);

            await Task.Run(() => CopyDir(path, dest));
        }
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);

        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);

        foreach (var dir in Directory.GetDirectories(src))
            CopyDir(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }
}