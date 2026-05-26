using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinToLin.Views.Steps;

namespace WinToLin.Migrator.DistroIndependent
{
    public static class SoftwareScannerUtil
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // Compiled regular expressions for performance
        private static readonly Regex PythonRegex = new(@"Python\s+(\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SymbolsRegex = new(@"\(TM\)|™|®", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ParenthesesRegex = new(@"\([\s\w\-]*\)", RegexOptions.Compiled);
        private static readonly Regex TrailingVersionRegex = new(@"\s+\d+(\.\d+)+$", RegexOptions.Compiled);
        private static readonly Regex ArchitectureRegex = new(@"64-bit|32-bit|x64|x86", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SpacesRegex = new(@"\s+", RegexOptions.Compiled);

        private static readonly string[] SubComponentKeywords = 
        { 
            "Test Suite", "Standard Library", "Development Libraries", "pip Bootstrap", 
            "Core Interpreter", "Documentation", "Meeting Add-in for Microsoft Office", 
            "WebView2-Laufzeit", "WebView2 Runtime", "- Steam", "Installer"
        };

        public static void PerformBackgroundScan(IProgress<Software.SoftwareInfo> progress, bool hideRandomWinStuff)
        {
            string[] keys = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            var filter = new[] { "Redistributable", "Runtime", "Windows", "Microsoft", "AMD", "INTEL" };
            var seenNames = new HashSet<string>();

            foreach (string path in keys)
            {
                using RegistryKey rk = Registry.LocalMachine.OpenSubKey(path);
                if (rk == null) continue;

                foreach (string subkey in rk.GetSubKeyNames())
                {
                    using RegistryKey sk = rk.OpenSubKey(subkey);
                    if (sk?.GetValue("DisplayName") is not string name || string.IsNullOrWhiteSpace(name)) continue;

                    string cleanedName = GetCleanedSoftwareName(name);

                    if (seenNames.Contains(cleanedName)) continue;
                    if (hideRandomWinStuff && filter.Any(x => cleanedName.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;

                    seenNames.Add(cleanedName);

                    string iconReg = sk.GetValue("DisplayIcon") as string;
                    string installDir = sk.GetValue("InstallLocation") as string;
                    
                    var iconSource = FindBestIcon(iconReg, installDir);

                    var info = new Software.SoftwareInfo
                    {
                        Name = cleanedName,
                        Icon = iconSource,
                        Install = false,
                    };

                    progress.Report(info);
                }
            }
        }

        /// <summary>
        /// Aggressively cleans up application titles by cutting architecture tags, localizations, 
        /// version numbers, and sub-component extensions.
        /// </summary>
        private static string GetCleanedSoftwareName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            // 1. Maintain specific Python version separation cleanly
            if (name.Contains("Python", StringComparison.OrdinalIgnoreCase))
            {
                var match = PythonRegex.Match(name);
                if (match.Success) return match.Value;
                if (name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) return "Python Launcher";
            }

            // 2. Remove registration or trade symbols
            name = SymbolsRegex.Replace(name, "");

            // 3. Remove known sub-component phrases, add-ins, and marketplace tags
            foreach (var keyword in SubComponentKeywords)
            {
                name = name.Replace(keyword, "", StringComparison.OrdinalIgnoreCase);
            }

            // 4. Strip Parentheses blocks containing architecture, build extensions, or languages
            name = ParenthesesRegex.Replace(name, "");

            // 5. Remove trailing standalone bits or typical version tags
            name = name.Replace(" CE", "", StringComparison.OrdinalIgnoreCase);

            // 6. Cut trailing raw version numbers
            name = TrailingVersionRegex.Replace(name, "");

            // 7. Strip loose trailing architecture words if any are left unparenthesized
            name = ArchitectureRegex.Replace(name, "");

            // 8. Final pass to fix multiple blank spaces or lingering trailing punctuation
            name = SpacesRegex.Replace(name, " ");
            return name.Trim(' ', '-', '_');
        }

        private static BitmapSource FindBestIcon(string registryPath, string installDir)
        {
            try
            {
                if (!string.IsNullOrEmpty(registryPath))
                {
                    string path = registryPath.Split(',')[0].Trim(' ', '"');
                    if (File.Exists(path)) return ExtractHighResIcon(path);
                }

                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    var dirInfo = new DirectoryInfo(installDir);
                    var files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                                       .Where(f => f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || 
                                                   f.Extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                                       .OrderByDescending(f => f.Extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                                       .Take(3);

                    foreach (var file in files)
                    {
                        var result = ExtractHighResIcon(file.FullName);
                        if (result != null) return result;
                    }
                }
            }
            catch { }
            return null;
        }

        private static BitmapSource ExtractHighResIcon(string path)
        {
            try
            {
                var shfi = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        
                        bs.Freeze(); 
                        return bs;
                    }
                    finally
                    {
                        DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}