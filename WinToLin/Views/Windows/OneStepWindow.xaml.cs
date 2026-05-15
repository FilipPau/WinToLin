using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinToLin.logic.manager;
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

    private void OneStepWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StepManager.Instance.MainTaskCompleted();
    }

    private void UpdateMigrateButtonState()
    {
        // Button becomes clickable only if BOTH components have valid user input
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
                    // Catch iteration anomalies smoothly
                }
            }
        }
        catch
        {
            // Fail safely if local hardware access layers are restricted
        }
        
        UsbList.ItemsSource = devices;

        // Maintain selection reference if drive persists across hardware updates
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
        catch { }

        return null;
    }
    
    private void StartUsbMonitoring()
    {
        try
        {
            insertWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));

            insertWatcher.EventArrived += (s, e) =>
            {
                Dispatcher.Invoke(LoadUsbDevices);
            };

            insertWatcher.Start();

            removeWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3"));

            removeWatcher.EventArrived += (s, e) =>
            {
                Dispatcher.Invoke(LoadUsbDevices);
            };

            removeWatcher.Start();
        }
        catch
        {
            // Fail silently on watcher environment thread constraints
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
}