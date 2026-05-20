using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinToLin.Views.Steps;

namespace WinToLin.Logic.Utils
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
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Dictionary<string, CompatibilityInfo> _softwareDb = new();

        private static readonly List<string> AntiCheatBlockedGames = new()
        {
            "Valorant", "Call of Duty: Warzone", "Battlefield 2042", 
            "Apex Legends", "Fortnite", "League of Legends"
        };

        public static void PerformBackgroundScan(IProgress<Software.SoftwareInfo> progress, bool hideRandomWinStuff)
        {
            LoadSoftwareDb();
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

                    // Clean, cut trailing versions/tags, and consolidate
                    string cleanedName = GetCleanedSoftwareName(name);

                    if (seenNames.Contains(cleanedName)) continue;
                    if (hideRandomWinStuff && filter.Any(x => cleanedName.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;

                    seenNames.Add(cleanedName);

                    string iconReg = sk.GetValue("DisplayIcon") as string;
                    string installDir = sk.GetValue("InstallLocation") as string;
                    
                    var lookup = GetSoftwareInfoFromDb(cleanedName);
                    var iconSource = FindBestIcon(iconReg, installDir);

                    bool isNative = lookup.Type?.ToLower() == "native";
                    bool isAntiCheat = AntiCheatBlockedGames.Any(x => cleanedName.Contains(x, StringComparison.OrdinalIgnoreCase));

                    var info = new Software.SoftwareInfo
                    {
                        Name = cleanedName,
                        Version = sk.GetValue("DisplayVersion") as string ?? "1.0",
                        Icon = iconSource,
                        Install = false,
                        HasLinuxNative = isNative,
                        HasLinuxAlternative = lookup.Type?.ToLower() == "alternative",
                        HasAntiCheat = isAntiCheat,
                        SortOrder = isNative ? 0 : (isAntiCheat ? 1 : 2),
                        Alternatives = lookup.Alternatives?.Select(a => new Software.AlternativeInfo { Name = a }).ToList() ?? new()
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

            // 1. Maintain specific Python version separation cleanly (e.g. "Python 3.12", "Python 3.11")
            if (name.Contains("Python", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(name, @"Python\s+(\d+\.\d+)", RegexOptions.IgnoreCase);
                if (match.Success) return match.Value;
                if (name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) return "Python Launcher";
            }

            // 2. Remove registration or trade symbols
            name = name.Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("™", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("®", "", StringComparison.OrdinalIgnoreCase);

            // 3. Remove known sub-component phrases, add-ins, and marketplace tags
            string[] subComponentKeywords = new[] 
            { 
                "Test Suite", "Standard Library", "Development Libraries", "pip Bootstrap", 
                "Core Interpreter", "Documentation", "Meeting Add-in for Microsoft Office", 
                "WebView2-Laufzeit", "WebView2 Runtime", "- Steam", "Installer"
            };
            foreach (var keyword in subComponentKeywords)
            {
                name = name.Replace(keyword, "", StringComparison.OrdinalIgnoreCase);
            }

            // 4. Strip Parentheses blocks containing architecture, build extensions, or languages (e.g., (x64 de), ( x64))
            name = Regex.Replace(name, @"\([\s\w\-]*\)", "");

            // 5. Remove trailing standalone bits or typical version tags (e.g., CE, v8.0)
            name = name.Replace(" CE", "", StringComparison.OrdinalIgnoreCase);

            // 6. Cut trailing raw version numbers (e.g., "WinRAR 6.23" -> "WinRAR", "Unity Hub 3.16.4" -> "Unity Hub")
            // Matches trailing patterns like " 6.23", " 21.0.2", " 7.1.10", " 8.0.100"
            name = Regex.Replace(name, @"\s+\d+(\.\d+)+$", "");

            // 7. Strip loose trailing architecture words if any are left unparenthesized
            name = name.Replace("64-bit", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("32-bit", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("x64", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("x86", "", StringComparison.OrdinalIgnoreCase);

            // 8. Final pass to fix multiple blank spaces or lingering trailing punctuation
            name = Regex.Replace(name, @"\s+", " ");
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
                                       .Where(f => f.Extension == ".exe" || f.Extension == ".ico")
                                       .OrderByDescending(f => f.Extension == ".ico")
                                       .Take(3);

                    foreach (var file in files)
                    {
                        var result = ExtractHighResIcon(file.FullName);
                        if (result != null) return result;
                    }
                }
            } catch { }
            return null;
        }

        private static BitmapSource ExtractHighResIcon(string path)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    bs.Freeze(); 
                    DestroyIcon(shfi.hIcon);
                    return bs;
                }
            }
            catch { }
            return null;
        }

        private static void LoadSoftwareDb()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "software_alternatives_db.json");
                if (!File.Exists(jsonPath)) return;
                _softwareDb = JsonSerializer.Deserialize<Dictionary<string, CompatibilityInfo>>(File.ReadAllText(jsonPath), 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            } catch { _softwareDb = new(); }
        }

        private static CompatibilityInfo GetSoftwareInfoFromDb(string name)
        {
            var key = name.ToLower();
            return _softwareDb.FirstOrDefault(x => key.Contains(x.Key.ToLower())).Value ?? new CompatibilityInfo { Type = "unknown" };
        }

        private class CompatibilityInfo 
        { 
            [JsonPropertyName("type")] public string Type { get; set; } 
            [JsonPropertyName("alternatives")] public List<string> Alternatives { get; set; } 
        }
    }
}