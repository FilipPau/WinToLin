using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WinToLin.Steps;

public partial class Backup : UserControl
{
    private Manager manager;

    public ObservableCollection<BackupItem> PersonalFiles { get; set; } = new();
    public ObservableCollection<BackupItem> AppData { get; set; } = new();
    public ObservableCollection<BackupItem> CustomLocations { get; set; } = new();

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
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(100); // allow overlay to render
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

    // Fired when a backup item is selected
    private void BackupItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not BackupItem item) return;

        OnBackupItemSelected(item.Path);
    }

    private void BackupItem_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not BackupItem item) return;

        OnBackupItemDeselected(item.Path);
    }

    private void OnBackupItemSelected(string path)
    {
        manager.AddBackupPath(path);
    }

    private void OnBackupItemDeselected(string path)
    {
        manager.RemoveBackupPath(path);
    }
    
    private void AddIfExists(ObservableCollection<BackupItem> list, string name, string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            Dispatcher.Invoke(() =>
            {
                list.Add(new BackupItem
                {
                    Name = name,
                    Path = path,
                    Selected = true,
                    Icon = GetIcon(path)
                });

                OnBackupItemSelected(path);
            });
        }
    }

    private BitmapImage GetIcon(string path)
    {
        try
        {
            string iconPath = Directory.Exists(path) ? "folder_icon.png" : "file_icon.png";
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri($"pack://application:,,,/Assets/{iconPath}");
            bi.EndInit();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    private bool IsSubPath(string child, string parent)
    {
        var childUri = new Uri(Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) +
                               Path.DirectorySeparatorChar);
        var parentUri = new Uri(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) +
                                Path.DirectorySeparatorChar);

        return parentUri.IsBaseOf(childUri);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            AddCustomLocation(dialog.FolderName);
        }
    }

    private bool IsCoveredByAny(string path)
    {
        foreach (var item in PersonalFiles)
            if (IsSubPath(path, item.Path))
                return true;

        foreach (var item in AppData)
            if (IsSubPath(path, item.Path))
                return true;

        foreach (var item in CustomLocations)
            if (IsSubPath(path, item.Path))
                return true;

        return false;
    }

    private void AddCustomLocation(string path)
    {
        path = Path.GetFullPath(path);

        // 1. BLOCK if already covered anywhere
        if (IsCoveredByAny(path))
        {
            MessageBox.Show("This folder is already covered by an existing selection.");
            return;
        }

        // 2. Remove subpaths from CustomLocations
        for (int i = CustomLocations.Count - 1; i >= 0; i--)
        {
            if (IsSubPath(CustomLocations[i].Path, path))
            {
                CustomLocations.RemoveAt(i);
            }
        }

        // 3. Add
        CustomLocations.Add(new BackupItem
        {
            Name = System.IO.Path.GetFileName(path),
            Path = path,
            Selected = true,
            Icon = GetIcon(path)
        });
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
}

public class BackupItem
{
    public bool Selected { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public BitmapImage Icon { get; set; }
}