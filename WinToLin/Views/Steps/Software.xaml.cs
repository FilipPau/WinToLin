using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Logic.Utils;
using WinToLin.Migrator.DistroIndependent;
using WinToLin.Migrator.DistroDependent.Utils;

namespace WinToLin.Views.Steps
{
    public partial class Software : UserControl
    {
        private readonly ConfigManager _configManager;
        private readonly AppCompatibilityUtil _compatibilityUtil = new();
        public ObservableCollection<SoftwareInfo> _displaySoftware { get; set; } = new();
        private List<SoftwareInfo> _allSoftwareCache = new();
        private Dictionary<string, List<AlternativeItemDto>> _alternativesDb = new(StringComparer.OrdinalIgnoreCase);
        private bool _hideRandomWinStuff = true;
        private bool _showOnlyCompatible = false;
        private bool _isLoaded = false;
        private bool _isUpdatingCollection = false;
        
        private SoftwareInfo _currentSelectedAppForAlternatives;

        public Software()
        {
            _configManager = ConfigManager.Instance;
            InitializeComponent();
            DataContext = this;
            Loaded += Software_Loaded;
            LoadAlternativesDatabase();
        }

        private void LoadAlternativesDatabase()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Packages", "Universal", "flatpak_alternatives.json");
                if (!File.Exists(jsonPath)) return;

                var container = JsonSerializer.Deserialize<AlternativesRootDto>(
                    File.ReadAllText(jsonPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (container?.Apps != null)
                {
                    foreach (var app in container.Apps)
                    {
                        if (string.IsNullOrWhiteSpace(app.Name)) continue;
                        var list = app.Alternatives ?? new List<AlternativeItemDto>();
                        _alternativesDb[app.Name.ToLower().Trim()] = list;

                        if (app.Aliases != null)
                        {
                            foreach (var alias in app.Aliases)
                            {
                                if (!string.IsNullOrWhiteSpace(alias))
                                    _alternativesDb[alias.ToLower().Trim()] = list;
                            }
                        }
                    }
                }
            }
            catch
            {
                _alternativesDb = new Dictionary<string, List<AlternativeItemDto>>(StringComparer.OrdinalIgnoreCase);
            }
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
            _isUpdatingCollection = true;
            _displaySoftware.Clear();
            _isUpdatingCollection = false;
            _allSoftwareCache.Clear();

            List<AppCompatibilityUtil.AppResult> scannedApps = null;
            await Task.Run(() =>
            {
                scannedApps = _compatibilityUtil.ScanInstalledAppsAndCheckCompatibility(_hideRandomWinStuff);
            });

            if (scannedApps != null)
            {
                foreach (var app in scannedApps)
                {
                    bool hasAlts = false;
                    if (app.Compatibility != null && app.Compatibility.Equals("Incompatible", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAlts = _alternativesDb.ContainsKey(app.Name.ToLower().Trim());
                    }

                    var info = new SoftwareInfo
                    {
                        Name = app.Name,
                        Icon = app.Icon,
                        CompatibilityType = app.Compatibility,
                        FlatpakId = app.FlatpakId,
                        Install = false,
                        HasAlternatives = hasAlts
                    };
                    _allSoftwareCache.Add(info);
                }
            }

            ApplyFiltersAndSearch();
            SearchBox.IsEnabled = true;
        }

        private void SoftwareList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCollection) return;

            // Only track items that are fully selectable (Native items)
            var removedTracked = e.RemovedItems.OfType<SoftwareInfo>().Where(x => !x.HasAlternatives && x.CompatibilityType == "Native");
            var addedTracked = e.AddedItems.OfType<SoftwareInfo>().Where(x => !x.HasAlternatives && x.CompatibilityType == "Native");

            foreach (var item in removedTracked)
            {
                item.Install = false;
                _configManager.RemoveSoftware((item.Name, item.FlatpakId ?? item.Name));
            }
            foreach (var item in addedTracked)
            {
                item.Install = true;
                _configManager.AddSoftware((item.Name, item.FlatpakId ?? item.Name));
            }

