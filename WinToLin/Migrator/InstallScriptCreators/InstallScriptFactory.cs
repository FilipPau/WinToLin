using System.Diagnostics;
using WinToLin.Logic.Enums;

namespace WinToLin.Migrator.InstallScriptCreators;

public sealed class InstallScriptFactory
{
    public static IInstallScriptWriter CreateInstallScriptWriter(Distros  distro)
    {
        return distro switch
        {
            Distros.UBUNTU => new UbuntuInstallScriptCreator(),
            _ =>  throw new NotSupportedException($"The distro {distro} is not supported currently."),
        };
    }
}