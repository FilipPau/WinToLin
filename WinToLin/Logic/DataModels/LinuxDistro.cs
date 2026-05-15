using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using WinToLin.Logic.Enums;

namespace WinToLin.Steps;

public class LinuxDistro : INotifyPropertyChanged
{
    private static readonly Dictionary<string, Distros> EnumMapping = new(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Ubuntu", Distros.UBUNTU },
        { "Fedora", Distros.FEDORA },
        { "Arch Linux", Distros.ARCH },
        { "Linux Mint", Distros.MINT }
    };

    // 1. Capture the raw string value from the JSON "Name" key safely
    private string _rawName;
    [JsonPropertyName("Name")]
    public string RawName
    {
        get => _rawName;
        set
        {
            _rawName = value;
            // Automatically assign the correct Enum value based on the text string string
            if (!string.IsNullOrEmpty(value) && EnumMapping.TryGetValue(value, out var matchedEnum))
            {
                Distro = matchedEnum;
            }
            OnPropertyChanged();
        }
    }

    // 2. The Enum representation for application internal tracking logic
    public Distros Distro { get; set; }
    
    public string Base { get; set; }
    public string Description { get; set; }
    public string UseCase { get; set; }
    public string DownloadUrl { get; set; }
    public string ImagePath { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}