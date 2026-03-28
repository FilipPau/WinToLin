using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinToLin.Steps;

public partial class Distro : UserControl
{
    private Manager manager;
    private Border selectedCard = null;

    public class LinuxDistro
    {
        public string Name { get; set; }
        public string Base { get; set; }
        public string Description { get; set; }
        public string UseCase { get; set; }
        public string DownloadUrl { get; set; }
        public string ImagePath { get; set; }
    }

    public Distro()
    {
        InitializeComponent();
        manager = Manager.Instance;
        LoadDistrosFromJson();
    }

    private void LoadDistrosFromJson()
    {
        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "distros.json");
        if (!File.Exists(jsonPath))
        {
            MessageBox.Show("distros.json not found!");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var distros = System.Text.Json.JsonSerializer.Deserialize<List<LinuxDistro>>(json);

        if (distros != null)
            DistroList.ItemsSource = distros;
    }

    // Card click handler
    private void CardBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border card) return;

        // Un-highlight previous
        if (selectedCard != null)
            selectedCard.BorderBrush = Brushes.Black;

        // Highlight new
        card.BorderBrush = Brushes.CornflowerBlue;
        selectedCard = card;

        // Get DataContext
        if (card.DataContext is LinuxDistro distro)
        {
            OnDistroSelected(distro);
        }
    }

    // Function called when a distro is clicked
    private void OnDistroSelected(LinuxDistro distro)
    {
        manager.SetDistro(distro.Name);
    }
}