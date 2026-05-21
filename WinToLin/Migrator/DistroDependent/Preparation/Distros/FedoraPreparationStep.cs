using System.Windows;
using WinToLin.Migrator.DistroDependent.Preparation.Tools;

namespace WinToLin.Migrator.DistroDependent.Preparation.Distros;

public class FedoraPreparationStep : IPreparationStep
{
    public async Task PrepareAsync(string isoPath)
    {
        try
        {
            await XorrisoHelper.RunXorrisoExtraction(isoPath, "/boot/grub2/grub.cfg", "WinToLin_Build/grub.cfg");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Extraction Error:\n\n" + ex.Message);
        }
    }
}