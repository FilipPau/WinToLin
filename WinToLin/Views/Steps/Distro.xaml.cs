using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Steps;

namespace WinToLin.Views.Steps;

public partial class Distro : UserControl
{
    private readonly ConfigManager _configManager;
    
    // The observable list that notifies the UI of additions or removals
    public ObservableCollection<LinuxDistro> DistroItems { get; set; }

    public Distro()
    {
        InitializeComponent();
        _configManager = ConfigManager.Instance;
        DistroItems = new ObservableCollection<LinuxDistro>();
        
        // Set DataContext so XAML can find DistroItems
        this.DataContext = this;

        LoadDistrosFromJson();
    }

    private void LoadDistrosFromJson()
    {
        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "distros.json");
        if (!File.Exists(jsonPath)) return;

        try
        {
            string json = File.ReadAllText(jsonPath);
            var distros = JsonSerializer.Deserialize<List<LinuxDistro>>(json);

            if (distros != null)
            {
                DistroItems.Clear();
                foreach (var distro in distros)
                {
                    DistroItems.Add(distro);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading distros: {ex.Message}");
        }
    }

    private void CardBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is LinuxDistro selectedDistro)
        {
            // Reset selection for all items in the observable list
            foreach (var item in DistroItems)
            {
                item.IsSelected = false;
            }

            // Select the clicked item
            selectedDistro.IsSelected = true;

            // Update global state
            _configManager.SetDistro(selectedDistro.Distro);
            StepManager.Instance.MainTaskCompleted();
        }
    }
}