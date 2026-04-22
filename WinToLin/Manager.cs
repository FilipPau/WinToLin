using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinToLin
{
    public sealed class Manager
    {
        private static readonly Lazy<Manager> _instance = new(() => new Manager());
        public static Manager Instance => _instance.Value;

        private Manager()
        {
            GPUNames = new List<string>();
            NICNames = new List<string>();
            Drives = new List<string>();
            SoftwareNames = new List<string>();
            BackupPaths = new List<string>();

            DistroName = string.Empty;
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
        public List<string> SoftwareNames { get; }

        public void AddSoftware(string softwareName)
        {
            if (!string.IsNullOrWhiteSpace(softwareName))
                SoftwareNames.Add(softwareName);
        }

        public void RemoveSoftware(string softwareName)
        {
            SoftwareNames.Remove(softwareName);
        }

        public void AddSoftwareList(IEnumerable<string> softwareList)
        {
            if (softwareList != null)
                SoftwareNames.AddRange(softwareList);
        }

        // =========================
        // Backup
        // =========================

        [JsonPropertyName("backupLocations")] public List<string> BackupPaths { get; }

        public void AddBackupPath(string backupPath)
        {
            if (!string.IsNullOrWhiteSpace(backupPath))
                BackupPaths.Add(backupPath);
        }

        public void RemoveBackupPath(string backupPath)
        {
            BackupPaths.Remove(backupPath);
        }

        // =========================
        // Distro
        // =========================

        [JsonPropertyName("selectedDistro")] public string DistroName { get; private set; }

        public void SetDistro(string distroName)
        {
            DistroName = distroName ?? string.Empty;
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
    }
}