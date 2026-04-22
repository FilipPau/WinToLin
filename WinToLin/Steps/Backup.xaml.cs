using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WinToLin.Steps;

public partial class Backup : UserControl
{
    private Manager manager;

    public ObservableCollection<BackupItem> PersonalFiles { get; set; } = new();
    public ObservableCollection<BackupItem> AppData { get; set; } = new();
    public ObservableCollection<BackupItem> CustomLocations { get; set; } = new();

    private static bool hasLoaded = false;
    
    public Backup()
    {

        
        InitializeComponent();

        manager = Manager.Instance;

        Loaded += Backup_Loaded;

        PersonalFilesList.ItemsSource = PersonalFiles;
        AppDataList.ItemsSource = AppData;
        CustomLocationsList.ItemsSource = CustomLocations;
    }

    private async void Backup_Loaded(object sender, RoutedEventArgs e)
    {
        if (hasLoaded)
            return; 

        hasLoaded = true;
        
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(100);
        await LoadBackupLocationsAsync();
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task LoadBackupLocationsAsync()
    {
        await Task.Run(() =>
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            AddIfExists(PersonalFiles, "Documents", Path.Combine(user, "Documents"));
            AddIfExists(PersonalFiles, "Pictures", Path.Combine(user, "Pictures"));
            AddIfExists(PersonalFiles, "Videos", Path.Combine(user, "Videos"));
            AddIfExists(PersonalFiles, "Music", Path.Combine(user, "Music"));
            AddIfExists(PersonalFiles, "Downloads", Path.Combine(user, "Downloads"));
            AddIfExists(PersonalFiles, "Desktop", Path.Combine(user, "Desktop"));

            AddIfExists(AppData, "VSCode Settings", Path.Combine(user, "AppData", "Roaming", "Code"));
            AddIfExists(AppData, "Firefox Profile", Path.Combine(user, "AppData", "Roaming", "Mozilla"));
            AddIfExists(AppData, "SSH Keys", Path.Combine(user, ".ssh"));
            AddIfExists(AppData, "Git Config", Path.Combine(user, ".gitconfig"));
            AddIfExists(AppData, "Steam Saves", Path.Combine(user, "AppData", "Roaming", "Steam"));
        });
    }

    private void AddIfExists(ObservableCollection<BackupItem> list, string name, string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            Dispatcher.Invoke(() =>
            {
                var item = new BackupItem
                {
                    Name = name,
                    Path = path,
                    Icon = GetIcon(path)
                };

                list.Add(item);
                manager.AddBackupPath(path);
            });
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            AddCustomLocation(dialog.FolderName);
        }
    }

    private void AddCustomLocation(string path)
    {
        path = Path.GetFullPath(path);

        // 1. If already covered → do nothing
        if (ExistsInAnyHierarchy(path))
        {
            MessageBox.Show("This folder (or its parent/child) is already included.");
            return;
        }

        // 2. Remove all subfolders of this new path (it takes over them)
        for (int i = CustomLocations.Count - 1; i >= 0; i--)
        {
            if (IsSubPath(CustomLocations[i].Path, path))
            {
                manager.RemoveBackupPath(CustomLocations[i].Path);
                CustomLocations.RemoveAt(i);
            }
        }

        // 3. Remove all parents of this path (they are replaced by the more specific one OR vice versa rule)
        for (int i = CustomLocations.Count - 1; i >= 0; i--)
        {
            if (IsSubPath(path, CustomLocations[i].Path))
            {
                manager.RemoveBackupPath(CustomLocations[i].Path);
                CustomLocations.RemoveAt(i);
            }
        }

        // 4. Add new item
        CustomLocations.Add(new BackupItem
        {
            Name = Path.GetFileName(path),
            Path = path,
            Icon = GetIcon(path)
        });

        manager.AddBackupPath(path);
    }

    private bool IsSameOrNested(string a, string b)
    {
        a = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        b = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
               || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private bool ExistsInAnyHierarchy(string path)
    {
        path = Path.GetFullPath(path);

        foreach (var item in PersonalFiles)
            if (IsSameOrNested(path, item.Path))
                return true;

        foreach (var item in AppData)
            if (IsSameOrNested(path, item.Path))
                return true;

        foreach (var item in CustomLocations)
            if (IsSameOrNested(path, item.Path))
                return true;

        return false;
    }

    private bool IsSubPath(string child, string parent)
    {
        var childPath = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parentPath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private bool ExistsInAny(string path)
    {
        return PersonalFiles.Any(x => x.Path == path)
               || AppData.Any(x => x.Path == path)
               || CustomLocations.Any(x => x.Path == path);
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not BackupItem item) return;

        PersonalFiles.Remove(item);
        AppData.Remove(item);
        CustomLocations.Remove(item);

        manager.RemoveBackupPath(item.Path);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }

    private BitmapImage GetIcon(string path)
    {
        try
        {
            string icon = Directory.Exists(path) ? "folder_icon.png" : "file_icon.png";

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri($"pack://application:,,,/Assets/{icon}");
            bi.EndInit();
            return bi;
        }
        catch
        {
            return null;
        }
    }
}

public class BackupItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public BitmapImage Icon { get; set; }
}