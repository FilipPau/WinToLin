using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CryptSharp;

namespace WinToLin.Migration
{
    public static class UbuntuProvider
    {
        public static async Task GenerateAsync(string isoPath, string workDirectory)
        {
            if (!File.Exists(isoPath)) throw new FileNotFoundException("Source ISO not found", isoPath);

            var migrationPaths = ConfigManager.Instance.BackupPaths;

            Directory.CreateDirectory(workDirectory);
            string migrationArchivesDir = Path.Combine(workDirectory, "migration_archives");

            if (Directory.Exists(migrationArchivesDir))
                Directory.Delete(migrationArchivesDir, true);

            Directory.CreateDirectory(migrationArchivesDir);

            foreach (var (path, _) in migrationPaths)
            {
                if (Directory.Exists(path))
                {
                    string folderName = new DirectoryInfo(path).Name;
                    string safeName = GetSafeFilename(folderName);
                    string archivePath = Path.Combine(migrationArchivesDir, $"{safeName}.zip");
                    ZipDirectorySafely(path, archivePath);
                }
            }

            string outputIsoPath = Path.Combine(workDirectory, "WinToLin_Custom.iso");
            string xorrisoPath = "xorriso";

            
            string yamlPath = Path.Combine(workDirectory, "autoinstall.yaml");
            string metaData = Path.Combine(workDirectory, "meta-data");
            string userData = Path.Combine(workDirectory, "user-data");
            string grubPath = Path.Combine(workDirectory, "grub.cfg");

            
            string autoinstallContent = GenerateYamlContent(migrationPaths.Select(x => x.Value).ToList());
            await File.WriteAllTextAsync(yamlPath, autoinstallContent);
            await File.WriteAllTextAsync(metaData, "");
            await File.WriteAllTextAsync(userData, autoinstallContent);
            await File.WriteAllTextAsync(grubPath, GenerateGrubContent());

            await InjectAndBuildAsync(isoPath, outputIsoPath, xorrisoPath);
        }

        private static string GenerateYamlContent(List<string> migrationPaths)
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

            var localizedFolders = GetLocalizedPaths(manager.Language);
            foreach (var path in migrationPaths)
            {
                if (Directory.Exists(path))
                {
                    string folderName = new DirectoryInfo(path).Name;
                    string safeName = GetSafeFilename(folderName);
                    string targetFolder = localizedFolders.GetValueOrDefault(folderName, $"migrated_{safeName}");
                    string targetPath = $"/target/home/{manager.UserName}/{targetFolder}";

                    migrationCmds.AppendLine($"    - mkdir -p {targetPath}");
                    migrationCmds.AppendLine($"    - unzip -o /cdrom/migration/{safeName}.zip -d {targetPath} || true");
                }
            }

            migrationCmds.AppendLine(
                $"    - curtin in-target -- chown -R {manager.UserName}:{manager.UserName} /home/{manager.UserName}/");

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

        private static void ZipDirectorySafely(string sourceDir, string zipPath)
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                DirectoryInfo di = new DirectoryInfo(sourceDir);
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true, RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };
                foreach (var file in di.EnumerateFiles("*", options))
                {
                    if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                        file.Name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        archive.CreateEntryFromFile(file.FullName, Path.GetRelativePath(sourceDir, file.FullName));
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        private static string GetSafeFilename(string filename)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
            return filename.Replace(" ", "_");
        }

        private static async Task InjectAndBuildAsync(string inIso, string outIso, string xorriso)
        {
            // Always leave the paths as is, xorriso needs them to be so, pls
            StringBuilder xorArgs = new StringBuilder();
            xorArgs.Append($"-indev \"{inIso}\" -outdev \"{outIso}\" ");

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

        private static Dictionary<string, string> GetLocalizedPaths(string locale)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Documents", "Documents" }, { "Pictures", "Pictures" }, { "Music", "Music" }, { "Videos", "Videos" },
                { "Desktop", "Desktop" }, { "Downloads", "Downloads" }
            };
            if (locale.StartsWith("de", StringComparison.OrdinalIgnoreCase))
            {
                mapping["Documents"] = "Dokumente";
                mapping["Pictures"] = "Bilder";
                mapping["Music"] = "Musik";
                mapping["Videos"] = "Videos";
                mapping["Desktop"] = "Schreibtisch";
            }

            return mapping;
        }

        private static string GenerateGrubContent() =>
            "set timeout=5\nset default=0\n\nmenuentry \"WinToLin Automated Install\" {\n    set gfxpayload=keep\n    linux /casper/vmlinuz autoinstall ds=nocloud\\;seedfrom=/cdrom/preseed/ --- quiet splash\n    initrd /casper/initrd\n}";
    }
}