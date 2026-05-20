using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinToLin.Migrator.ISOInjectors;

public class FedoraISOInjector : IISOInjector
{
    public async void Inject(string inputISO, string outputISO, string xorriso)
    {
        StringBuilder xorArgs = new StringBuilder();
        xorArgs.Append($"-indev \"{inputISO}\" -outdev \"{outputISO}\" ");

        // =========================
        // KICKSTART (Fedora replaces preseed/autoinstall)
        // =========================
        xorArgs.Append(
            $"-map \"{Path.Combine("WinToLin_Build", "ks.cfg")}\" \"/ks.cfg\" ");

        // =========================
        // BOOTLOADER (Fedora GRUB location)
        // =========================
        xorArgs.Append(
            $"-map \"{Path.Combine("WinToLin_Build", "grub.cfg")}\" \"/boot/grub2/grub.cfg\" ");

        // =========================
        // MIGRATION FILES
        // =========================
        xorArgs.Append(
            $"-map \"{Path.Combine("WinToLin_Build", "migration_archives")}\" \"/migration\" ");

        // =========================
        // PERMISSIONS
        // =========================

        xorArgs.Append("-chown_r 0 /migration -- ");
        xorArgs.Append("-chgrp_r 0 /migration -- ");
        xorArgs.Append("-chmod_r 0644 /migration -- ");

        xorArgs.Append(
            "-chmod 0644 /ks.cfg /boot/grub2/grub.cfg -- ");

        // =========================
        // BOOT IMAGE REPLAY
        // =========================
        xorArgs.Append("-boot_image any replay -commit ");

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

        if (process.ExitCode != 0)
            throw new Exception("Xorriso failed: " + err);
    }
}