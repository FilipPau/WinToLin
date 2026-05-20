using WinToLin.Logic.Enums;
using WinToLin.Migrator.BootloaderConfigUpdater;
using WinToLin.Migrator.InstallScriptCreators;
using WinToLin.Migrator.ISOBootloaderConfigExtractor;
using WinToLin.Migrator.ISOInjectors;

namespace WinToLin.Migrator.ToolKit;

public static class TransferToolKitFactory
{
    public record ToolKit(
        ISOBootloaderConfigExtractor.IISOBootLoaderExtractor BootLoaderConfigExtractor,
        IBootLoaderConfigUpdater BootLoaderConfigUpdater,
        IInstallScriptWriter InstallScriptWriter,
        IISOInjector IsoInjector
    );

    static Dictionary<Distros, ToolKit> DistroToToolKit = new()
    {
        [Distros.UBUNTU] = new
        (
            new UbuntuGrubIsoExtractConfig(),
            new UbuntuGrubBootLoaderUpdater(),
            new UbuntuInstallScriptCreator(),
            new UbuntuISOInjector()
        ),
        [Distros.FEDORA] = new
        (
            new FedoraGrubIsoExtractConfig(),
            new FedoraGrubBootLoaderUpdater(),
            new FedoraInstallScriptCreator(),
            new FedoraISOInjector()
        ),
    };


    public static ToolKit CreateTransferToolKit(Distros distro)
    {
        return DistroToToolKit[distro];
    }
}