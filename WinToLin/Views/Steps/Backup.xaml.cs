using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using WinToLin.Helper;
using WinToLin.logic.manager;

namespace WinToLin.Views.Steps;

public partial class Backup : UserControl
{
    private ConfigManager _configManager;
    public ObservableCollection<BackupItem> AllBackupItems { get; set; } = new();
    public ICollectionView GroupedItems { get; set; }

    private bool _isInternalUpdate = false;
    private string _currentLocale;

    // ==========================================
    // EXTENSIBLE LANGUAGE DICTIONARY
    // Casing here defines the target casing on Linux!
    // ==========================================
    private readonly Dictionary<string, Dictionary<string, string>> _languagePack = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new() {
            { "Documents", "Documents" }, { "Pictures", "Pictures" }, { "Videos", "Videos" },
            { "Music", "Music" }, { "Desktop", "Desktop" }, { "Downloads", "Downloads" }
        },
        ["de"] = new() {
            { "Documents", "Dokumente" }, { "Pictures", "Bilder" }, { "Videos", "Videos" },
            { "Music", "Musik" }, { "Desktop", "Schreibtisch" }, { "Downloads", "Downloads" }
        }
    };

    public Backup()
    {
        InitializeComponent();
        _configManager = ConfigManager.Instance;
        
        _currentLocale = SystemSettingsHelper.GetLanguage().Substring(0, 2).ToLower();

        GroupedItems = CollectionViewSource.GetDefaultView(AllBackupItems);
        GroupedItems.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        MasterBackupList.ItemsSource = GroupedItems;

        Loaded += Backup_Loaded;
    }

    private async void Backup_Loaded(object sender, RoutedEventArgs e)
    {
        StepManager.Instance.MainTaskCompleted();
        if (AllBackupItems.Count > 0) return; 

        await LoadBackupLocationsAsync();
        UpdateTotalCount();
    }

    private async Task LoadBackupLocationsAsync()
    {
        await Task.Run(() =>
        {
            AddStandardFolder("Documents", Environment.SpecialFolder.MyDocuments);
            AddStandardFolder("Pictures", Environment.SpecialFolder.MyPictures);
            AddStandardFolder("Videos", Environment.SpecialFolder.MyVideos);
            AddStandardFolder("Music", Environment.SpecialFolder.MyMusic);
            AddStandardFolder("Desktop", Environment.SpecialFolder.DesktopDirectory);
            
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddIfExists("Standard Folders", GetTranslation("Downloads"), Path.Combine(userPath, "Downloads"), "Downloads");
            
            AddIfExists("Application Configs", "VSCode", Path.Combine(userPath, "AppData", "Roaming", "Code"), ".config/Code");
            AddIfExists("Application Configs", "SSH Keys", Path.Combine(userPath, ".ssh"), ".ssh");
        });
    }

    private void AddStandardFolder(string key, Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder);
        // Pass 'key' as the hint so MapToLinux knows which translation to look up
        AddIfExists("Standard Folders", GetTranslation(key), path, key);
    }

    private void AddIfExists(string category, string name, string physicalPath, string linuxHint = null)
    {
        if (string.IsNullOrEmpty(physicalPath) || !Directory.Exists(physicalPath)) return;

        Dispatcher.Invoke(() =>
        {
            if (IsPathAlreadyCovered(physicalPath)) return;

            string linuxPath = MapToLinux(physicalPath, linuxHint);

            AllBackupItems.Add(new BackupItem {
                Category = category,
                Name = name, 
                Path = physicalPath, // Removed .ToLower()
                PhysicalPath = physicalPath, 
                LinuxPath = linuxPath,
                Icon = GetIcon(physicalPath)
            });
            _configManager.AddBackupPath(physicalPath, linuxPath);
        });
    }

    private string GetTranslation(string key)
    {
        if (_languagePack.TryGetValue(_currentLocale, out var lang) && lang.TryGetValue(key, out var translated))
            return translated;
        
        return _languagePack["en"][key];
    }

    private string MapToLinux(string winPath, string linuxHint)
    {
        // Keep the username lowercase (standard Linux practice) but paths respect dictionary
        string user = _configManager.UserName.ToLower();
        
        // If we have a hint (like "Documents"), find the translation for that specific key
        string targetFolder = linuxHint;

        if (targetFolder != null && _languagePack.TryGetValue(_currentLocale, out var lang))
        {
            if (lang.TryGetValue(targetFolder, out var translated))
            {
                targetFolder = translated;
            }
        }

        // If no hint or no translation found, fall back to the actual folder name on disk
        targetFolder ??= Path.GetFileName(winPath);

        return $"/home/{user}/{targetFolder}";
    }

    private void MasterBackupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MasterBackupList.SelectedItem is BackupItem item)
        {
            _isInternalUpdate = true;
            DetailPanel.Visibility = Visibility.Visible;
            EmptyDetailPlaceholder.Visibility = Visibility.Collapsed;
            SourcePathTxt.Text = item.Path;
            TargetPathInput.Text = item.LinuxPath;
            _isInternalUpdate = false;
        }
        else
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            EmptyDetailPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void TargetPathInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInternalUpdate) return;
        if (MasterBackupList.SelectedItem is BackupItem item)
        {
            // Allow manual user edits to maintain their casing
            item.LinuxPath = TargetPathInput.Text;
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            string newPath = Path.GetFullPath(dialog.FolderName);
            if (IsPathAlreadyCovered(newPath)) return;

            var children = AllBackupItems.Where(x => IsSubPath(x.PhysicalPath, newPath)).ToList();
            foreach (var child in children) AllBackupItems.Remove(child);

            string linuxPath = MapToLinux(newPath, null);

            AllBackupItems.Add(new BackupItem {
                Category = "Custom Locations",
                Name = Path.GetFileName(newPath),
                Path = newPath, // Preserved casing
                PhysicalPath = newPath,
                LinuxPath = linuxPath,
                Icon = GetIcon(newPath)
            });
            _configManager.AddBackupPath(newPath, linuxPath);
            UpdateTotalCount();
        }
    }

    // Comparison for "already covered" still uses OrdinalIgnoreCase because 
    // Windows doesn't allow "C:\Data" and "C:\data" to coexist.
    private bool IsPathAlreadyCovered(string newPath)
    {
        string normNew = NormalizePath(newPath);
        return AllBackupItems.Any(i => normNew.StartsWith(NormalizePath(i.PhysicalPath), StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSubPath(string child, string parent) => 
        NormalizePath(child).StartsWith(NormalizePath(parent), StringComparison.OrdinalIgnoreCase) && child.Length > parent.Length;

    private string NormalizePath(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BackupItem item })
        {
            AllBackupItems.Remove(item);
            _configManager.RemoveBackupPath(item.PhysicalPath);
            UpdateTotalCount();
        }
    }

    private void UpdateTotalCount() => TotalCountDisplay.Text = AllBackupItems.Count.ToString();

    private BitmapImage? GetIcon(string path)
    {
        try {
            string iconName = Directory.Exists(path) ? "folder_icon.png" : "file_icon.png";
            return new BitmapImage(new Uri($"pack://application:,,,/Assets/{iconName}"));
        } catch { return null; }
    }
}

public class BackupItem
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty; 
    public string PhysicalPath { get; set; } = string.Empty; 
    public string LinuxPath { get; set; } = string.Empty;
    public BitmapImage? Icon { get; set; }
}