using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WinToLin.Steps
{
    public partial class Software : UserControl
    {
        private Manager manager;
        private Dictionary<string, CompatibilityInfo> softwareDb = new();
        private List<SoftwareInfo> allSoftware = new();
        private bool _hideRandomWinStuff = true;

        private static readonly List<string> AntiCheatBlockedGames = new()
        {
            "Valorant",
            "Call of Duty: Warzone",
            "Battlefield 2042",
            "Apex Legends",
            "Fortnite",
            "League of Legends"
        };

        public Software()
        {
            
            manager = Manager.Instance;

            InitializeComponent();
            Loaded += Software_Loaded;
        }

        #region 1

        // Fired when a software is selected
        private void Software_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            if (cb.DataContext is not SoftwareInfo software) return;

            OnSoftwareSelected(software.Name);
        }

// Fired when a software is deselected
        private void Software_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            if (cb.DataContext is not SoftwareInfo software) return;

            OnSoftwareDeselected(software.Name);
        }

// 🔥 THIS is the function you said you will replace
        private void OnSoftwareSelected(string name)
        {
            manager.AddSoftware(name);
        }

        private void OnSoftwareDeselected(string name)
        {
            manager.RemoveSoftware(name);
        }

        #endregion
        
        
        private async void Software_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            SearchBox.IsEnabled = false;

            await Task.Delay(200);
            await LoadSoftware();
        }

        private async Task LoadSoftware()
        {
            var results = await Task.Run(PerformBackgroundScan);

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                allSoftware = results;
                SoftwareList.ItemsSource = allSoftware;

                LoadingOverlay.Visibility = Visibility.Collapsed;
                SearchBox.IsEnabled = true;
            }));
        }

        private List<SoftwareInfo> PerformBackgroundScan()
        {
            LoadSoftwareDb();

            var installed = GetInstalledPrograms();
            var list = new List<SoftwareInfo>();

            foreach (var program in installed)
            {
                var lookup = GetSoftwareInfoFromDb(program.Name);

                list.Add(new SoftwareInfo
                {
                    Name = program.Name,
                    Version = program.Version,
                    InstallPath = program.InstallPath,
                    IconPath = program.IconPath,
                    Icon = ExtractIcon(program.IconPath),
                    Install = false,
                    HasLinuxNative = lookup.Type?.ToLower() == "native",
                    HasLinuxAlternative = lookup.Type?.ToLower() == "alternative",
                    IsSteam = program.IsSteam,
                    HasAntiCheat = program.HasAntiCheat,
                    Alternatives = BuildAlternatives(lookup)
                });
            }

            return list
                .OrderBy(x => x.HasLinuxNative ? 0 :
                              x.IsSteam ? 1 :
                              x.HasLinuxAlternative ? 2 : 3)
                .ThenByDescending(x => x.IconPath != null)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private void LoadSoftwareDb()
        {
            try
            {
                string jsonPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "data",
                    "software_alternatives_db.json");

                if (!File.Exists(jsonPath))
                    return;

                string jsonText = File.ReadAllText(jsonPath);

                softwareDb = JsonSerializer.Deserialize<Dictionary<string, CompatibilityInfo>>(
                    jsonText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new();
            }
            catch
            {
                softwareDb = new();
            }
        }

        private CompatibilityInfo GetSoftwareInfoFromDb(string name)
        {
            if (softwareDb == null)
                return new CompatibilityInfo { Type = "unknown" };

            string key = name.ToLower();

            var matchEntry = softwareDb
                .FirstOrDefault(x => key.Contains(x.Key.ToLower()));

            var match = matchEntry.Value;

            return match != null
                ? new CompatibilityInfo
                {
                    Type = match.Type,
                    Alternatives = match.Alternatives
                }
                : new CompatibilityInfo { Type = "unknown" };
        }

        private List<AlternativeInfo> BuildAlternatives(CompatibilityInfo lookup)
        {
            var list = new List<AlternativeInfo>();

            if (lookup?.Alternatives == null)
                return list;

            foreach (var alt in lookup.Alternatives)
            {
                list.Add(new AlternativeInfo
                {
                    Name = alt,
                    Description = "Linux Alternative"
                });
            }

            return list;
        }

        
        private List<SoftwareInfo> GetInstalledPrograms()
        {
            var programs = new List<SoftwareInfo>();

            string[] keys =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            List<string> randomWinStuffPrefix = new()
            {
                "Redistributable","Runtime","Windows","Win","IIS",
                "Microsoft","WinRT","AMD","INTEL"
            };

            List<string> keepOnlyOncePrograms = new()
            {
                "Java",
                "Python"
            };

            foreach (string registryPath in keys)
            {
                using RegistryKey rk = Registry.LocalMachine.OpenSubKey(registryPath);
                if (rk == null) continue;

                foreach (string subkey in rk.GetSubKeyNames())
                {
                    using RegistryKey sk = rk.OpenSubKey(subkey);

                    string name = sk?.GetValue("DisplayName") as string;
                    if (string.IsNullOrEmpty(name)) continue;

                    if (_hideRandomWinStuff &&
                        randomWinStuffPrefix.Any(x => name.Contains(x)))
                        continue;

                    if (keepOnlyOncePrograms.Any(x => name.Contains(x)))
                    {
                        string keepOnceName =
                            keepOnlyOncePrograms.Find(x => name.Contains(x));

                        if (programs.Any(x => x.Name == keepOnceName))
                            continue;

                        name = keepOnceName;
                    }

                    string rawIconPath = sk?.GetValue("DisplayIcon") as string;

                    string cleanPath = !string.IsNullOrEmpty(rawIconPath)
                        ? rawIconPath.Split(',')[0].Trim(' ', '"')
                        : null;

                    if (cleanPath != null && !File.Exists(cleanPath))
                        cleanPath = null;

                    string installLocation =
                        sk?.GetValue("InstallLocation") as string
                        ?? cleanPath
                        ?? "";

                    bool isSteam =
                        installLocation.Contains("steamapps",
                        StringComparison.OrdinalIgnoreCase);

                    bool hasAntiCheat = AntiCheatBlockedGames.Any(x =>
                        name.IndexOf(x,
                        StringComparison.OrdinalIgnoreCase) >= 0);

                    programs.Add(new SoftwareInfo
                    {
                        Name = name,
                        Version = sk?.GetValue("DisplayVersion") as string ?? "Unknown",
                        IconPath = cleanPath,
                        InstallPath = installLocation,
                        IsSteam = isSteam,
                        HasAntiCheat = hasAntiCheat
                    });
                }
            }

            return programs;
        }

        private BitmapSource ExtractIcon(string iconPath)
        {
            try
            {
                if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
                    return null;

                using Icon icon = Icon.ExtractAssociatedIcon(iconPath);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();

                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                BitmapImage bi = new();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();

                return bi;
            }
            catch
            {
                return null;
            }
        }

        private void Alternative_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            if (cb.DataContext is not AlternativeInfo selectedAlt) return;

            var software = allSoftware
                .FirstOrDefault(s => s.Alternatives.Contains(selectedAlt));

            if (software == null) return;

            foreach (var alt in software.Alternatives)
                if (alt != selectedAlt)
                    alt.IsSelected = false;

            SoftwareList.Items.Refresh();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            SoftwareList.ItemsSource = allSoftware
                .Where(s => s.Name.ToLower().Contains(query))
                .ToList();
        }

        private async void HideRandomWindowsStuff_OnChecked(object sender, RoutedEventArgs e)
        {
            _hideRandomWinStuff = true;
            await LoadSoftware();
        }

        private async void HideRandomWindowsStuff_OnUnchecked(object sender, RoutedEventArgs e)
        {
            _hideRandomWinStuff = false;
            await LoadSoftware();
        }

        public class SoftwareInfo
        {
            public bool Install { get; set; }

            public string Name { get; set; }
            public string Version { get; set; }

            public string IconPath { get; set; }
            public BitmapSource Icon { get; set; }

            public string InstallPath { get; set; }

            public bool HasLinuxNative { get; set; }
            public bool HasLinuxAlternative { get; set; }

            public bool IsSteam { get; set; }
            public bool HasAntiCheat { get; set; }

            
            
            public List<AlternativeInfo> Alternatives { get; set; } = new();

            public Visibility ShowAlternatives =>
                Alternatives.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            public Visibility ShowMainCheckbox =>
                Alternatives.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            public bool CanInstall =>
                HasLinuxNative ||
                HasLinuxAlternative ||
                IsSteam;

            public string CompatibilityCategory
            {
                get
                {
                    if (HasLinuxNative) return "Native";
                    if (HasLinuxAlternative) return "Alternative";
                    if (IsSteam && HasAntiCheat) return "AntiCheat";
                    if (IsSteam) return "Steam";
                    return "Unknown";
                }
            }

            public string Compatibility
            {
                get
                {
                    var notes = new List<string>();

                    if (HasLinuxNative)
                        notes.Add("Native Linux");

                    else if (HasLinuxAlternative)
                        notes.Add("Linux alternative");

                    if (IsSteam)
                        notes.Add(HasAntiCheat
                            ? "Steam (anti-cheat, may not run)"
                            : "Steam compatible");

                    if (notes.Count == 0)
                        notes.Add("Unknown");

                    return string.Join(", ", notes);
                }
            }
        }

        public class AlternativeInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }

            public BitmapSource Icon { get; set; }

            public bool IsSelected { get; set; }
        }

        private class CompatibilityInfo
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("alternatives")]
            public List<string> Alternatives { get; set; }
        }
    }
    
    
    
}