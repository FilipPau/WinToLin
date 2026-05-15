namespace WinToLin.Migrator.BootloaderConfigUpdater;

public sealed class BootLoaderConfigUpdaterFactory
{
    public static IBootLoaderConfigUpdater CreateBootLoaderConfigUpdater(BootLoaders bootLoader)
    {
        return bootLoader switch
        {
            BootLoaders.GRUB => new GrubBootLoaderUpdater()
        };
    }
}