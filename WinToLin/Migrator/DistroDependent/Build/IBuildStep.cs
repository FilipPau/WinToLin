namespace WinToLin.Migrator.DistroDependent.Build;

public interface  IBuildStep
{
    public Task BuildAsync(string isoPath, string outputIso, string xorrisoPath);
}