            // Remove any items from selection that are not allowed to be selected
            var badSelections = SoftwareList.SelectedItems.OfType<SoftwareInfo>()
                .Where(x => x.HasAlternatives ? !x.IsAlternativeSelected : x.CompatibilityType != "Native").ToList();
                
            if (badSelections.Count > 0)
            {
                _isUpdatingCollection = true;
                foreach (var bad in badSelections)
                {
                    SoftwareList.SelectedItems.Remove(bad);
                }
                _isUpdatingCollection = false;
            }
            
            int totalSelectable = SoftwareList.Items.OfType<SoftwareInfo>().Count(x => !x.HasAlternatives && x.CompatibilityType == "Native");
            int selectedSelectable = SoftwareList.SelectedItems.OfType<SoftwareInfo>().Count(x => !x.HasAlternatives && x.CompatibilityType == "Native");
            SelectAllToggle.IsChecked = selectedSelectable == totalSelectable && totalSelectable > 0;
            StepManager.Instance.MainTaskCompleted();
        }

        private void SelectAll_Clicked(object sender, RoutedEventArgs e)
        {
            if (SelectAllToggle.IsChecked == true)
            {
                _isUpdatingCollection = true;
                SoftwareList.SelectedItems.Clear();
                foreach (var item in SoftwareList.Items.OfType<SoftwareInfo>())
                {
                    // Only select valid active items (Native apps or configured alternative mappings)
                    if (item.CompatibilityType == "Native" || item.IsAlternativeSelected)
                    {
                        SoftwareList.SelectedItems.Add(item);
                        item.Install = true;

                        if (item.IsAlternativeSelected)
                            _configManager.AddSoftware((item.Name, item.SelectedAlternativeFlatpakId));
                        else
                            _configManager.AddSoftware((item.Name, item.FlatpakId ?? item.Name));
                    }
                }
                _isUpdatingCollection = false;
            }
            else
            {
                SoftwareList.UnselectAll();
            }
        }

        private async void HideComponents_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _hideRandomWinStuff = HideSystemCheckBox.IsChecked ?? false;
            await LoadSoftwareAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFiltersAndSearch();

