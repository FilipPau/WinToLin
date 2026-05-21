using System.Diagnostics;

namespace WinToLin.Migrator.DistroDependent.Preparation.Tools;

public class XorrisoHelper
{
    public static async Task<bool> RunXorrisoExtraction(string isoPath, string internalPath, string localDest)
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