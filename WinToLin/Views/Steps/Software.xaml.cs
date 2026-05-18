using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Logic.Utils;

namespace WinToLin.Views.Steps
{
    public partial class Software : UserControl
    {
        private readonly ConfigManager _configManager;
        public ObservableCollection<SoftwareInfo> _displaySoftware { get; set; } = new();
        private List<SoftwareInfo> _allSoftwareCache = new();
        private bool _hideRandomWinStuff = true;
        private bool _isLoaded = false;

        public Software()
        {
            _configManager = ConfigManager.Instance;
            InitializeComponent();
            DataContext = this;
            Loaded += Software_Loaded;
        }

        private async void Software_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            await LoadSoftwareAsync();
            _isLoaded = true;
            StepManager.Instance.MainTaskCompleted();
        }

        private async Task LoadSoftwareAsync()
        {
            SearchBox.IsEnabled = false;
            _displaySoftware.Clear();
            _allSoftwareCache.Clear();

            var progress = new Progress<SoftwareInfo>(software =>
            {
                _displaySoftware.Add(software);
                _allSoftwareCache.Add(software);
            });

            // Offload scanning parameters directly to the static class
            await Task.Run(() => SoftwareScannerUtil.PerformBackgroundScan(progress, _hideRandomWinStuff));
            SearchBox.IsEnabled = true;
        }

        private void SoftwareList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in e.RemovedItems.OfType<SoftwareInfo>())
            {
                item.Install = false;
                _configManager.RemoveSoftware(item.Name);
            }
            foreach (var item in e.AddedItems.OfType<SoftwareInfo>())
            {
                item.Install = true;
                _configManager.AddSoftware(item.Name);
            }
            SelectAllToggle.IsChecked = SoftwareList.SelectedItems.Count == SoftwareList.Items.Count && SoftwareList.Items.Count > 0;
            StepManager.Instance.MainTaskCompleted();
        }

        private void SelectAll_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectAllToggle.IsChecked == true) SoftwareList.SelectAll(); 
            else SoftwareList.UnselectAll();
        }

        private async void HideComponents_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _hideRandomWinStuff = HideSystemCheckBox.IsChecked ?? false;
            await LoadSoftwareAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();
            var filtered = _allSoftwareCache.Where(s => s.Name.ToLower().Contains(query)).ToList();
            _displaySoftware.Clear();
            foreach (var item in filtered) _displaySoftware.Add(item);
        }

        public class SoftwareInfo : INotifyPropertyChanged 
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public BitmapSource Icon { get; set; }
            public bool HasLinuxNative { get; set; }
            public bool HasLinuxAlternative { get; set; }
            public bool HasAntiCheat { get; set; }
            public int SortOrder { get; set; } 
            public List<AlternativeInfo> Alternatives { get; set; } = new();
            private bool _install;
            public bool Install { get => _install; set { _install = value; OnPropertyChanged(); } }
            
            public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpper();
            public string Compatibility => HasAntiCheat ? "Anti-Cheat" : (HasLinuxNative ? "Native" : "Unknown");

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class AlternativeInfo { public string Name { get; set; } }
    }
}