using System;
using System.IO;
using System.Text.Json;

namespace WinToLin.Migration;

public static class ManifestBuilder
{
    private static readonly Manager manager = Manager.Instance;

    public static void Write(string manifestPath)
    {
        var manifest = new
        {
            // =========================
            // SYSTEM CONFIGURATION
            // =========================
            system = new
            {
                distro = manager.DistroName,
                language = manager.Language,
                timezone = manager.TimeZone,
                keyboard = manager.KeyboardLayout,
            },


            // =========================
            // SOFTWARE INSTALLATION
            // =========================
            packages = manager.SoftwareNames,

            // =========================
            // BACKUP MIGRATION DATA
            // =========================
            backup = new
            {
                paths = manager.BackupPaths,
                strategy = "mirror",
                target = "/home/user",
                preserveStructure = true
            },

            // =========================
            // HARDWARE CONTEXT (optional tuning)
            // =========================
            hardware = new
            {
                gpus = manager.GPUNames,
                network = manager.NICNames,
                drives = manager.Drives,
                osTargetDrive = manager.OsDrive
            },

            // =========================
            // USB BOOT CONTEXT
            // =========================
            usb = new
            {
                deviceLetter = manager.InstalationUSBLetter,
                deviceId = manager.InstallationUSBDeviceId,
            },

            // =========================
            // NETWORK RESTORE
            // =========================
            network = new
            {
                wifiSsid = manager.UsedWlanSSId,
                wifiProfilePath = manager.WifiProfileExportPath
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