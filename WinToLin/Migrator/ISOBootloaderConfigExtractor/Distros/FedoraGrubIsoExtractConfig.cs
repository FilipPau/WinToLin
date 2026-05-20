using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WinToLin.Migrator.ISOBootloaderConfigExtractor;

public class FedoraGrubIsoExtractConfig : IISOBootLoaderExtractor
{
    /// <summary>
    /// Extracts only the Fedora GRUB config from ISO to staging directory.
    /// </summary>
    public async Task ExtractAsync(string isoPath)
    {
        try
        {
            // The confirmed internal path for your Fedora grub.cfg
            string internalPath = "/boot/grub2/grub.cfg";
            string localDest = "WinToLin_Build/grub.cfg";

            // Ensure the target directory exists before running xorriso
            string targetDirectory = Path.GetDirectoryName(localDest);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var success = await RunXorrisoExtraction(
                isoPath,
                internalPath,
                localDest
            );

            if (!success)
            {
                throw new Exception($"xorriso failed to extract the file from '{internalPath}'.");
            }
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
            Arguments = $"-osirrox on -indev \"{isoPath}\" -extract \"{internalPath}\" \"{localDest}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
            await process.WaitForExitAsync();

            // Returns true only if xorriso returned an exit code of 0 (success)
            return process.ExitCode == 0;
        }
        catch
        {
            // Fail gracefully if xorriso binary is missing, uninstalled, or unreachable
            return false;
        }
    }
}