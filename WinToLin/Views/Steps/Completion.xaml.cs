using System.Windows.Controls;
using WinToLin.logic.manager;

namespace WinToLin.Views.Steps;

public partial class Completion : UserControl
{    private StepManager stepManager;

    public Completion(StepManager stepManager)
    {
        InitializeComponent();
        this.stepManager = stepManager;
    }
}