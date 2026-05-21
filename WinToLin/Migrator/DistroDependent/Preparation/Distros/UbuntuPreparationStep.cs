using System.Diagnostics;
using System.Windows;
using WinToLin.Migrator.DistroDependent.Preparation.Tools;

namespace WinToLin.Migrator.DistroDependent.Preparation.Distros;

public class UbuntuPreparationStep : IPreparationStep
{
    public async Task PrepareAsync(string isoPath)
    {
        try
        {
            await XorrisoHelper.RunXorrisoExtraction(isoPath, "/boot/grub/grub.cfg", "WinToLin_Build/grub.cfg");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Extraction Error:\n\n" + ex.Message);
        }
    }
}