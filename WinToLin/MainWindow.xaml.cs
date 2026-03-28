using System.Diagnostics.SymbolStore;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using WinToLin.Steps;

namespace WinToLin;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<(String name, UserControl content)> steps = new ();

    private int CurrentStep = 0;


    public MainWindow()
    {
        InitializeComponent();


        steps = [
            ("Welcome", new Welcome(this)),
            ("Hardware", new Hardware()),
            ("Software", new Software()),
            ("Backup", new Backup()),
            ("Distro", new Distro()),
            ("USB Setup", new USBSetup()),
            ("Summary", new Summary()),
            ("Migration", new Migration()),
            ("Completion", new Completion(this)),
        ];
        
        //eine page hinzufügen für das mapping von dingen wie folders und so
        
        for (int i = 0; i < steps.Count; i++)
        {
            StepsListGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new Label { Content = steps[i].name };
            
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 0, 0);
            
            Grid.SetColumn(label, i);
            
            StepsListGrid.Children.Add(label);
        }


        this.Closing += MainWindow_Closing;
        
        SetStep(steps[0].content);
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var manager = Manager.Instance;

        string json = JsonSerializer.Serialize(manager, new JsonSerializerOptions { WriteIndented = true });

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manager_data.json");
        File.WriteAllText(filePath, json);

        //MessageBox.Show($"Manager data saved to:\n{filePath}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    
    
    
    
    public void NextStep()
    {
        if (CurrentStep+1 >= steps.Count)
        {
            return;
        }
        
        CurrentStep++;
        SetStep(steps[CurrentStep].content);
    }

    public void LastStep()
    {
        CurrentStep--;
        SetStep(steps[CurrentStep].content);
    }
    
    private void SetStep(UserControl content)
    {

        
        if (CurrentStep != 0)
        {
            BackButton.Visibility = Visibility.Visible;
        }
        else
        {
            BackButton.Visibility = Visibility.Hidden;
        }
        
        MainContent.Content = content;

        Label label = StepsListGrid.Children.OfType<Label>().FirstOrDefault(l => Grid.GetColumn(l) == CurrentStep)!;
        
        label.Content = "✓ " + steps[CurrentStep].name;
        
        
    }

    private void GoNext_OnClick(object sender, RoutedEventArgs e)
    {
        NextStep();
    }

    private void GoBack_OnClick(object sender, RoutedEventArgs e)
    {
        LastStep();
    }
}