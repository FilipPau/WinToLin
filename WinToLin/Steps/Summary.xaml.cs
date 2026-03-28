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
}