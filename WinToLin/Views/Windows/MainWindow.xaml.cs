using System;
using System.Windows;
using System.Windows.Controls;
using WinToLin.Helper;
using WinToLin.logic.manager;

namespace WinToLin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var stepManager = StepManager.Instance;

        TaskText.Text = stepManager.CurrentStep.StepTaskDescription;

        // This is the only thing needed to connect the data
        this.DataContext = stepManager;

        stepManager.OnNextPage += (sender, args) =>
        {
            MainContent.Content = args.NextPage;
            GoNextButton.IsEnabled = false; // Reset for next step
            BackButton.Visibility = Visibility.Visible;
            // Add Logic for "Close" text if needed: GoNextButton.Content = args.ButtonText;

            TaskText.Text = stepManager.CurrentStep.StepTaskDescription;
        };

        stepManager.OnHideBackButton += (sender, args) => BackButton.Visibility = Visibility.Hidden;
        stepManager.OnAllowNextStep += (sender, args) => GoNextButton.IsEnabled = true;

        BackButton.Visibility = Visibility.Hidden;
        MainContent.Content = stepManager.FirstStepPanel;
        
        Closing += (sender, args) =>
        {
            Application.Current.Shutdown();
        };
    }

    private void GoNext_OnClick(object sender, RoutedEventArgs e) => StepManager.Instance.NextStep();
    private void GoBack_OnClick(object sender, RoutedEventArgs e) => StepManager.Instance.LastStep();
}