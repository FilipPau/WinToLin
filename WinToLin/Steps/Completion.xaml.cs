using System.Windows.Controls;

namespace WinToLin.Steps;

public partial class Completion : UserControl
{    private MainWindow mainWindow;

    public Completion(MainWindow mainWindow)
    {
        InitializeComponent();
        this.mainWindow = mainWindow;
    }
}