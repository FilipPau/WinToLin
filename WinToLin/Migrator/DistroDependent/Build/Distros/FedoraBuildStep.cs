using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinToLin.Migrator.DistroDependent.Build.Distros;

public class FedoraBuildStep : IBuildStep
{
   public async Task BuildAsync(string inputISO, string outputIso, string xorrisoPath)
    {
         // Always leave the paths as is, xorriso needs them to be so, pls
            StringBuilder xorArgs = new StringBuilder();
            xorArgs.Append($"-indev \"{inputISO}\" -outdev \"{outputIso}\" ");

            // Mapping files    
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "ks.cfg")}\" \"/ks.cfg\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "grub.cfg")}\" \"/boot/grub2/grub.cfg\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "migration_archives")}\" \"/migration\" ");

            xorArgs.Append($"-boot_image any replay -commit ");

            var psi = new ProcessStartInfo
            {
                FileName = xorrisoPath,
                Arguments = xorArgs.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            string err = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception("Xorriso failed: " + err);
    }
}