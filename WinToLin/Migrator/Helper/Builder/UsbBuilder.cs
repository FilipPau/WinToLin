using System.IO;

namespace WinToLin.Migration;

public static class UsbBuilder
{
    public static void CreateStructure(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "WinToLinBackup", "data"));
        Directory.CreateDirectory(Path.Combine(root, "autoinstall"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "boot"));
    }
}