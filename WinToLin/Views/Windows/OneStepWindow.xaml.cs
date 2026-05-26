using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices; // Added for Win32 P/Invokes
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop; // Added for HwndSource
using Microsoft.Win32;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Logic.Utils;
using WinToLin.Migrator.DistroIndependent; // Ensure your ConfigManager namespace is accessible
using WinToLin.Steps;
using WinToLin.Views.Steps;

namespace WinToLin.Views.Windows;

public partial class OneStepWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigManager _configManager;
    private ManagementEventWatcher insertWatcher;
    private ManagementEventWatcher removeWatcher;

    private bool _isDistroSelected = false;
    private bool _isUsbSelected = false;
    private bool _isRunning = false;

    // Visual list collection tracking actual items defined in your base Migrator class pipeline
    private List<StepItemVisual> _steps = new List<StepItemVisual>();

    public ObservableCollection<LinuxDistro> DistroItems { get; set; }

    private ObservableCollection<LinuxDistro> _filteredDistroItems;

    public ObservableCollection<LinuxDistro> FilteredDistroItems
    {
        get => _filteredDistroItems;
        set
        {
            _filteredDistroItems = value;
            OnPropertyChanged();
        }
    }

    #region Win32 System Menu Interop Constants & Imports

    private const int WM_SYSCOMMAND = 0x0112;
    private const int MF_STRING = 0x00000000;
    private const int MF_SEPARATOR = 0x00000800;
    
    // Unique ID for your custom context menu command. 
    // Must be lower than 0xF000 (System commands use 0xF000 and above).
    private const int MENU_PRINT_JSON_ID = 0x1001; 

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    #endregion

    public OneStepWindow()
    {
        InitializeComponent();

        _configManager = ConfigManager.Instance;

        DistroItems = new ObservableCollection<LinuxDistro>();
        FilteredDistroItems = new ObservableCollection<LinuxDistro>();

        LoadDistrosFromJson();
        LoadUsbDevices();
        StartUsbMonitoring();

        UsbList.SelectionChanged += UsbList_SelectionChanged;
        Loaded += OneStepWindow_Loaded;
    }

    private async void OneStepWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeStepVisuals();
        SetupTitleBarContextMenu();
        
        // Load files and apps, and automatically pre-select compatible apps
        await AutoSelectCompatibleAppsAsync();
        
        // Load and automatically add default user directories to target backup config
        await AutoSelectDefaultBackupFoldersAsync();
    }

    private void InitializeStepVisuals()
    {
        // Strictly matched to the exact Invoke indices (0-3) found inside Migrator.cs
        _steps = new List<StepItemVisual>
        {
            new StepItemVisual { Name = "Downloading ISO & Compressing Files" }, // Step 0
            new StepItemVisual { Name = "Extracting Bootloader Config" },          // Step 1
            new StepItemVisual { Name = "Injecting Config & Migration Scripts" },  // Step 2
            new StepItemVisual { Name = "Creating Final Bootable ISO" }          // Step 3
        };
        
        StepsControl.ItemsSource = _steps;
    }

    private void SetupTitleBarContextMenu()
    {
        // Get the window handle (HWND) for this WPF window
        WindowInteropHelper helper = new WindowInteropHelper(this);
        IntPtr hWnd = helper.Handle;

        if (hWnd != IntPtr.Zero)
        {
            // Retrieve a handle to the system window context menu (Title bar right-click menu)
            IntPtr sysMenu = GetSystemMenu(hWnd, false);

            if (sysMenu != IntPtr.Zero)
            {
                // Add a visual separator line, then append our custom action button
                AppendMenu(sysMenu, MF_SEPARATOR, 0, string.Empty);
                AppendMenu(sysMenu, MF_STRING, MENU_PRINT_JSON_ID, "Print Config JSON");

                // Hook into the native WndProc message evaluation loop
                HwndSource source = HwndSource.FromHwnd(hWnd);
                source?.AddHook(WndProc);
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercept System Commands sent from the Title Bar context menu
        if (msg == WM_SYSCOMMAND)
        {
            if (wParam.ToInt32() == MENU_PRINT_JSON_ID)
            {
                // Execute the print logic inside ConfigManager
                ConfigManager.OutputFileLocation = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\out.json";
                _configManager.GetConfigJson();
                
                // Mark message handled so OS drops it
                handled = true; 
            }
        }
        return IntPtr.Zero;
    }

    private async Task AutoSelectCompatibleAppsAsync()
    {
        try
        {
            var progress = new Progress<Software.SoftwareInfo>(software =>
            {
                // Updated to support the new tuple tracking configuration interface schema
                string targetPackage = !string.IsNullOrEmpty(software.FlatpakId) ? software.FlatpakId : software.Name;
                _configManager.AddSoftware((software.Name, targetPackage));
            });

            await Task.Run(() => SoftwareScannerUtil.PerformBackgroundScan(progress, hideRandomWinStuff: false));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during background app pre-selection: {ex.Message}");
        }
    }

    private async Task AutoSelectDefaultBackupFoldersAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                BackupScannerUtil.ScanBackupLocations((category, name, physicalPath, linuxPath) =>
                {
                    // Directly assign configurations into global state tracking on background loop threads safely
                    _configManager.AddBackupPath(physicalPath, linuxPath);
                });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during background folder pre-selection: {ex.Message}");
        }
    }

    private void RefreshSteps()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(_steps);
        view?.Refresh();
    }

    private void UpdateMigrateButtonState()
    {
        if (_isRunning)
        {
            CreateIsoButton.IsEnabled = false;
            MigrateButton.IsEnabled = false;
            return;
        }

        CreateIsoButton.IsEnabled = _isDistroSelected;
        MigrateButton.IsEnabled = _isDistroSelected && _isUsbSelected;
    }

    #region Distribution Handling Logic

    private void LoadDistrosFromJson()
    {
        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "distros.json");
        if (!File.Exists(jsonPath)) return;

        try
        {
            string json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var distros = JsonSerializer.Deserialize<List<LinuxDistro>>(json, options);

            if (distros != null)
            {
                DistroItems.Clear();
                var tempFiltered = new ObservableCollection<LinuxDistro>();

                foreach (var distro in distros)
                {
                    DistroItems.Add(distro);
                    tempFiltered.Add(distro);
                }

                FilteredDistroItems = tempFiltered;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading distros: {ex.Message}");
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox == null || DistroItems == null) return;

        string filter = SearchBox.Text.Trim();
        var tempFiltered = new ObservableCollection<LinuxDistro>();

        foreach (var item in DistroItems)
        {
            if (string.IsNullOrEmpty(filter) || item.RawName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                tempFiltered.Add(item);
            }
        }

        FilteredDistroItems = tempFiltered;
    }

    private void CardBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is LinuxDistro selectedDistro)
        {
            foreach (var item in DistroItems)
            {
                item.IsSelected = false;
            }

            selectedDistro.IsSelected = true;
            _isDistroSelected = true;

            _configManager.SetDistro(selectedDistro.Distro);
            StepManager.Instance.MainTaskCompleted();

            UpdateMigrateButtonState();
        }
    }

    #endregion

    #region USB Drive Detection Logic

    private void LoadUsbDevices()
    {
        var devices = new List<UsbDevice>();
        string currentlySelectedId = (UsbList.SelectedItem as UsbDevice)?.DeviceId;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

            foreach (ManagementObject drive in searcher.Get())
            {
                try
                {
                    string deviceId = drive["DeviceID"]?.ToString();
                    string model = drive["Model"]?.ToString() ?? "USB Drive";

                    ulong sizeBytes = drive["Size"] != null ? (ulong)drive["Size"] : 0;
                    string size = sizeBytes > 0
                        ? $"{sizeBytes / (1024 * 1024 * 1024)} GB"
                        : "Unknown";

                    string letter = GetDriveLetter(deviceId);

                    devices.Add(new UsbDevice
                    {
                        Name = model,
                        Size = size,
                        Letter = letter ?? "No letter",
                        DeviceId = deviceId
                    });
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        UsbList.ItemsSource = devices;

        if (!string.IsNullOrEmpty(currentlySelectedId))
        {
            var matchedDevice = devices.FirstOrDefault(d => d.DeviceId == currentlySelectedId);
            if (matchedDevice != null)
            {
                UsbList.SelectedItem = matchedDevice;
            }
            else
            {
                _isUsbSelected = false;
                SelectedUsbText.Text = string.Empty;
            }
        }
        else
        {
            _isUsbSelected = false;
            SelectedUsbText.Text = string.Empty;
        }

        NoUsbText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateMigrateButtonState();
    }

    private string GetDriveLetter(string deviceId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, DriveType FROM Win32_LogicalDisk");

            foreach (ManagementObject disk in searcher.Get())
            {
                var driveType = disk["DriveType"];

                if (driveType == null || Convert.ToInt32(driveType) != 2)
                    continue;

                string letter = disk["DeviceID"]?.ToString();

                if (!string.IsNullOrWhiteSpace(letter))
                    return letter;
            }
        }
        catch
        {
        }

        return null;
    }

    private void StartUsbMonitoring()
    {
        try
        {
            insertWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));

            insertWatcher.EventArrived += (s, e) => { Dispatcher.Invoke(LoadUsbDevices); };

            insertWatcher.Start();

            removeWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3"));

            removeWatcher.EventArrived += (s, e) => { Dispatcher.Invoke(LoadUsbDevices); };

            removeWatcher.Start();
        }
        catch
        {
        }
    }

    private void UsbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UsbList.SelectedItem is UsbDevice selected)
        {
            SelectedUsbText.Text = $"Selected: {selected.Name} ({selected.Letter})";
            _isUsbSelected = true;
            _configManager.SetUSB(selected.Letter, selected.DeviceId);
        }
        else
        {
            _isUsbSelected = false;
        }

        UpdateMigrateButtonState();
    }

    protected override void OnClosed(EventArgs e)
    {
        insertWatcher?.Stop();
        insertWatcher?.Dispose();
        removeWatcher?.Stop();
        removeWatcher?.Dispose();

        base.OnClosed(e);
    }

    #endregion

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void CreateIsoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        OpenFolderDialog folderDialog = new OpenFolderDialog
        {
            Title = "Select Destination Directory",
            InitialDirectory = Environment.CurrentDirectory
        };

        if (folderDialog.ShowDialog() == true)
        {
            string selectedDir = folderDialog.FolderName;
            
            string workDir = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build");

            _isRunning = true;
            UpdateMigrateButtonState();
            ExecutionStatusPanel.Visibility = Visibility.Visible;

            var migrator = Migrator.Migrator.Instance;

            // --- Wire up UI Events exactly as specified by base ---
            migrator.OnProgressChanged += p => Dispatcher.Invoke((Action)(() => MainProgress.Value = p));
            migrator.OnStatusChanged += s => Dispatcher.Invoke((Action)(() => ProgressText.Text = s));
            migrator.OnStepChanged += idx => Dispatcher.Invoke(() => {
                for (int i = 0; i < _steps.Count; i++) {
                    _steps[i].IsActive = (i == idx);
                    _steps[i].IsDone = (i < idx);
                }
                RefreshSteps();
            });

            try
            {
                await migrator.RunPipeline(workDir, true, selectedDir);
                StepManager.Instance.MainTaskCompleted();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Migration failed: {ex.Message}", "Error", MessageBoxButton.OK);
            }
            finally
            {
                _isRunning = false;
                UpdateMigrateButtonState();
            }
        }
    }

    private async void MigrateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        string workDir = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build");

        _isRunning = true;
        UpdateMigrateButtonState();
        ExecutionStatusPanel.Visibility = Visibility.Visible;

        var migrator = Migrator.Migrator.Instance;

        // --- Wire up UI Events exactly as specified by base ---
        migrator.OnProgressChanged += p => Dispatcher.Invoke((Action)(() => MainProgress.Value = p));
        migrator.OnStatusChanged += s => Dispatcher.Invoke((Action)(() => ProgressText.Text = s));
        migrator.OnStepChanged += idx => Dispatcher.Invoke(() => {
            for (int i = 0; i < _steps.Count; i++) {
                _steps[i].IsActive = (i == idx);
                _steps[i].IsDone = (i < idx);
            }
            RefreshSteps();
        });

        try
        {
            await migrator.RunPipeline(workDir, false);
            StepManager.Instance.MainTaskCompleted();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Migration Error: {ex.Message}", "Error", MessageBoxButton.OK);
        }
        finally
        {
            _isRunning = false;
            UpdateMigrateButtonState();
        }
    }
}

public class StepItemVisual : INotifyPropertyChanged
{
    private bool _isActive;
    private bool _isDone;

    public string Name { get; set; }

    public bool IsActive 
    { 
        get => _isActive; 
        set { _isActive = value; OnPropertyChanged(); } 
    }
    
    public bool IsDone 
    { 
        get => _isDone; 
        set { _isDone = value; OnPropertyChanged(); } 
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string p = null) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}