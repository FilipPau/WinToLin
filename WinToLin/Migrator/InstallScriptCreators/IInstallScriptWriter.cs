namespace WinToLin.Migrator;

public interface IInstallScriptWriter
{
    public void CreateAndWriteMigrationScripts(string workingDirectory);
}