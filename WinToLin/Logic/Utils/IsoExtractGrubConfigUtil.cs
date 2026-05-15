using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WinToLin.Migration
{
    public static class IsoExtractGrubConfigUtil
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
    }
}