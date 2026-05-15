using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinToLin.logic.manager;

namespace WinToLin.Views.Steps
{
    public partial class Software : UserControl
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
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DestroyIcon(IntPtr hIcon);

        private readonly ConfigManager _configManager;
        private Dictionary<string, CompatibilityInfo> _softwareDb = new();
        public ObservableCollection<SoftwareInfo> _displaySoftware { get; set; } = new();
        private List<SoftwareInfo> _allSoftwareCache = new();
        private bool _hideRandomWinStuff = true;
        private bool _isLoaded = false;

        private static readonly List<string> AntiCheatBlockedGames = new()
        {
            "Valorant", "Call of Duty: Warzone", "Battlefield 2042", 
            "Apex Legends", "Fortnite", "League of Legends"
        };

        public Software()
        {
            _configManager = ConfigManager.Instance;
            InitializeComponent();
            DataContext = this;
            Loaded += Software_Loaded;
        }

        private async void Software_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            await LoadSoftwareAsync();
            _isLoaded = true;
            StepManager.Instance.MainTaskCompleted();
        }

        private async Task LoadSoftwareAsync()
        {
            SearchBox.IsEnabled = false;
            _displaySoftware.Clear();
            _allSoftwareCache.Clear();

            var progress = new Progress<SoftwareInfo>(software =>
            {
                _displaySoftware.Add(software);
                _allSoftwareCache.Add(software);
            });

            await Task.Run(() => PerformBackgroundScan(progress));
            SearchBox.IsEnabled = true;
        }

        private void PerformBackgroundScan(IProgress<SoftwareInfo> progress)
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
                    if (seenNames.Contains(name)) continue;
                    if (_hideRandomWinStuff && filter.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;

                    seenNames.Add(name);

                    string iconReg = sk.GetValue("DisplayIcon") as string;
                    string installDir = sk.GetValue("InstallLocation") as string;
                    
                    var lookup = GetSoftwareInfoFromDb(name);
                    var iconSource = FindBestIcon(iconReg, installDir);

                    bool isNative = lookup.Type?.ToLower() == "native";
                    bool isAntiCheat = AntiCheatBlockedGames.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase));

                    var info = new SoftwareInfo
                    {
                        Name = name,
                        Version = sk.GetValue("DisplayVersion") as string ?? "1.0",
                        Icon = iconSource,
                        Install = false,
                        HasLinuxNative = isNative,
                        HasLinuxAlternative = lookup.Type?.ToLower() == "alternative",
                        HasAntiCheat = isAntiCheat,
                        SortOrder = isNative ? 0 : (isAntiCheat ? 1 : 2),
                        Alternatives = lookup.Alternatives?.Select(a => new AlternativeInfo { Name = a }).ToList() ?? new()
                    };

                    progress.Report(info);
                }
            }
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

        private BitmapSource ExtractHighResIcon(string path)
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

        private void SoftwareList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in e.RemovedItems.OfType<SoftwareInfo>())
            {
                item.Install = false;
                _configManager.RemoveSoftware(item.Name);
            }
            foreach (var item in e.AddedItems.OfType<SoftwareInfo>())
            {
                item.Install = true;
                _configManager.AddSoftware(item.Name);
            }
            SelectAllToggle.IsChecked = SoftwareList.SelectedItems.Count == SoftwareList.Items.Count && SoftwareList.Items.Count > 0;
            StepManager.Instance.MainTaskCompleted();
        }

        private void SelectAll_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectAllToggle.IsChecked == true) SoftwareList.SelectAll(); 
            else SoftwareList.UnselectAll();
        }

        private async void HideComponents_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _hideRandomWinStuff = HideSystemCheckBox.IsChecked ?? false;
            await LoadSoftwareAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();
            var filtered = _allSoftwareCache.Where(s => s.Name.ToLower().Contains(query)).ToList();
            _displaySoftware.Clear();
            foreach (var item in filtered) _displaySoftware.Add(item);
        }

        private void LoadSoftwareDb()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "software_alternatives_db.json");
                if (!File.Exists(jsonPath)) return;
                _softwareDb = JsonSerializer.Deserialize<Dictionary<string, CompatibilityInfo>>(File.ReadAllText(jsonPath), 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            } catch { _softwareDb = new(); }
        }

        private CompatibilityInfo GetSoftwareInfoFromDb(string name)
        {
            var key = name.ToLower();
            return _softwareDb.FirstOrDefault(x => key.Contains(x.Key.ToLower())).Value ?? new CompatibilityInfo { Type = "unknown" };
        }

        public class SoftwareInfo : INotifyPropertyChanged {
            public string Name { get; set; }
            public string Version { get; set; }
            public BitmapSource Icon { get; set; }
            public bool HasLinuxNative { get; set; }
            public bool HasLinuxAlternative { get; set; }
            public bool HasAntiCheat { get; set; }
            public int SortOrder { get; set; } 
            public List<AlternativeInfo> Alternatives { get; set; } = new();
            private bool _install;
            public bool Install { get => _install; set { _install = value; OnPropertyChanged(); } }
            
            public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpper();
            public string Compatibility => HasAntiCheat ? "Anti-Cheat" : (HasLinuxNative ? "Native" : "Unknown");

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class AlternativeInfo { public string Name { get; set; } }
        private class CompatibilityInfo { 
            [JsonPropertyName("type")] public string Type { get; set; } 
            [JsonPropertyName("alternatives")] public List<string> Alternatives { get; set; } 
        }
    }
}