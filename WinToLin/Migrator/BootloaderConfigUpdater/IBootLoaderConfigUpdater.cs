namespace WinToLin.Migrator.BootloaderConfigUpdater;

public interface IBootLoaderConfigUpdater
{
    public void UpdateAndWriteBootLoaderConfig(string workingDirectory);
}