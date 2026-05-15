using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WinToLin.Migration;

public static class CompressUtil
{
    private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(10);

    public static async Task CompressAndMoveFilesAsync(string targetCompressedFilesDirPath, List<string> filePaths)
    {
        var tasks = filePaths
            .Where(Directory.Exists)
            .Select(async path => 
            {
                // Wait for a slot to become available
                await _throttle.WaitAsync();
                try
                {
                    string folderName = new DirectoryInfo(path).Name;
                    string safeName = GetSafeFilename(folderName);
                    string archivePath = Path.Combine(targetCompressedFilesDirPath, $"{safeName}.zip");
                    
                    await ZipDirectorySafelyAsync(path, archivePath);
                }
                finally
                {
                    // Always release the slot, even if an error occurs
                    _throttle.Release();
                }
            });

        // Run the throttled tasks
        await Task.WhenAll(tasks);
    }

    private static async Task ZipDirectorySafelyAsync(string sourceDir, string zipPath)
    {
        string? zipDirectory = Path.GetDirectoryName(zipPath);
        if (zipDirectory != null) Directory.CreateDirectory(zipDirectory);

        using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
        {
            DirectoryInfo di = new DirectoryInfo(sourceDir);
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var file in di.EnumerateFiles("*", options))
            {
                if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                    file.Name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    string entryName = Path.GetRelativePath(sourceDir, file.FullName);
                    ZipArchiveEntry entry = archive.CreateEntry(entryName);

                    using (Stream entryStream = entry.Open())
                    using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
                catch
                {
                    // Skip files that are locked or inaccessible
                    continue; 
                }
            }
        }
    }

    private static string GetSafeFilename(string filename)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            filename = filename.Replace(c, '_');
        }
        return filename.Replace(" ", "_");
    }
}