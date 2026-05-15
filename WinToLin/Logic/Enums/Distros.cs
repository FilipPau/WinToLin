using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace WinToLin.Logic.Enums
{
    public enum Distros
    {
        [EnumMember(Value = "Ubuntu")]
        UBUNTU,
        
        [EnumMember(Value = "Fedora")]
        FEDORA,
        
        [EnumMember(Value = "Arch Linux")]
        ARCH,
        
        [EnumMember(Value = "Linux Mint")]
        MINT
    }
}
