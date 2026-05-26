using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinToLin.Logic.Manager;

namespace WinToLin.Migrator.DistroDependent.Modification.Distros;

public class FedoraModificationStep : IModificationStep
{
    public Task ModifyAsync(string workDir)
    {
        return Task.WhenAll(ModifyBootLoader(workDir), CreateInstallScript(workDir));
    }

    private async Task ModifyBootLoader(string workDir)
    {
        string grubPath = Path.Combine(workDir, "grub.cfg");

        await UpdateBootLoader(grubPath);
    }

    private async Task CreateInstallScript(string workDir)
    {
        var migrationPaths = ConfigManager.Instance.BackupPaths;

        string yamlPath = Path.Combine(workDir, "ks.cfg");

        await File.WriteAllTextAsync(yamlPath, GenerateScript(migrationPaths));
    }

    #region Install Script

    private static string GenerateScript(Dictionary<string, string> migrationPaths)
    {
        var manager = ConfigManager.Instance;

        var dnfPackages = new List<string>();
        var flatpakPackages = new List<string>();

        // Process selected apps into Packages
        foreach (var rawName in manager.SoftwareNames.Distinct())
        {
            string name = rawName.packageName;

            // Determine if package target matches flatpak ID syntax or remains native system map
            if (name.Contains(".") && (name.StartsWith("org.") || name.StartsWith("com.") || name.StartsWith("io.") || name.StartsWith("net.")))
            {
                flatpakPackages.Add(name);
            }
            else
            {
                dnfPackages.Add(name);
            }
        }

        StringBuilder ks = new StringBuilder();

        ks.AppendLine("#version=DEVEL");
        ks.AppendLine("graphical");
        ks.AppendLine();

        ks.AppendLine("### INSTALL MODE");
        ks.AppendLine("url --mirrorlist=https://mirrors.fedoraproject.org/mirrorlist?repo=fedora-44&arch=$basearch");
        ks.AppendLine($"lang {manager.Language}");
        ks.AppendLine($"keyboard {manager.KeyboardLayout}");
        ks.AppendLine($"timezone {manager.TimeZone} --utc");
        ks.AppendLine();

        ks.AppendLine("### NETWORK (DHCP auto)");
        ks.AppendLine("network --bootproto=dhcp --device=link --activate");
        ks.AppendLine();

        ks.AppendLine("### USERS");
        ks.AppendLine($"rootpw --plaintext {manager.UserName}pass");
        ks.AppendLine($"user --name={manager.UserName} --password=test --groups=wheel --plaintext");
        ks.AppendLine();

        ks.AppendLine("### DISK (AUTO WIPE - IMPORTANT)");
        ks.AppendLine("clearpart --all --initlabel");
        ks.AppendLine("autopart");
        ks.AppendLine();

        ks.AppendLine("### BOOTLOADER");
        ks.AppendLine("bootloader --location=mbr");
        ks.AppendLine();

        ks.AppendLine("### SECURITY");
        ks.AppendLine("firewall --enabled");
        ks.AppendLine("selinux --enforcing");
        ks.AppendLine();

        ks.AppendLine("### SERVICES");
        ks.AppendLine("services --enabled=gdm");
        ks.AppendLine();

        ks.AppendLine("### PACKAGES");
        ks.AppendLine("%packages");
        ks.AppendLine("@workstation-product-environment");
        ks.AppendLine("@core");
        ks.AppendLine("@standard");
        ks.AppendLine("curl");
        ks.AppendLine("wget");
        ks.AppendLine("htop");
        ks.AppendLine("unzip");

        // Add custom requested DNF native apps
        foreach (var pkg in dnfPackages.Distinct())
        {
            ks.AppendLine(pkg);
        }

        // CRITICAL: Ensure native flatpak application support tool is loaded early in package list
        if (flatpakPackages.Count > 0 && !dnfPackages.Any(x => x.Equals("flatpak", StringComparison.OrdinalIgnoreCase)))
        {
            ks.AppendLine("flatpak");
        }

        ks.AppendLine("%end");
        ks.AppendLine();

        /////////////////////////////////////////////////////
        // 1) DATA MIGRATION DURING INSTALL (CORRECT PHASE)
        /////////////////////////////////////////////////////

        ks.AppendLine("### DATA MIGRATION (INSTALL STAGE)");
        ks.AppendLine("%post --nochroot");
        ks.AppendLine();
        ks.AppendLine("echo \"=== MIGRATION: extracting data into installed system ===\"");
        ks.AppendLine();

        foreach (var path in migrationPaths)
        {
            if (Directory.Exists(path.Key))
            {
                string folderName = new DirectoryInfo(path.Key).Name;
                string safeName = GetSafeFilename(folderName);

                string targetPath =
                    $"/mnt/sysroot/{path.Value.TrimStart('/')}".Replace("//", "/");

                string isoZipSource =
                    $"/run/install/repo/migration/{safeName}.zip";

                ks.AppendLine($"# Migration: {folderName}");
                ks.AppendLine($"mkdir -p \"{targetPath}\"");

                ks.AppendLine($"if [ -f \"{isoZipSource}\" ]; then");
                ks.AppendLine($"    unzip -q -o \"{isoZipSource}\" -d \"{targetPath}/\"");
                ks.AppendLine("else");
                ks.AppendLine($"    echo \"WARNING: missing {isoZipSource}\"");
                ks.AppendLine("fi");
                ks.AppendLine();
            }
        }

        ks.AppendLine($"chown -R 1000:1000 \"/mnt/sysroot/home/{manager.UserName}\"");
        ks.AppendLine("%end");
        ks.AppendLine();


        /////////////////////////////////////////////////////
        // 2) FINAL SYSTEM CONFIGURATION
        /////////////////////////////////////////////////////

        ks.AppendLine("### POST INSTALL CONFIGURATION");
        ks.AppendLine("%post");
        ks.AppendLine("echo \"Installation complete\" > /etc/motd");
        ks.AppendLine("systemctl enable sshd");
        ks.AppendLine("systemctl set-default graphical.target");
        ks.AppendLine();
    
        // Safe operational verification loop for Flatpak execution within the chroot container context
        if (flatpakPackages.Count > 0)
        {
            ks.AppendLine("echo \"=== CONFIGURING FLATPAK PACKAGES ===\"");
            ks.AppendLine("if ! command -v flatpak &> /dev/null; then");
            ks.AppendLine("    dnf install -y flatpak");
            ks.AppendLine("fi");
            ks.AppendLine("flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo");
            
            foreach (var flatpakId in flatpakPackages.Distinct())
            {
                ks.AppendLine($"flatpak install -y flathub {flatpakId} || true");
            }
            ks.AppendLine();
        }

        ks.AppendLine($"DESKTOP=\"/home/{manager.UserName}/Desktop\"");
        ks.AppendLine("STAGING=\"/opt\"");

        ks.AppendLine("if [ -d \"$STAGING\" ]; then");
        ks.AppendLine("    mkdir -p \"$DESKTOP\"");
        ks.AppendLine("    cp -r /opt/* \"$DESKTOP/\"");
        ks.AppendLine($"    chown -R {manager.UserName}:{manager.UserName} \"$DESKTOP\"");
        ks.AppendLine("fi");

        ks.AppendLine("%end");

        return ks.ToString();
    }

