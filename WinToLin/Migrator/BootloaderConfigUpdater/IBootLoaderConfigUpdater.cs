namespace WinToLin.Migrator.BootloaderConfigUpdater;

public interface IBootLoaderConfigUpdater
{
    public Task UpdateAndWriteBootLoaderConfig(string workingDirectory);
}