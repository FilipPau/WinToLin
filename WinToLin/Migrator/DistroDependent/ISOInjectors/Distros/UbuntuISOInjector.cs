using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinToLin.Migrator.ISOInjectors;

public class UbuntuISOInjector : IISOInjector
{
    public async void Inject(string inputISO, string outputISO, string xorriso)
    {
              // Always leave the paths as is, xorriso needs them to be so, pls
            StringBuilder xorArgs = new StringBuilder();
            xorArgs.Append($"-indev \"{inputISO}\" -outdev \"{outputISO}\" ");

            // Mapping files
            xorArgs.Append(
                $"-map \"{Path.Combine("WinToLin_Build", "autoinstall.yaml")}\" \"/preseed/autoinstall.yaml\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "user-data")}\" \"/preseed/user-data\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "meta-data")}\" \"/preseed/meta-data\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "grub.cfg")}\" \"/boot/grub/grub.cfg\" ");
            xorArgs.Append($"-map \"{Path.Combine("WinToLin_Build", "migration_archives")}\" \"/migration\" ");

            // --- Critical Ownership & Permission Fixes (Surgical to /migration) ---

            // 1. Force root ownership to remove the "random Windows user" (197608)
            xorArgs.Append("-chown_r 0 /migration -- ");
            xorArgs.Append("-chgrp_r 0 /migration -- ");

            // 2. Set readable permissions for all migration files
            // 3. The 'a+X' (capital X) adds the Execute bit ONLY to directories 
            // This turns 'drw-' into 'drwxr-xr-x' so the installer can enter the folder.
            xorArgs.Append("-chmod_r 0644 /migration -- ");

            // 4. Standard permissions for configuration files
            xorArgs.Append(
                "-chmod 0644 /preseed/autoinstall.yaml /preseed/user-data /preseed/meta-data /boot/grub/grub.cfg -- ");

            // Finalize
            xorArgs.Append($"-boot_image any replay -commit ");

            var psi = new ProcessStartInfo
            {
                FileName = xorriso,
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