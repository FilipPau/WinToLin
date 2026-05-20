namespace WinToLin.Migrator;

public interface IInstallScriptWriter
{
    public Task CreateAndWriteMigrationScripts(string workingDirectory);
}