using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        await File.WriteAllTextAsync(grubPath, UpdateBootLoader(grubPath));
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
        ks.AppendLine($"user --name={manager.UserName} --password={manager.UserName}pass --groups=wheel --plaintext");
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

    private string UpdateBootLoader(string grubPath)
    {
        // Read all lines into memory so we can modify and rewrite the file safely
        string[] lines = File.ReadAllLines(grubPath);
        bool updatedFirstOccurrence = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Only modify the very first occurrence of this target line
            if (!updatedFirstOccurrence && line.Contains("linux /images/pxeboot/vmlinuz"))
            {
                // Match 'LABEL=' followed by non-whitespace characters
                Match match = Regex.Match(line, @"LABEL=([^\s]+)");

                if (match.Success)
                {
                    // Dynamically extract the label name found on this line
                    string labelName = match.Groups[1].Value;

                    // Construct the exact insertion string based on the extracted label
                    string insertString = $"inst.ks=hd:LABEL={labelName}:/ks.cfg ";

                    // Find the position of "quiet" to insert our new argument right before it
                    int quietIndex = line.IndexOf("quiet");
                    if (quietIndex != -1)
                    {
                        lines[i] = line.Insert(quietIndex, insertString);
                        updatedFirstOccurrence = true; // Prevents modifying subsequent lines
                    }
                }
            }
        }

        // Write the complete, updated text back to the GRUB file
        File.WriteAllLines(grubPath, lines, Encoding.UTF8);

        return grubPath;
    }

    #endregion
}