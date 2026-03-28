using System.Windows;
using System.Windows.Controls;

namespace WinToLin;

public partial class Welcome : UserControl
{
    private MainWindow mainWindow;
    
    public Welcome(MainWindow mainWindow)
    {
        InitializeComponent();
        
        this.mainWindow = mainWindow;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        mainWindow.NextStep();
    }
}