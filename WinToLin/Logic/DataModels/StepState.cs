using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace WinToLin.logic.manager;

public enum StepStatus { Upcoming, Current, Completed }

public class StepState : INotifyPropertyChanged
{
    public string Name { get; set; }
    public UserControl Content { get; set; }

    public string StepTaskDescription { get; set; }
    
    private StepStatus _status = StepStatus.Upcoming;
    public StepStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    private bool _isTaskDone;
    public bool IsTaskDone
    {
        get => _isTaskDone;
        set { _isTaskDone = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}