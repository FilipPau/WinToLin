namespace WinToLin.Migrator.DistroDependent.Preparation;

public interface IPreparationStep
{
    public Task PrepareAsync(string isoPath);
}