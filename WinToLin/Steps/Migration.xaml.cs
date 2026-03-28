using System.Windows.Controls;

namespace WinToLin.Steps;

public partial class Migration : UserControl
{    
    private Manager manager;

    public Migration()
    {
        InitializeComponent();
        
        manager = Manager.Instance;
    }
}