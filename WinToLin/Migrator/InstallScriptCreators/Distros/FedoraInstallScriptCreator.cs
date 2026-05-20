using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinToLin.Logic.Manager;

namespace WinToLin.Migrator.InstallScriptCreators;

public class FedoraInstallScriptCreator : IInstallScriptWriter
{
    // Changed from async void to async Task to prevent race conditions during extraction/ISO building
    public async Task CreateAndWriteMigrationScripts(string workingDirectory)
    {
        string ksPath = Path.Combine(workingDirectory, "ks.cfg");

        string content = GenerateKickstartContent();

        await File.WriteAllTextAsync(ksPath, content);
    }

    private static string GenerateKickstartContent()
    {
        var manager = ConfigManager.Instance;
        var migrationPaths = manager.BackupPaths;

        var sb = new StringBuilder();

        sb.AppendLine();
        // System bootloader configuration
        sb.AppendLine("# System bootloader configuration");
        sb.AppendLine("bootloader --append=\" crashkernel=auto\" --location=mbr --boot-drive=sda");
        sb.AppendLine();

        // Partition clearing information
        sb.AppendLine("# Partition clearing information");
        sb.AppendLine("clearpart --all --drives=sda --initlabel");
        sb.AppendLine();

        // Partition information
        sb.AppendLine("# Partition information (To encrypt the partition add: --encrypted --luks-version=luks2 --passphrase=P@ssword)");
        sb.AppendLine("autopart --type=lvm");
        sb.AppendLine();

        // Use text install
        sb.AppendLine("# Use text install");
        sb.AppendLine("text");
        sb.AppendLine();

        // Install source
        sb.AppendLine("# Install source");
        sb.AppendLine("cdrom #FAST - Choose this option for a faster local install from cdrom or ISO.");
        sb.AppendLine();

        // Keyboard layouts
        sb.AppendLine("# Keyboard layouts");
        sb.AppendLine($"keyboard --vckeymap={manager.KeyboardLayout} --xlayouts='{manager.KeyboardLayout}'");
        sb.AppendLine();

        // System language
        sb.AppendLine("# System language");
        sb.AppendLine("lang " + manager.Language);
        sb.AppendLine();

        // License agreement
        sb.AppendLine("# License agreement");
        sb.AppendLine("eula --agreed");
        sb.AppendLine();

        // Network information
        sb.AppendLine("# Network information");
        sb.AppendLine("network --onboot=yes --bootproto=dhcp --noipv6 --activate --hostname=rockylinux-custom.localdomain");
        sb.AppendLine();

        // Clear pass details commentary & pass rules
        sb.AppendLine("# To create user password hashes type use the command: python3 -c 'import crypt,getpass; print(crypt.crypt(getpass.getpass()))'");
        sb.AppendLine("# The password below for both root and administrator users is P@ssword ");
        sb.AppendLine("# If you don't care about hashing the passwords (not recommended), simply use the lines below");
        sb.AppendLine("#Root password=P@ssword");
        sb.AppendLine($"#user --groups=dialout,kvm,libvirt,qemu,wheel --name={manager.UserName} --password=P@ssword --gecos=\"{manager.UserName}\"");
        sb.AppendLine();

        // Create root user
        sb.AppendLine("# Create root user");
        sb.AppendLine("rootpw --iscrypted $6$INireMy4ZLQwW7NN$btkLm/dwn9qV/XWW8dhDd2hjKHk8tj59q.Q8qSW7i4LojhPYWXDx4YRWxXQ/.30E8ND3IcImJ.pys3DyYwco0.");
        sb.AppendLine();

        // Create additional users
        sb.AppendLine("# Create additional users");
        sb.AppendLine($"user --name={manager.UserName} --groups=dialout,kvm,libvirt,qemu,wheel,wheel --password=$6$f9y8RhpOf4kppQlt$FpXm5aOecAV8Hf9DQM4/gHMD.EPbkacI36OQEyS50Iqs0Y2fLnOWeEPGXDhaVZjHpNF4RhEdyRDxBDByffCGH/ --iscrypted --gecos=\"{manager.UserName}\"");
        sb.AppendLine();

        // Run the Setup Agent on first boot
        sb.AppendLine("# Run the Setup Agent on first boot");
        sb.AppendLine("firstboot --disable");
        sb.AppendLine();

        // X Window System configuration information
        sb.AppendLine("# X Window System configuration information");
        sb.AppendLine("xconfig  --startxonboot");
        sb.AppendLine();

        // System services
        sb.AppendLine("# System services");
        sb.AppendLine("services --enabled=\"chronyd\"");
        sb.AppendLine();

        // System timezone
        sb.AppendLine("# System timezone");
        string utcFlag = manager.TimeZone.ToLower().Contains("utc") ? "--utc" : "--utc"; 
        sb.AppendLine($"timezone {manager.TimeZone} {utcFlag}");
        sb.AppendLine();

        // Packages block setup
        sb.AppendLine("%packages");


        foreach (var pkg in manager.SoftwareNames.Distinct())
        {
            sb.AppendLine(pkg.ToLower().Trim());
        }
        sb.AppendLine("%end");
        sb.AppendLine();

        sb.AppendLine("# The state of the machine after the install completes. Leave commented for no action.");
        sb.AppendLine("#shutdown");
        sb.AppendLine("reboot --eject"); // Explicitly run requested reboot with eject action
        sb.AppendLine();

        // Post segment setup
        sb.AppendLine("# Post nochroot");
        sb.AppendLine("%post --interpreter=/usr/bin/bash --log=/root/ks-post-migration.log");
        sb.AppendLine();

        // Configure SELinux
        sb.AppendLine("# Configure SELinux");
        sb.AppendLine("setsebool -P domain_kernel_load_modules on");
        sb.AppendLine();

        // Enable Automatic security updates via dnf-automatic
        sb.AppendLine("# Enable Automatic security updates via dnf-automatic");
        sb.AppendLine("sed -i s/'upgrade_type = default'/'upgrade_type = security'/ /etc/dnf/automatic.conf");
        sb.AppendLine("value=\"no\""); // Temporary placeholder fix logic for string syntax inside sed script if needed
        sb.AppendLine("sed -i s/'apply_updates = no'/'apply_updates = yes'/ /etc/dnf/automatic.conf");
        sb.AppendLine("systemctl enable dnf-automatic.timer");
        sb.AppendLine();

        // Bring network interfaces up
        sb.AppendLine("# Bring network interfaces up");
        sb.AppendLine("#for i in $(nmcli -g NAME con show); do nmcli con up \"$i\"; done;");
        sb.AppendLine();

        // Install EPEL repository and packages
        sb.AppendLine("# Install EPEL repository and packages");
        sb.AppendLine("dnf --nogpgcheck -y install https://dl.fedoraproject.org/pub/epel/epel-release-latest-9.noarch.rpm");
        sb.AppendLine();

        // DCONF update for GDM modifications
        sb.AppendLine("# DCONF update for GDM modifications");
        sb.AppendLine("dconf update");
        sb.AppendLine();

        sb.AppendLine("# ==========================================");
        sb.AppendLine("# MIGRATION SCRIPT EXECUTION SECTION        ");
        sb.AppendLine("# ==========================================");
        
        foreach (var path in migrationPaths)
        {
            if (Directory.Exists(path.Key))
            {
                string folderName = new DirectoryInfo(path.Key).Name;
                string safeName = GetSafeFilename(folderName);

                string targetPath = $"/home/{manager.UserName}/{path.Value}";

                sb.AppendLine($"mkdir -p \"{targetPath}\"");
                sb.AppendLine($"unzip -o /run/install/repo/migration/{safeName}.zip -d \"{targetPath}\" || true");
            }
        }
        sb.AppendLine();

        sb.AppendLine("# Skip first-time interactive environment setups");
        sb.AppendLine($"mkdir -p /home/{manager.UserName}/.config");
        sb.AppendLine($"touch /home/{manager.UserName}/.config/gnome-initial-setup-done");
        sb.AppendLine("systemctl mask gnome-initial-setup.service || true");
        sb.AppendLine("systemctl mask firstboot.service || true");
        sb.AppendLine();

        // Fix permissions last so everything in the home folder belongs to the user
        sb.AppendLine($"chown -R {manager.UserName}:{manager.UserName} /home/{manager.UserName}/");
        sb.AppendLine();

        sb.AppendLine("/usr/bin/systemctl set-default graphical.target");
        sb.AppendLine();

        sb.AppendLine("%end");
        sb.Append("#################################################################");

        return sb.ToString();
    }

    private static string GetSafeFilename(string filename)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            filename = filename.Replace(c, '_');

        return filename.Replace(" ", "_");
    }
}