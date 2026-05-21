namespace WinToLin.Migrator.DistroDependent.Modification;

public interface  IModificationStep
{
    public Task ModifyAsync(string workDir);
}