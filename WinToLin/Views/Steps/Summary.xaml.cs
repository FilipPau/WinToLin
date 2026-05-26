using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;

namespace WinToLin.Views.Steps;

public partial class Summary : UserControl
{
    private ConfigManager _configManager;

    public Summary()
    {
        InitializeComponent();
        _configManager = ConfigManager.Instance;
        Loaded += Summary_Loaded;
    }

    private void Summary_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSummary();
        StepManager.Instance.MainTaskCompleted();
    }

    private void LoadSummary()
    {
        // OS & USB
        DistroText.Text = _configManager.DistroName.ToString().ToLower();
        UsbText.Text = string.IsNullOrEmpty(_configManager.InstalationUSBLetter) ? "No USB" : $"Drive {_configManager.InstalationUSBLetter}";

        // Software Visibility Logic
        if (_configManager.SoftwareNames != null && _configManager.SoftwareNames.Count > 0)
        {
            NoAppsText.Visibility = Visibility.Collapsed;
            SoftwarePreview.Visibility = Visibility.Visible;
            SoftwareViewBtn.Visibility = Visibility.Visible;
            SoftwarePreview.ItemsSource = _configManager.SoftwareNames.Select(x => x.name).Take(8).ToList();
        }
        else
        {
            NoAppsText.Visibility = Visibility.Visible;
            SoftwarePreview.Visibility = Visibility.Collapsed;
            SoftwareViewBtn.Visibility = Visibility.Collapsed; // Hide "View All" if there's nothing to view
        }

        // Backup Visibility Logic
        if (_configManager.BackupPaths != null && _configManager.BackupPaths.Count > 0)
        {
            NoBackupText.Visibility = Visibility.Collapsed;
            BackupPreview.Visibility = Visibility.Visible;
            BackupViewBtn.Visibility = Visibility.Visible;
            BackupPreview.ItemsSource = _configManager.BackupPaths.Take(3).Select(x => $" {x.Key} ->  {x.Value}");
        }
        else
        {
            NoBackupText.Visibility = Visibility.Visible;
            BackupPreview.Visibility = Visibility.Collapsed;
            BackupViewBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowSoftwareModal(object sender, RoutedEventArgs e)
    {
        ModalTitle.Text = "Apps to Install";
        ModalList.ItemsSource = _configManager.SoftwareNames.Select(x => x.name).ToList();
        ModalOverlay.Visibility = Visibility.Visible;
    }

    private void ShowBackupModal(object sender, RoutedEventArgs e)
    {
        ModalTitle.Text = "Migration Paths";
        ModalList.ItemsSource = _configManager.BackupPaths.Select(x => $"{x.Key} ->  {x.Value}");
        ModalOverlay.Visibility = Visibility.Visible;
    }

    private void CloseModal(object sender, RoutedEventArgs e) => ModalOverlay.Visibility = Visibility.Collapsed;

    private void Dimmer_MouseDown(object sender, MouseButtonEventArgs e) => ModalOverlay.Visibility = Visibility.Collapsed;
}