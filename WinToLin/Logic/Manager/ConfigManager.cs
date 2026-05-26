using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinToLin.Logic.Enums;

namespace WinToLin.Logic.Manager
{
    public sealed class ConfigManager
    {
        private static readonly Lazy<ConfigManager> _instance = new(() => new ConfigManager());
        public static ConfigManager Instance => _instance.Value;

        /// <summary>
        /// Set this static variable to the file path where you want the JSON saved.
        /// If left empty or null, the file saving output logic is completely skipped.
        /// </summary>
        public static string OutputFileLocation { get; set; } = null;

        private ConfigManager()
        {
            GPUNames = new();
            NICNames = new();
            Drives = new();
            SoftwareNames = new();
            BackupPaths = new();

            InstalationUSBLetter = string.Empty;
            InstallationUSBDeviceId = string.Empty;

            Language = string.Empty;
            KeyboardLayout = string.Empty;
            TimeZone = string.Empty;
            UsedWlanSSId = string.Empty;
            WifiProfileExportPath = string.Empty;
        }

        // =========================
        // Hardware
        // =========================

        [JsonPropertyName("gpus")] public List<string> GPUNames { get; }

        public void AddGPU(string gpuName)
        {
            if (!string.IsNullOrWhiteSpace(gpuName))
                GPUNames.Add(gpuName);
        }

        public void AddGPUs(IEnumerable<string> gpuNames)
        {
            if (gpuNames != null)
                GPUNames.AddRange(gpuNames);
        }

        [JsonPropertyName("networkCards")] public List<string> NICNames { get; }

        public void AddNIC(string nicName)
        {
            if (!string.IsNullOrWhiteSpace(nicName))
                NICNames.Add(nicName);
        }

        public void AddNICs(IEnumerable<string> nicNames)
        {
            if (nicNames != null)
                NICNames.AddRange(nicNames);
        }

        [JsonPropertyName("storageDrives")] public List<string> Drives { get; }

        public void AddDrive(string drive)
        {
            if (!string.IsNullOrWhiteSpace(drive))
                Drives.Add(drive);
        }

        public void AddDrives(IEnumerable<string> drives)
        {
            if (drives != null)
                Drives.AddRange(drives);
        }

        [JsonPropertyName("driveToInstallOs")] public string OsDrive { get; private set; }

        public void SetOsDrive(string drive)
        {
            OsDrive = drive ?? string.Empty;
        }

        // =========================
        // Software
        // =========================

        [JsonPropertyName("softwareToInstall")]
        public List<(string name, string packageName)> SoftwareNames { get; }

        public void AddSoftware((string name, string packageName) softwareName)
        {
            SoftwareNames.Add(softwareName);
        }

        public void RemoveSoftware((string name, string packageName) software)
        {
            SoftwareNames.Remove(software);
        }


        // =========================
        // Backup
        // =========================

        //Windows to Linux Mapping
        [JsonPropertyName("backupLocations")] public Dictionary<string, string> BackupPaths { get; }

        public void AddBackupPath(string windowsBackupPath, string linuxTargetPath)
        {
            if (!string.IsNullOrWhiteSpace(windowsBackupPath) || !string.IsNullOrWhiteSpace(linuxTargetPath))
                BackupPaths.Add(windowsBackupPath, linuxTargetPath);
        }

        public void RemoveBackupPath(string backupPath)
        {
            BackupPaths.Remove(backupPath);
        }

        // =========================
        // Distro
        // =========================

        [JsonPropertyName("selectedDistro")] public Distros DistroName { get; private set; }

        public void SetDistro(Distros distroName)
        {
            DistroName = distroName;
        }

        // =========================
        // USB
        // =========================

        [JsonPropertyName("instalationUSB")] public string InstalationUSBLetter { get; private set; }

        [JsonPropertyName("installationUSBId")]
        public string InstallationUSBDeviceId { get; private set; }

        public void SetUSB(string letter, string deviceId)
        {
            InstalationUSBLetter = letter ?? string.Empty;
            InstallationUSBDeviceId = deviceId ?? string.Empty;
        }

        // =========================
        // OS CONFIG
        // =========================

        [JsonPropertyName("timeZone")] public string TimeZone { get; private set; }

        [JsonPropertyName("userName")] public string UserName { get; private set; }

        [JsonPropertyName("keyboardLayout")] public string KeyboardLayout { get; private set; }

        [JsonPropertyName("language")] public string Language { get; private set; }

        [JsonPropertyName("usedWlanSSId")] public string UsedWlanSSId { get; private set; }

        [JsonPropertyName("wifiProfileExportPath")]
        public string WifiProfileExportPath { get; private set; }

        // =========================
        // SET SYSTEM CONFIG
        // =========================

        public void SetSystemSettings(
            string userName,
            string language,
            string keyboardLayout,
            string timeZone,
            string wlanSsid,
            string wifiExportPath)
        {
            UserName = userName ?? string.Empty;
            Language = language ?? string.Empty;
            KeyboardLayout = keyboardLayout ?? string.Empty;
            TimeZone = timeZone ?? string.Empty;
            UsedWlanSSId = wlanSsid ?? string.Empty;
            WifiProfileExportPath = wifiExportPath ?? string.Empty;
        }

        // =========================
        // UTILITIES
        // =========================

        /// <summary>
        /// Serializes the current config instance into a readable JSON string. 
        /// Saves to OutputFileLocation file if that path variable is populated.
        /// </summary>
        /// <returns>The formatted JSON string data.</returns>
        public string GetConfigJson()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string jsonOutput = JsonSerializer.Serialize(this, options);

                // Only output and save if the path has been explicitly populated
                if (!string.IsNullOrWhiteSpace(OutputFileLocation))
                {
                    string directoryPath = Path.GetDirectoryName(OutputFileLocation);
                    if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    File.WriteAllText(OutputFileLocation, jsonOutput);

                    Debug.WriteLine("=== Current Config Manager State JSON ===");
                    Debug.WriteLine($"Saved to file successfully: {OutputFileLocation}");
                    Debug.WriteLine("=========================================");
                }

                return jsonOutput;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to generate JSON configuration output: {ex.Message}");
                return string.Empty;
            }
        }
    }
}