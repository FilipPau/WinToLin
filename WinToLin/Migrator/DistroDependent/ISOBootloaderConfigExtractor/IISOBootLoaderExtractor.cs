namespace WinToLin.Migrator.ISOBootloaderConfigExtractor;

public interface IISOBootLoaderExtractor
{
    public Task ExtractAsync(string isoPath);
}