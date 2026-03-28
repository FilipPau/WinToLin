using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
            DistroName = "";
        }

        #region Hardware

        [JsonPropertyName("gpus")]
        public List<string> GPUNames { get; set; }
        public void AddGPU(string gpuName) => GPUNames.Add(gpuName);
        public void AddGPUs(List<string> gpuNames) => GPUNames.AddRange(gpuNames);

        [JsonPropertyName("networkCards")]
        public List<string> NICNames { get; set; }
        public void AddNIC(string nicName) => NICNames.Add(nicName);
        public void AddNICs(List<string> nicNames) => NICNames.AddRange(nicNames);

        [JsonPropertyName("storageDrives")]
        public List<string> Drives { get; set; }
        
        public void AddDrive(string drive) => Drives.Add(drive);
        public void AddDrives(List<string> drives) => Drives.AddRange(drives);

        #endregion

        #region Software

        [JsonPropertyName("softwareToInstall")]
        public List<string> SoftwareNames { get; set; }
        public void AddSoftware(string softwareName) => SoftwareNames.Add(softwareName);
        public void RemoveSoftware(string softwareName) => SoftwareNames.Remove(softwareName);
        public void AddSoftwareList(List<string> softwareList) => SoftwareNames.AddRange(softwareList);

        #endregion

        #region Backup

        [JsonPropertyName("backupLocations")]
        public List<string> BackupPaths { get; set; }
        public void AddBackupPath(string backupPath) => BackupPaths.Add(backupPath);
        public void RemoveBackupPath(string backupPath) => BackupPaths.Remove(backupPath);

        #endregion

        #region Distro

        [JsonPropertyName("selectedDistro")]
        public string DistroName { get; set; }
        public void SetDistro(string distroName) => DistroName = distroName;

        #endregion
    }
}