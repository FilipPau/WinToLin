using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinToLin.Logic.Enums;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;
using WinToLin.Migration;
using WinToLin.Migrator.BootloaderConfigUpdater;
using WinToLin.Migrator.InstallScriptCreators;
using WinToLin.Migrator.ISOBootloaderConfigExtractor;
using WinToLin.Migrator.ISOInjectors;
using WinToLin.Migrator.ToolKit;

namespace WinToLin.Migrator;

public class Migrator
{
    private static Migrator? _instance;
    private static readonly object _lock = new object();
    private ConfigManager _configManager;

    // --- Events for UI Binding ---
    public event Action<double>? OnProgressChanged;
    public event Action<string>? OnStatusChanged;
    public event Action<int>? OnStepChanged;

    private Migrator()
    {
        _configManager = ConfigManager.Instance;
    }

    public static Migrator Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new Migrator();
                return _instance;
            }
        }
    }

    public async Task RunPipeline(string workDir, bool onlyCreateIso, string? outputIsoDirPath = null)
    {
        string isoPath = Path.Combine(workDir, "distro.iso");
        string compressedFilesPath = Path.Combine(workDir, "migration_archives");
        var distro = _configManager.DistroName;

        var toolkit = TransferToolKitFactory.CreateTransferToolKit(distro);

        Directory.CreateDirectory(workDir);

        // STEP 0: Download & Compression
        OnStepChanged?.Invoke(0);
        OnStatusChanged?.Invoke("Downloading ISO and Compressing Files...");

        var downloadProgress = new Progress<double>(p => OnProgressChanged?.Invoke(p));

        // Use Task.WhenAll for async non-blocking execution
        await Task.WhenAll(new Task[]
        {
            IsoDownloader.DownloadAsync(distro, isoPath, downloadProgress),

            //auto detect files and folder to keep automaticaly which means all of them, important mate
            CompressUtil.CompressAndMoveFilesAsync(compressedFilesPath,
                _configManager.BackupPaths.Select(x => x.Key).ToList())
        });

        // STEP 1: Extract Config
        OnStepChanged?.Invoke(1);
        OnStatusChanged?.Invoke("Extracting Bootloader Config...");

        await toolkit.BootLoaderConfigExtractor.ExtractAsync(isoPath);

        // STEP 2: Injection Preparation
        OnStepChanged?.Invoke(2);
        OnStatusChanged?.Invoke("Injecting Config & Migration Scripts...");

        toolkit.BootLoaderConfigUpdater.UpdateAndWriteBootLoaderConfig(workDir);

        toolkit.InstallScriptWriter.CreateAndWriteMigrationScripts(workDir);

        // STEP 3: USB/ISO Finalization
        OnStepChanged?.Invoke(3);
        OnStatusChanged?.Invoke("Creating Final Bootable ISO...");
        
        
        
        string outputIso = Path.Combine(workDir, "wintolin.iso");

        string xorrisoPath = "xorriso";

        // If Inject supports progress, pass it here; otherwise, manual completion
        await Task.Run(() => toolkit.IsoInjector.Inject(isoPath, outputIsoDirPath is not null ? Path.Combine(outputIsoDirPath, "wintolin.iso") : outputIso, xorrisoPath));

        OnProgressChanged?.Invoke(100);
        OnStatusChanged?.Invoke("Migration Ready!");

        if (onlyCreateIso)
            return;

        //make a bootable usb
    }
}