using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using CryptSharp;
using WinToLin.Logic.Manager;

namespace WinToLin.Migrator.InstallScriptCreators;

public class UbuntuInstallScriptCreator : IInstallScriptWriter
{
    public async void CreateAndWriteMigrationScripts(string workingDirectory)
    {
        var migrationPaths = ConfigManager.Instance.BackupPaths;

        string yamlPath = Path.Combine(workingDirectory, "autoinstall.yaml");
        string metaData = Path.Combine(workingDirectory, "meta-data");
        string userData = Path.Combine(workingDirectory, "user-data");

        string autoinstallContent = GenerateYamlContent(migrationPaths);
        await File.WriteAllTextAsync(yamlPath, autoinstallContent);
        await File.WriteAllTextAsync(metaData, "");
        await File.WriteAllTextAsync(userData, autoinstallContent);
    }


    private static string GenerateYamlContent(Dictionary<string, string> migrationPaths)
    {
        var manager = ConfigManager.Instance;
        var appData = GetAppBuckets();

        var aptPackages = new List<string>();
        var snapLines = new List<string>();

        // 1. Process selected apps into Buckets
        foreach (var rawName in manager.SoftwareNames.Distinct())
        {
            string name = rawName.ToLower().Trim();

            if (appData.Alternatives.TryGetValue(name, out string alternative))
                name = alternative;

            if (appData.NativeApt.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                aptPackages.Add(name);
            }
            else
            {
                var snap = appData.SnapPackages.FirstOrDefault(x =>
                    x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (snap != null)
                {
                    string line = $"- name: {snap.Name}";
                    if (snap.Name == "code" || snap.Name.Contains("studio") || snap.Name.Contains("idea"))
                        line += "\n      classic: true";
                    snapLines.Add(line);
                }
            }
        }

        // 2. Generate Migration Commands
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

        if (snapLines.Count > 0)
        {
            yaml.AppendLine("  snaps:");
            foreach (var line in snapLines.Distinct()) // Added Distinct to prevent double entries
                yaml.AppendLine($"    {line}");
        }

        yaml.Append(migrationCmds.ToString());

        yaml.AppendLine("  identity:");
        yaml.AppendLine($"    hostname: {manager.UserName}-pc");
        yaml.AppendLine($"    username: {manager.UserName}");
        yaml.AppendLine($"    password: \"{Crypter.Sha512.Crypt("test")}\"");

        return yaml.ToString();
    }


    private static AppData GetAppBuckets()
    {
        string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "ubuntu_packages.json");
        if (!File.Exists(jsonPath)) return new AppData();

        try
        {
            string jsonString = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var root = JsonSerializer.Deserialize<AppRoot>(jsonString, options);
            return root?.Buckets ?? new AppData();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading JSON: {ex.Message}");
            return new AppData();
        }
    }

    private static string GetSafeFilename(string filename)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
        return filename.Replace(" ", "_");
    }


    private class AppRoot
    {
        [JsonPropertyName("ubuntu_migration_buckets")]
        public AppData Buckets { get; set; }
    }

    private class AppData
    {
        [JsonPropertyName("native_apt")] public List<string> NativeApt { get; set; } = new List<string>();
        [JsonPropertyName("snap_packages")] public List<SnapInfo> SnapPackages { get; set; } = new List<SnapInfo>();

        public Dictionary<string, string> Alternatives { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "photoshop", "gimp" }, { "winrar", "7zip" }, { "office", "libreoffice" },
                { "google chrome", "firefox" }, { "google", "firefox" }, { "chrome", "firefox" },
                { "utorrent", "qbittorrent" }
            };
    }

    private class SnapInfo
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        public bool Classic { get; set; } = false;
    }
}