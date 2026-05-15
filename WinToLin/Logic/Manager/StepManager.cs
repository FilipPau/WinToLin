using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WinToLin.logic.manager.events;
using WinToLin.Steps;
using Backup = WinToLin.Views.Steps.Backup;
using Completion = WinToLin.Views.Steps.Completion;
using Distro = WinToLin.Views.Steps.Distro;
using Software = WinToLin.Views.Steps.Software;
using Summary = WinToLin.Views.Steps.Summary;
using USBSetup = WinToLin.Views.Steps.USBSetup;

namespace WinToLin.logic.manager;

public class StepManager
{
    private static StepManager _instance;
    private static readonly object _lock = new object();

    public ObservableCollection<StepState> Steps { get; }
    private int _currentStep = 0;

    public EventHandler<NextPageEventArgs> OnNextPage;
    public EventHandler<HideBackButtonEventArgs> OnHideBackButton;
    public EventHandler<AllowNextStepArgs> OnAllowNextStep;

    public StepState CurrentStep => Steps[_currentStep];

    public static StepManager Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance ??= new StepManager();
            }
        }
    }

    private StepManager()
    {
        // By initializing these once here, the state (text, checkboxes, lists) 
        // stays in memory as long as the StepManager lives.
        Steps = new ObservableCollection<StepState>
        {
            new()
            {
                Name = "Distro", Content = new Distro(), Status = StepStatus.Current, IsTaskDone = false,
                StepTaskDescription = "Select a Distro"
            },
            new()
            {
                Name = "Software", Content = new Software(),
                StepTaskDescription = "Select the software you want to take with you"
            },
            new()
            {
                Name = "Backup", Content = new Backup(),
                StepTaskDescription = "Select the files you want to take with you"
            },
            new()
            {
                Name = "USB Setup", Content = new USBSetup(), StepTaskDescription = "Select the USB drive to be flashed"
            },
            new() { Name = "Summary", Content = new Summary(), StepTaskDescription = "An overview of all your selections" },
            new() { Name = "Migration", Content = new Views.Steps.Migration(), StepTaskDescription = "" },
            new() { Name = "Completion", Content = new Completion(this), StepTaskDescription = "" }
        };
    }

    public UserControl FirstStepPanel => Steps[0].Content;

    public void MainTaskCompleted()
    {
        Steps[_currentStep].IsTaskDone = true;
        OnAllowNextStep?.Invoke(this, new AllowNextStepArgs());
    }

    public void NextStep()
    {
        // Mark current as completed
        Steps[_currentStep].Status = StepStatus.Completed;

        if (_currentStep + 1 >= Steps.Count)
        {
            Application.Current.Shutdown();
            return;
        }

        _currentStep++;

        // Prepare the next step
        var nextStep = Steps[_currentStep];
        nextStep.Status = StepStatus.Current;   

        // Determine button text (Last page usually says "Finish" or "Close")
        string buttonText = (_currentStep == Steps.Count - 1) ? "Close" : "Next";

        // IMPORTANT: We pass the EXACT same UserControl instance stored in 'Content'
        OnNextPage?.Invoke(this, new NextPageEventArgs(nextStep.Content, buttonText));
    }

    public void LastStep()
    {
        if (_currentStep <= 0) return;

        // Revert status
        Steps[_currentStep].Status = StepStatus.Upcoming;
        _currentStep--;

        var previousStep = Steps[_currentStep];
        previousStep.Status = StepStatus.Current;

        // When going back, we always show "Next" unless it's the first page logic
        OnNextPage?.Invoke(this, new NextPageEventArgs(previousStep.Content, "Next"));

        if (_currentStep == 0 || _currentStep == 5)
            OnHideBackButton?.Invoke(this, new HideBackButtonEventArgs());

        // Since the task was likely already done when we were here before,
        // we re-trigger the AllowNextStep if the state was finished.
        OnAllowNextStep?.Invoke(this, new AllowNextStepArgs());
    }
}