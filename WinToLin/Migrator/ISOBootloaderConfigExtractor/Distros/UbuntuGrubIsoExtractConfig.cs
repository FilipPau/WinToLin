using System.Diagnostics;
using System.Windows;

namespace WinToLin.Migrator.ISOBootloaderConfigExtractor;

public class UbuntuGrubIsoExtractConfig : IISOBootLoaderExtractor
{
    /// <summary>
    /// Extracts only the necessary boot files (grub.cfg) from the ISO to a staging directory.
    /// </summary>
    public async Task ExtractAsync(
        string isoPath)
    {
        try
        {
            await RunXorrisoExtraction(isoPath, "/boot/grub/grub.cfg", "WinToLin_Build/grub.cfg");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Extraction Error:\n\n" + ex.Message);
        }
    }

    private static async Task<bool> RunXorrisoExtraction(string isoPath, string internalPath, string localDest)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "xorriso",
            Arguments = $"-osirrox on " +
                        $"-indev \"{isoPath}\" " +
                        $"-chmod 0777 \"{internalPath}\" -- " +
                        $"-extract \"{internalPath}\" \"{localDest}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }
}
