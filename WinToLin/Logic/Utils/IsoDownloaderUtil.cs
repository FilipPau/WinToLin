using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using WinToLin.Logic.Enums;
using WinToLin.Steps;

namespace WinToLin.Migration;

public static class IsoDownloader
{
    private static readonly HttpClient client = new HttpClient();

    static IsoDownloader()
    {
        client.Timeout = TimeSpan.FromHours(2);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 WinToLinInstaller"
        );
    }

    public static async Task<string> DownloadAsync(
        Distros distroName,
        string outputPath,
        IProgress<double>? progress = null)
    {
        var distro = DistroRepository.GetByName(distroName);

        if (distro == null)
            throw new Exception("Distro not found");

        if (string.IsNullOrWhiteSpace(distro.DownloadUrl))
            throw new Exception("DownloadUrl is empty");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (File.Exists(outputPath))
        {
            var existingFile = new FileInfo(outputPath);

            
            // simple validity check (ISO files are large)
            if (existingFile.Length > 100_000_000)
            {
                Console.WriteLine("✔ Using existing ISO: " + outputPath);
                progress?.Report(100);
                return outputPath;
            }

            // too small → likely broken download
            Console.WriteLine("⚠ Existing file too small, re-downloading...");
            File.Delete(outputPath);
        }

        using var response = await client.GetAsync(
            distro.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? -1;
        long downloaded = 0;

        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            useAsync: true
        );

        byte[] buffer = new byte[1024 * 128];

        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloaded += bytesRead;

            if (total > 0)
            {
                double percent = (double)downloaded / total * 100.0;
                progress?.Report(percent);
            }
        }

        long size = new FileInfo(outputPath).Length;

        if (size < 100_000_000)
            throw new Exception("Downloaded file is too small → likely not an ISO");

        return outputPath;
    }
}