using System.Windows;
using System.Windows.Controls;
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