using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;

namespace WinToLin.Steps
{
    public partial class USBSetup : UserControl
    {
        private Manager manager;

        private ManagementEventWatcher insertWatcher;
        private ManagementEventWatcher removeWatcher;

        public USBSetup()
        {
            InitializeComponent();

            manager = Manager.Instance;

            LoadUsbDevices();
            StartUsbMonitoring();

            UsbList.SelectionChanged += UsbList_SelectionChanged;
        }

        private void LoadUsbDevices()
        {
            var devices = new List<UsbDevice>();

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

                        
                        // Try to find drive letter

                        
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
                        // Ignore broken entries
                    }
                }
            }
            catch
            {
                // WMI failed (rare but possible)
            }
            
            
            UsbList.ItemsSource = devices;

            NoUsbText.Visibility = devices.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Maps DiskDrive → Partition → LogicalDisk (Drive Letter)
        /// </summary>
        private string GetDriveLetter(string deviceId)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, DriveType FROM Win32_LogicalDisk");

                foreach (ManagementObject disk in searcher.Get())
                {
                    var driveType = disk["DriveType"];

                    // 2 = removable drive (USB)
                    if (driveType == null || Convert.ToInt32(driveType) != 2)
                        continue;

                    string letter = disk["DeviceID"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(letter))
                        return letter; // e.g. "E:"
                }
            }
            catch { }

            return null;
        }
        
        private void StartUsbMonitoring()
        {
            try
            {
                // Detect ANY device change (more reliable than VolumeChangeEvent)
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
                // Ignore watcher failures
            }
        }

        private void UsbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsbList.SelectedItem is UsbDevice selected)
            {
                SelectedUsbText.Text =
                    $"Selected: {selected.Name} ({selected.Letter})";

            
                
                manager.SetUSB( selected.Letter,  selected.DeviceId );
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);

            if (Parent == null)
            {
                insertWatcher?.Stop();
                removeWatcher?.Stop();
            }
        }
    }

    public class UsbDevice
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Letter { get; set; }
        public string DeviceId { get; set; }
    }
}