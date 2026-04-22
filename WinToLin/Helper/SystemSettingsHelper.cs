namespace WinToLin.Helper;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using TimeZoneConverter;

public static class SystemSettingsHelper
{
    // =========================
    // WinAPI
    // =========================
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    public static string GetUserProfileName()
    {
        try
        {
            return Environment.UserName;
        }
        catch
        {
            return "user";
        }
    }
    
    // =========================
    // LINUX FORMAT CONVERSIONS
    // =========================

    /// <summary>
    /// Returns Linux IANA timezone (e.g. Europe/Vienna)
    /// </summary>
    public static string GetTimeZone()
    {
        try
        {
            string windowsTz = TimeZoneInfo.Local.Id;
            return TZConvert.WindowsToIana(windowsTz);
        }
        catch
        {
            return "UTC";
        }
    }

    /// <summary>
    /// Returns Linux locale format (e.g. de_AT.UTF-8)
    /// </summary>
    public static string GetLanguage()
    {
        try
        {
            var culture = CultureInfo.CurrentUICulture.Name; // e.g. de-DE

            var parts = culture.Split('-');
            if (parts.Length != 2)
                return "en_US.UTF-8";

            string lang = parts[0].ToLower();
            string region = parts[1].ToUpper();

            return $"{lang}_{region}.UTF-8";
        }
        catch
        {
            return "en_US.UTF-8";
        }
    }

    /// <summary>
    /// Returns Linux keyboard layout (us, de, uk, etc.)
    /// </summary>
    public static string GetKeyboardLayout()
    {
        try
        {
            IntPtr hkl = GetKeyboardLayout(0);
            int localeId = hkl.ToInt32() & 0xFFFF;

            string windowsLayout = new CultureInfo(localeId).Name; // e.g. en-US

            return windowsLayout.ToLower() switch
            {
                "en-us" => "us",
                "de-de" => "de",
                "de-at" => "de",
                "en-gb" => "uk",
                _ => "us"
            };
        }
        catch
        {
            return "us";
        }
    }

    // =========================
    // WIFI (UNCHANGED)
    // =========================

    public static string ExportWifiProfiles()
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "WifiProfilesExport"
            );

            Directory.CreateDirectory(folder);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"wlan export profile key=clear folder=\"{folder}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            return folder;
        }
        catch
        {
            return null;
        }
    }

    public static string GetCurrentWifiSSID()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("SSID") && !trimmed.StartsWith("BSSID"))
                {
                    return trimmed.Split(':')[1].Trim();
                }
            }
        }
        catch { }

        return null;
    }

    // =========================
    // UTILITY
    // =========================

    public static string GenerateUuid()
    {
        return Guid.NewGuid().ToString();
    }
}