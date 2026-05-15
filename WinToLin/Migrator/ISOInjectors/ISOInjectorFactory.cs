using WinToLin.Logic.Enums;

namespace WinToLin.Migrator.ISOInjectors;

public sealed class ISOInjectorFactory
{
    public static IISOInjector CreateISOInjector(Distros distro)
    {
        return distro switch
        {
            Distros.UBUNTU => new UbuntuISOInjector()
        };
    }

}