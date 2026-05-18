using System.Windows;
using System.Windows.Controls;
using WinToLin.Helper;
using WinToLin.Logic.Manager;
using WinToLin.Views.Windows;

namespace WinToLin.views;

public partial class SelectMode : Window
{

    private MainWindow _mainWindow;
    private OneStepWindow _oneClickWindow;
    
    public SelectMode()
    {
        _mainWindow = new MainWindow();
        _oneClickWindow = new OneStepWindow();
        
        InitializeComponent();

        Loaded += (sender, args) =>
        {
            ConfigManager.Instance.SetSystemSettings(
                SystemSettingsHelper.GetUserProfileName(),
                SystemSettingsHelper.GetLanguage(),
                SystemSettingsHelper.GetKeyboardLayout(),
                SystemSettingsHelper.GetTimeZone(),
                SystemSettingsHelper.GetCurrentWifiSSID(),
                SystemSettingsHelper.ExportWifiProfiles()
            );
        };
    }

    private void CustomTransferClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.MainWindow = _mainWindow;
        _mainWindow.Show();

        Close();
    }

    private void OneClickTransferClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.MainWindow = _oneClickWindow;
        _oneClickWindow.Show();

        Close();
    }
}