using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WinToLin.Migration
{
    public static class IsoExtractor
    {
        /// <summary>
        /// Extracts only the necessary boot files (grub.cfg) from the ISO to a staging directory.
        /// </summary>
        public static async Task ExtractAsync(
            string isoPath,
            string workDir,
            IProgress<double>? progress = null)
        {
            try
            {
                progress?.Report(10);

                // 2. Try to extract using the most common path (lowercase)
                await RunXorrisoExtraction(isoPath, "/boot/grub/grub.cfg", "WinToLin_Build/grub.cfg");

                progress?.Report(100);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Extraction Error:\n\n" + ex.Message);
            }
        }

        private static async Task<bool> RunXorrisoExtraction(string isoPath, string internalPath, string localDest)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xorriso",
                Arguments = $"-osirrox on " +
                            $"-indev \"{isoPath}\" " +
                            $"-chmod 0777 \"{internalPath}\" -- " +
                            $"-extract \"{internalPath}\" \"{localDest}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        /// <summary>
        /// Sledgehammer approach to ensure the files extracted to Windows are modifiable.
        /// </summary>
        private static void GrantWriteAccess(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            var dInfo = new DirectoryInfo(directoryPath);
            dInfo.Attributes = FileAttributes.Normal;

            foreach (var file in dInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file.FullName, FileAttributes.Normal);
                }
                catch
                {
                    /* Ignore files that might be locked */
                }
            }

            foreach (var subDir in dInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                subDir.Attributes = FileAttributes.Normal;
            }
        }

        // Helper for progress if you decide to add more files later
        private static double ParseSizeToMB(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Trim().ToLowerInvariant();
            try
            {
                double value = double.Parse(input[..^1], CultureInfo.InvariantCulture);
                return input[^1] switch
                {
                    'k' => value / 1024.0,
                    'm' => value,
                    'g' => value * 1024.0,
                    _ => value
                };
            }
            catch
            {
                return 0;
            }
        }
    }
}