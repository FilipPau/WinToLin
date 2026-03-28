using System.Windows.Controls;

namespace WinToLin.Steps;

public partial class USBSetup : UserControl
{    
    private Manager manager;

    public USBSetup()
    {
        InitializeComponent();
        
        manager = Manager.Instance;

    }
}