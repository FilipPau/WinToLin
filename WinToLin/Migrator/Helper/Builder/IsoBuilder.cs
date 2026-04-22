using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinToLin.Migration;

public static class IsoBuilder
{
    public static async Task<string> BuildBootableIsoAsync(
        string originalIsoPath, // Path to your distro.iso
        string workDir,       // Path to your folder with modified /boot and /nocloud
        IProgress<double>? progress = null)
    {
        string outputIsoPath = Path.GetFullPath(Path.Combine(workDir, "wintolin.iso"));
    
        // Total size for progress calculation
        long totalSize = new FileInfo(originalIsoPath).Length;

        var psi = new ProcessStartInfo
        {
            FileName = "xorriso",
            // We use the Native command set here
            Arguments = 
                $"-indev \"{originalIsoPath}\" " +
                $"-outdev \"{outputIsoPath}\" " +
                // MAP 1: Overlay your custom nocloud folder
                $"-map \"{Path.Combine("WinToLin_Build", "nocloud")}\" \"/nocloud\" " + 
                // MAP 2: Overlay your custom boot folder (where your grub.cfg is)
                $"-map \"{Path.Combine("WinToLin_Build", "boot")}\" \"/boot\" " + 
                // Ensures all boot settings (BIOS/UEFI/Hybrid) are copied from original
                $"-boot_image any keep " +     
                $"-commit",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var process = new Process { StartInfo = psi };
    
        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            // Matches: "xorriso : UPDATE : 57.89% done"
            var match = Regex.Match(e.Data, @"UPDATE\s*:\s*(\d+(\.\d+)?)%\s*done");
            if (match.Success && double.TryParse(match.Groups[1].Value, 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out double percent))
            {
                progress?.Report(percent);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0) throw new Exception("ISO Build Failed");

        progress?.Report(100.0);
        return outputIsoPath;
    }
}