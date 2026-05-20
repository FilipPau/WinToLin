using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using WinToLin.Helper;
using WinToLin.Logic.Manager;

namespace WinToLin.Logic.Utils
{
    public static class BackupScannerUtil
    { 
        private static readonly Dictionary<string, Dictionary<string, string>> LanguagePack = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new() {
                { "Documents", "Documents" }, { "Pictures", "Pictures" }, { "Videos", "Videos" },
                { "Music", "Music" }, { "Desktop", "Desktop" }, { "Downloads", "Downloads" }
            },
            ["de"] = new() {
                { "Documents", "Dokumente" }, { "Pictures", "Bilder" }, { "Videos", "Videos" },
                { "Music", "Musik" }, { "Desktop", "Schreibtisch" }, { "Downloads", "Downloads" }
            }
        };

        public static string CurrentLocale => SystemSettingsHelper.GetLanguage().Substring(0, 2).ToLower();

        public static void ScanBackupLocations(Action<string, string, string, string> onFolderFound)
        {
            AddStandardFolder("Documents", Environment.SpecialFolder.MyDocuments, onFolderFound);
            AddStandardFolder("Pictures", Environment.SpecialFolder.MyPictures, onFolderFound);
            AddStandardFolder("Videos", Environment.SpecialFolder.MyVideos, onFolderFound);
            AddStandardFolder("Music", Environment.SpecialFolder.MyMusic, onFolderFound);
            AddStandardFolder("Desktop", Environment.SpecialFolder.DesktopDirectory, onFolderFound);
            
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddIfExists("Standard Folders", GetTranslation("Downloads"), Path.Combine(userPath, "Downloads"), "Downloads", onFolderFound);
            
            AddIfExists("Application Configs", "VSCode", Path.Combine(userPath, "AppData", "Roaming", "Code"), ".config/Code", onFolderFound);
            AddIfExists("Application Configs", "SSH Keys", Path.Combine(userPath, ".ssh"), ".ssh", onFolderFound);
        }

        private static void AddStandardFolder(string key, Environment.SpecialFolder folder, Action<string, string, string, string> onFolderFound)
        {
            string path = Environment.GetFolderPath(folder);
            AddIfExists("Standard Folders", GetTranslation(key), path, key, onFolderFound);
        }

        private static void AddIfExists(string category, string name, string physicalPath, string linuxHint, Action<string, string, string, string> onFolderFound)
        {
            if (string.IsNullOrEmpty(physicalPath) || !Directory.Exists(physicalPath)) return;
            
            string linuxPath = MapToLinux(physicalPath, linuxHint);
            onFolderFound?.Invoke(category, name, physicalPath, linuxPath);
        }

        public static string GetTranslation(string key)
        {
            if (LanguagePack.TryGetValue(CurrentLocale, out var lang) && lang.TryGetValue(key, out var translated))
                return translated;
            
            return LanguagePack["en"][key];
        }

        public static string MapToLinux(string winPath, string linuxHint)
        {
            string user = ConfigManager.Instance.UserName.ToLower();
            string targetFolder = linuxHint;

            if (targetFolder != null && LanguagePack.TryGetValue(CurrentLocale, out var lang))
            {
                if (lang.TryGetValue(targetFolder, out var translated))
                {
                    targetFolder = translated;
                }
            }

            targetFolder ??= Path.GetFileName(winPath);
            return $"/home/{user}/{targetFolder}";
        }

        public static bool IsPathAlreadyCovered(string newPath, IEnumerable<string> existingPaths)
        {
            string normNew = NormalizePath(newPath);
            return existingPaths.Any(p => normNew.StartsWith(NormalizePath(p), StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsSubPath(string child, string parent) => 
            NormalizePath(child).StartsWith(NormalizePath(parent), StringComparison.OrdinalIgnoreCase) && child.Length > parent.Length;

        public static string NormalizePath(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}