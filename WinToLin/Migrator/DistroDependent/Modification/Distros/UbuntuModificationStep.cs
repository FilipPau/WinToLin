using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptSharp;
using WinToLin.Logic.Manager;

namespace WinToLin.Migrator.DistroDependent.Modification.Distros;

public class UbuntuModificationStep : IModificationStep
{
    public Task ModifyAsync(string workDir)
    {
        return Task.WhenAll(ModifyBootLoader(workDir), CreateInstallScript(workDir));
    }

    private async Task ModifyBootLoader(string workDir)
    {
        string grubPath = Path.Combine(workDir, "grub.cfg");

        await File.WriteAllTextAsync(grubPath, GenerateGrubContent());
    }

    private async Task CreateInstallScript(string workDir)
    {
        var migrationPaths = ConfigManager.Instance.BackupPaths;

        string yamlPath = Path.Combine(workDir, "autoinstall.yaml");
        string metaData = Path.Combine(workDir, "meta-data");
        string userData = Path.Combine(workDir, "user-data");

        string autoinstallContent = GenerateYamlContent(migrationPaths);
        await File.WriteAllTextAsync(yamlPath, autoinstallContent);
        await File.WriteAllTextAsync(metaData, "");
        await File.WriteAllTextAsync(userData, autoinstallContent);
    }

    #region Install Script

    private static string GenerateYamlContent(Dictionary<string, string> migrationPaths)
    {
        var manager = ConfigManager.Instance;

        var aptPackages = new List<string>();
        var flatpakPackages = new List<string>();

        // 1. Process selected apps into Packages
        foreach (var rawName in manager.SoftwareNames.Distinct())
        {
            // Extract from tracking tuple structure
            string name = rawName.packageName;

            // Determine if package target matches flatpak ID syntax or remains native system map
            if (name.Contains(".") && (name.StartsWith("org.") || name.StartsWith("com.") || name.StartsWith("io.") || name.StartsWith("net.")))
            {
                flatpakPackages.Add(name);
            }
            else
            {
                // Everything else defaults directly to apt package engine
                aptPackages.Add(name);
            }
        }

        // If Flatpaks are requested, make sure the system installs flatpak package container support natively
        if (flatpakPackages.Count > 0 && !aptPackages.Any(x => x.Equals("flatpak", StringComparison.OrdinalIgnoreCase)))
        {
            aptPackages.Add("flatpak");
        }

        // 2. Generate Migration & Flatpak Post-Install Commands
        StringBuilder migrationCmds = new StringBuilder();
        migrationCmds.AppendLine("  late-commands:");

        Console.WriteLine(migrationPaths.Count);

        foreach (var path in migrationPaths)
        {
            Console.WriteLine(path.Key);
            Console.WriteLine(Directory.Exists(path.Key));

            if (Directory.Exists(path.Key))
            {
                string folderName = new DirectoryInfo(path.Key).Name;
                string safeName = GetSafeFilename(folderName);

                string targetPath = $"/target/{path.Value}";

                migrationCmds.AppendLine($"    - mkdir -p {targetPath}");
                migrationCmds.AppendLine($"    - unzip -o /cdrom/migration/{safeName}.zip -d {targetPath} || true");
            }
        }

        // Run Flatpak installs via chroot context on target inside the deployment sequence loop environment
        if (flatpakPackages.Count > 0)
        {
            migrationCmds.AppendLine("    - curtin in-target -- flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo");
            foreach (var flatpakId in flatpakPackages.Distinct())
            {
                migrationCmds.AppendLine($"    - curtin in-target -- flatpak install -y flathub {flatpakId} || true");
            }
        }

        migrationCmds.AppendLine(
            $"    - curtin in-target -- chown -R {manager.UserName}:{manager.UserName} /home/{manager.UserName}/");

        #region skip ubuntu welcome

        migrationCmds.AppendLine($"    - curtin in-target -- mkdir -p /home/{manager.UserName}/.config");

        migrationCmds.AppendLine(
            $"    - curtin in-target -- touch /home/{manager.UserName}/.config/gnome-initial-setup-done");

        migrationCmds.AppendLine(
            $"    - curtin in-target -- chown -R {manager.UserName}:{manager.UserName} /home/{manager.UserName}/.config");

        migrationCmds.AppendLine("    - curtin in-target -- mkdir -p /etc/skel/.config");
        migrationCmds.AppendLine("    - curtin in-target -- touch /etc/skel/.config/gnome-initial-setup-done");

        migrationCmds.AppendLine("    - curtin in-target -- systemctl mask gnome-initial-setup.service");

        #endregion

        // 3. Assemble YAML
        StringBuilder yaml = new StringBuilder();
        yaml.AppendLine("#cloud-config");
        yaml.AppendLine("autoinstall:");
        yaml.AppendLine("  version: 1");
        yaml.AppendLine($"  locale: {manager.Language}");
        yaml.AppendLine($"  timezone: {manager.TimeZone}");
        yaml.AppendLine("  keyboard:");
        yaml.AppendLine($"    layout: {manager.KeyboardLayout}");

        if (aptPackages.Count > 0)
        {
            yaml.AppendLine("  packages:");
            foreach (var pkg in aptPackages.Distinct())
                yaml.AppendLine($"    - {pkg}");
        }

        yaml.Append(migrationCmds.ToString());

        yaml.AppendLine("  identity:");
        yaml.AppendLine($"    hostname: {manager.UserName}-pc");
        yaml.AppendLine($"    username: {manager.UserName}");
        yaml.AppendLine($"    password: \"{Crypter.Sha512.Crypt("test")}\"");

        return yaml.ToString();
    }

    private static string GetSafeFilename(string filename)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
        return filename.Replace(" ", "_");
    }

    #endregion
    
    #region Boot Loader

    private static string GenerateGrubContent() =>
        @"set timeout=0
set default=0

menuentry ""WinToLin Automated Install"" {
    set gfxpayload=keep
    linux /casper/vmlinuz autoinstall ds=nocloud\;seedfrom=/cdrom/preseed/ --- quiet splash
    initrd /casper/initrd
}";

    #endregion
}