        private void ShowOnlyCompatibleOnClick(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                _showOnlyCompatible = cb.IsChecked ?? false;
                ApplyFiltersAndSearch();
            }
        }

        private void ApplyFiltersAndSearch()
        {
            string query = SearchBox.Text.ToLower();
            var filtered = _allSoftwareCache.AsEnumerable();

            if (_showOnlyCompatible)
            {
                filtered = filtered.Where(s => s.CompatibilityType != null && s.CompatibilityType.Equals("Native", StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(s => s.Name != null && s.Name.ToLower().Contains(query));
            }

            var ordered = filtered.OrderByDescending(s => s.CompatibilityType != null && s.CompatibilityType.Equals("Native", StringComparison.OrdinalIgnoreCase))
                                  .ThenByDescending(s => s.HasAlternatives)
                                  .ThenBy(s => s.Name ?? string.Empty);

            _isUpdatingCollection = true;
            _displaySoftware.Clear();
            foreach (var item in ordered)
            {
                _displaySoftware.Add(item);
            }
            _isUpdatingCollection = false;
        }

        #region Alternatives Modal Interactivity

        private void OpenAlternativesModal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SoftwareInfo info)
            {
                _currentSelectedAppForAlternatives = info;
                ModalHeader.Text = $"Alternatives for {info.Name}";

                string lookupKey = info.Name.ToLower().Trim();
                if (_alternativesDb.TryGetValue(lookupKey, out var alts))
                {
                    ModalAlternativesList.ItemsSource = alts;
                    AlternativesModal.Visibility = Visibility.Visible;
                }
            }
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            AlternativesModal.Visibility = Visibility.Collapsed;
            _currentSelectedAppForAlternatives = null;
        }

        private void SelectAlternative_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedAppForAlternatives != null && ModalAlternativesList.SelectedItem is AlternativeItemDto selectedAlt)
            {
                if (_currentSelectedAppForAlternatives.IsAlternativeSelected)
                {
                    _configManager.RemoveSoftware((_currentSelectedAppForAlternatives.Name, _currentSelectedAppForAlternatives.SelectedAlternativeFlatpakId));
                }

                _currentSelectedAppForAlternatives.SelectedAlternativeName = selectedAlt.Name;
                _currentSelectedAppForAlternatives.SelectedAlternativeFlatpakId = selectedAlt.FlatpakId;
                _currentSelectedAppForAlternatives.IsAlternativeSelected = true;
                _currentSelectedAppForAlternatives.Install = true;

                _configManager.AddSoftware((_currentSelectedAppForAlternatives.Name, selectedAlt.FlatpakId));

                if (!SoftwareList.SelectedItems.Contains(_currentSelectedAppForAlternatives))
                {
                    _isUpdatingCollection = true;
                    SoftwareList.SelectedItems.Add(_currentSelectedAppForAlternatives);
                    _isUpdatingCollection = false;
                }

                CloseModal_Click(null, null);
            }
        }

        private void ClearAlternativeSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedAppForAlternatives != null)
            {
                if (_currentSelectedAppForAlternatives.IsAlternativeSelected)
                {
                    _configManager.RemoveSoftware((_currentSelectedAppForAlternatives.Name, _currentSelectedAppForAlternatives.SelectedAlternativeFlatpakId));
                }

                _currentSelectedAppForAlternatives.SelectedAlternativeName = null;
                _currentSelectedAppForAlternatives.SelectedAlternativeFlatpakId = null;
                _currentSelectedAppForAlternatives.IsAlternativeSelected = false;
                _currentSelectedAppForAlternatives.Install = false;

                if (SoftwareList.SelectedItems.Contains(_currentSelectedAppForAlternatives))
                {
                    _isUpdatingCollection = true;
                    SoftwareList.SelectedItems.Remove(_currentSelectedAppForAlternatives);
                    _isUpdatingCollection = false;
                }

                CloseModal_Click(null, null);
            }
        }

        #endregion

        #region DTO Mapping Models
        private class AlternativesRootDto
        {
            [JsonPropertyName("apps")] public List<AlternativeAppConfigDto> Apps { get; set; } = new();
        }
        private class AlternativeAppConfigDto
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = new();
            [JsonPropertyName("alternatives")] public List<AlternativeItemDto> Alternatives { get; set; } = new();
        }
        public class AlternativeItemDto
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("flatpak_id")] public string FlatpakId { get; set; }
        }
        #endregion

        public class SoftwareInfo : INotifyPropertyChanged 
        {
            public string Name { get; set; }
            public BitmapSource Icon { get; set; }
            public string CompatibilityType { get; set; }
            public string FlatpakId { get; set; }
            public bool HasAlternatives { get; set; }

            private string _selectedAlternativeName;
            public string SelectedAlternativeName
            {
                get => _selectedAlternativeName;
                set { _selectedAlternativeName = value; OnPropertyChanged(); }
            }

            private string _selectedAlternativeFlatpakId;
            public string SelectedAlternativeFlatpakId
            {
                get => _selectedAlternativeFlatpakId;
                set { _selectedAlternativeFlatpakId = value; OnPropertyChanged(); }
            }

            private bool _isAlternativeSelected;
            public bool IsAlternativeSelected
            {
                get => _isAlternativeSelected;
                set { _isAlternativeSelected = value; OnPropertyChanged(); }
            }

            private bool _install;
            public bool Install 
            { 
                get => _install; 
                set { _install = value; OnPropertyChanged(); } 
            }
            
            public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpper();

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}