    private static string GetSafeFilename(string filename)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
        return filename.Replace(" ", "_");
    }

    #endregion

    #region BootLoader

    
    private async Task UpdateBootLoader(string grubPath)
    {
        string[] lines = await File.ReadAllLinesAsync(grubPath);

        bool updated = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.Contains("set timeout"))
            {
                lines[i] = "set timeout=0";
            }
            
            if (line.Contains("set default"))
            {
                lines[i] = "set default=\"0\"";
            }
            
            // Only first matching linux boot line
            if (!updated && line.Contains("linux") && line.Contains("LABEL="))
            {
                // Extract LABEL value safely
                Match match = Regex.Match(line, @"LABEL=([^\s:]+)");

                if (match.Success)
                {
                    string labelName = match.Groups[1].Value;

                    // Skip if inst.ks already exists
                    if (line.Contains("inst.ks="))
                        continue;

                    string ksParam = $" inst.ks=hd:LABEL={labelName}:/ks.cfg";

                    // Insert before "quiet" if present
                    int quietIndex = line.IndexOf(" quiet");

                    if (quietIndex >= 0)
                    {
                        line = line.Insert(quietIndex, ksParam);
                    }
                    else
                    {
                        // Otherwise append at end
                        line += ksParam;
                    }

                    lines[i] = line;

                    updated = true;
                }
            }
        }

        // Overwrite original file
       await File.WriteAllLinesAsync(grubPath, lines, Encoding.UTF8);
    }
    #endregion
}