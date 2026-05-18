using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Logic.Utils;

/*
 *  ToDo 
 *  appdata & roaming & locals transfer and mapping to apps
 *  multiple drives too
 */

namespace WinToLin.Views.Steps
{
    public partial class Backup : UserControl
    {
        private readonly ConfigManager _configManager;
        public ObservableCollection<BackupItem> AllBackupItems { get; set; } = new();
        public ICollectionView GroupedItems { get; set; }

        private bool _isInternalUpdate = false;

        public Backup()
        {
            InitializeComponent();
            _configManager = ConfigManager.Instance;
            
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
                BackupScannerUtil.ScanBackupLocations((category, name, physicalPath, linuxPath) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var existingPaths = AllBackupItems.Select(x => x.PhysicalPath);
                        if (BackupScannerUtil.IsPathAlreadyCovered(physicalPath, existingPaths)) return;

                        AllBackupItems.Add(new BackupItem {
                            Category = category,
                            Name = name, 
                            Path = physicalPath, 
                            PhysicalPath = physicalPath, 
                            LinuxPath = linuxPath,
                            Icon = GetIcon(physicalPath)
                        });
                        _configManager.AddBackupPath(physicalPath, linuxPath);
                    });
                });
            });
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
                item.LinuxPath = TargetPathInput.Text;
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string newPath = Path.GetFullPath(dialog.FolderName);
                var existingPaths = AllBackupItems.Select(x => x.PhysicalPath);
                
                if (BackupScannerUtil.IsPathAlreadyCovered(newPath, existingPaths)) return;

                var children = AllBackupItems.Where(x => BackupScannerUtil.IsSubPath(x.PhysicalPath, newPath)).ToList();
                foreach (var child in children) AllBackupItems.Remove(child);

                string linuxPath = BackupScannerUtil.MapToLinux(newPath, null);

                AllBackupItems.Add(new BackupItem {
                    Category = "Custom Locations",
                    Name = Path.GetFileName(newPath),
                    Path = newPath,
                    PhysicalPath = newPath,
                    LinuxPath = linuxPath,
                    Icon = GetIcon(newPath)
                });
                _configManager.AddBackupPath(newPath, linuxPath);
                UpdateTotalCount();
            }
        }

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
}