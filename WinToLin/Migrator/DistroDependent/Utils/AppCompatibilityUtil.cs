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
using WinToLin.Logic.Manager;

namespace WinToLin.Migrator.DistroDependent.Utils
{
    public class AppCompatibilityUtil
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
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
            uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private Dictionary<string, string> _softwareDb = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex PythonRegex =
            new(@"Python\s+(\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex
            SymbolsRegex = new(@"\(TM\)|™|®", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ParenthesesRegex = new(@"\([\s\w\-]*\)", RegexOptions.Compiled);
        private static readonly Regex TrailingVersionRegex = new(@"\s+\d+(\.\d+)+$", RegexOptions.Compiled);

        private static readonly Regex ArchitectureRegex =
            new(@"64-bit|32-bit|x64|x86", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SpacesRegex = new(@"\s+", RegexOptions.Compiled);

        private static readonly string[] SubComponentKeywords =
        {
            "Test Suite", "Standard Library", "Development Libraries", "pip Bootstrap",
            "Core Interpreter", "Documentation", "Meeting Add-in for Microsoft Office",
            "WebView2-Laufzeit", "WebView2 Runtime", "- Steam", "Installer"
        };

        public class AppResult
        {
            public string Name { get; set; }
            public BitmapSource Icon { get; set; }
            public string Compatibility { get; set; } 
            public string FlatpakId { get; set; }
        }

        public List<AppResult> ScanInstalledAppsAndCheckCompatibility(bool hideRandomWinStuff)
        {
            LoadSoftwareDb();
            var checkedApps = new List<AppResult>();

            string[] keys =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            var filter = new[] { "Redistributable", "Runtime", "Windows", "Microsoft", "AMD", "INTEL", "Services", "Service", "Prerequisites", "Updater", "IIS", "Verifier" };
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    if (hideRandomWinStuff &&
                        filter.Any(x => cleanedName.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;

                    seenNames.Add(cleanedName);

                    string iconReg = sk.GetValue("DisplayIcon") as string;
                    string installDir = sk.GetValue("InstallLocation") as string;

                    var iconSource = FindBestIcon(iconReg, installDir);

                    string compType;
                    string flatpakId;

                    if (subkey.Contains("Steam", StringComparison.OrdinalIgnoreCase) || 
                        (!string.IsNullOrEmpty(installDir) && installDir.Contains("Steam", StringComparison.OrdinalIgnoreCase)))
                    {
                        compType = "Native";
                        flatpakId = "com.valvesoftware.Steam";
                    }
                    else
                    {
                        string lowercaseLookupKey = cleanedName.ToLower();
                        var dbMatch = _softwareDb.FirstOrDefault(x => 
                            lowercaseLookupKey.Contains(x.Key) || x.Key.Contains(lowercaseLookupKey));

                        if (dbMatch.Key != null)
                        {
                            compType = "Native";
                            flatpakId = dbMatch.Value;
                        }
                        else
                        {
                            compType = "Incompatible";
                            flatpakId = null;
                        }
                    }

                    checkedApps.Add(new AppResult
                    {
                        Name = cleanedName,
                        Icon = iconSource,
                        Compatibility = compType,
                        FlatpakId = flatpakId
                    });
                }
            }

            return checkedApps;
        }

        private string GetCleanedSoftwareName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            if (name.Contains("Python", StringComparison.OrdinalIgnoreCase))
            {
                var match = PythonRegex.Match(name);
                if (match.Success) return match.Value;
                if (name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) return "Python Launcher";
            }

            name = SymbolsRegex.Replace(name, "");

            foreach (var keyword in SubComponentKeywords)
            {
                name = name.Replace(keyword, "", StringComparison.OrdinalIgnoreCase);
            }

            name = ParenthesesRegex.Replace(name, "");
            name = name.Replace(" CE", "", StringComparison.OrdinalIgnoreCase);
            name = TrailingVersionRegex.Replace(name, "");
            name = ArchitectureRegex.Replace(name, "");
            name = SpacesRegex.Replace(name, " ");

            return name.Trim(' ', '-', '_');
        }

        private BitmapSource FindBestIcon(string registryPath, string installDir)
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
            catch
            {
            }

            return null;
        }

        private BitmapSource ExtractHighResIcon(string path)
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
            catch
            {
            }

            return null;
        }

        private void LoadSoftwareDb()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Packages", "Universal", "flatpak.json");
                if (!File.Exists(jsonPath)) return;

                var config = JsonSerializer.Deserialize<FlatpakConfig>(
                    File.ReadAllText(jsonPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var tempDb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (config?.Apps != null)
                {
                    foreach (var app in config.Apps)
                    {
                        if (app.Flatpak == null || string.IsNullOrWhiteSpace(app.Flatpak.Id)) continue;

                        if (!string.IsNullOrWhiteSpace(app.Name))
                        {
                            tempDb[app.Name.ToLower().Trim()] = app.Flatpak.Id;
                        }

                        if (app.Aliases != null)
                        {
                            foreach (var alias in app.Aliases)
                            {
                                if (!string.IsNullOrWhiteSpace(alias))
                                {
                                    tempDb[alias.ToLower().Trim()] = app.Flatpak.Id;
                                }
                            }
                        }
                    }
                }

                _softwareDb = tempDb;
            }
            catch
            {
                _softwareDb = new(StringComparer.OrdinalIgnoreCase);
            }
        }

        #region Configuration Data Transfer Objects

        private class FlatpakConfig
        {
            [JsonPropertyName("apps")]
            public List<AppConfig> Apps { get; set; } = new();
        }

        private class AppConfig
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("aliases")]
            public List<string> Aliases { get; set; } = new();

            [JsonPropertyName("flatpak")]
            public FlatpakDetails Flatpak { get; set; }
        }

        private class FlatpakDetails
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }

        #endregion
    }
}