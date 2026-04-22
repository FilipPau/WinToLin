using System.Windows;
using System.Windows.Controls;

namespace WinToLin.Steps;

public partial class Summary : UserControl
{
    private Manager manager;

    public Summary()
    {
        InitializeComponent();
        
        manager = Manager.Instance;
    }

    private void Summary_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSummary();
    }
    
    private void LoadSummary()
    {
        // Distro
        DistroText.Text = string.IsNullOrEmpty(manager.DistroName)
            ? "Not selected"
            : manager.DistroName;

        // USB
        UsbText.Text = string.IsNullOrEmpty(manager.InstalationUSBLetter)
            ? "No USB selected"
            : manager.InstalationUSBLetter;

        // Backup
        BackupList.ItemsSource = manager.BackupPaths.Count > 0
            ? manager.BackupPaths
            : new[] { "No backup paths selected" };

        // Software
        SoftwareList.ItemsSource = manager.SoftwareNames.Count > 0
            ? manager.SoftwareNames
            : new[] { "No software selected" };

        // Hardware
        GpuList.ItemsSource = manager.GPUNames.Count > 0
            ? manager.GPUNames
            : new[] { "No GPU detected" };

        NicList.ItemsSource = manager.NICNames.Count > 0
            ? manager.NICNames
            : new[] { "No network devices detected" };

        DriveList.ItemsSource = manager.Drives.Count > 0
            ? manager.Drives
            : new[] { "No drives detected" };
    }
}