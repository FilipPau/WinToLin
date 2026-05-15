using System;
using System.IO;
using System.Text.Json;

namespace WinToLin.Migration;

public static class ManifestBuilder
{
    private static readonly ConfigManager ConfigManager = ConfigManager.Instance;

    public static void Write(string manifestPath)
    {
        var manifest = new
        {
            // =========================
            // SYSTEM CONFIGURATION
            // =========================
            system = new
            {
                distro = ConfigManager.DistroName,
                language = ConfigManager.Language,
                timezone = ConfigManager.TimeZone,
                keyboard = ConfigManager.KeyboardLayout,
            },


            // =========================
            // SOFTWARE INSTALLATION
            // =========================
            packages = ConfigManager.SoftwareNames,

            // =========================
            // BACKUP MIGRATION DATA
            // =========================
            backup = new
            {
                paths = ConfigManager.BackupPaths,
                strategy = "mirror",
                target = "/home/user",
                preserveStructure = true
            },

            // =========================
            // HARDWARE CONTEXT (optional tuning)
            // =========================
            hardware = new
            {
                gpus = ConfigManager.GPUNames,
                network = ConfigManager.NICNames,
                drives = ConfigManager.Drives,
                osTargetDrive = ConfigManager.OsDrive
            },

            // =========================
            // USB BOOT CONTEXT
            // =========================
            usb = new
            {
                deviceLetter = ConfigManager.InstalationUSBLetter,
                deviceId = ConfigManager.InstallationUSBDeviceId,
            },

            // =========================
            // NETWORK RESTORE
            // =========================
            network = new
            {
                wifiSsid = ConfigManager.UsedWlanSSId,
                wifiProfilePath = ConfigManager.WifiProfileExportPath
            },

            // =========================
            // POST INSTALL BEHAVIOR
            // =========================
            postInstall = new
            {
                restoreFiles = true,
                restoreScript = "post-install.sh",
                fixPermissions = true,
                runFirstBoot = true
            },

            // =========================
            // METADATA
            // =========================
            meta = new
            {
                version = "2.0",
                createdAt = DateTime.UtcNow,
                generator = "WinToLin Migrator"
            }
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(
            Path.Combine(manifestPath),
            json
        );
    }
}