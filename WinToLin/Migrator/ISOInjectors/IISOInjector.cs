namespace WinToLin.Migrator.ISOInjectors;

public interface IISOInjector
{
    public void Inject(string inputISO, string outputISO, string xorriso);
}