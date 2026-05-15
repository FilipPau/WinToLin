using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinToLin.Logic.Enums;
using WinToLin.logic.manager;
using WinToLin.Migration;
using WinToLin.Migrator.BootloaderConfigUpdater;
using WinToLin.Migrator.InstallScriptCreators;
using WinToLin.Migrator.ISOInjectors;

namespace WinToLin.Migrator;

public class Migrator
{
    private static Migrator? _instance;
    private static readonly object _lock = new object();
    private ConfigManager _configManager;
    private Dictionary<Distros, BootLoaders> _distroToBootLoader;

    // --- Events for UI Binding ---
    public event Action<double>? OnProgressChanged;
    public event Action<string>? OnStatusChanged;
    public event Action<int>? OnStepChanged;

    private Migrator()
    {
        _configManager = ConfigManager.Instance;
        _distroToBootLoader = new Dictionary<Distros, BootLoaders>()
        {
            [Distros.UBUNTU] = BootLoaders.GRUB,
            [Distros.FEDORA] = BootLoaders.GRUB,
            [Distros.MINT] = BootLoaders.GRUB,
        };
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

    public async Task RunPipeline(string workDir)
    {
        string isoPath = Path.Combine(workDir, "distro.iso");
        string compressedFilesPath = Path.Combine(workDir, "migration_archives");
        var distroName = _configManager.DistroName;

        Directory.CreateDirectory(workDir);

        // STEP 0: Download & Compression
        OnStepChanged?.Invoke(0);
        OnStatusChanged?.Invoke("Downloading ISO and Compressing Files...");
        
        var downloadProgress = new Progress<double>(p => OnProgressChanged?.Invoke(p));

        // Use Task.WhenAll for async non-blocking execution
        await Task.WhenAll(new Task[]
        {
            IsoDownloader.DownloadAsync(distroName, isoPath, downloadProgress),
            CompressUtil.CompressAndMoveFilesAsync(compressedFilesPath, 
                _configManager.BackupPaths.Select(x => x.Key).ToList())
        });

        // STEP 1: Extract Config
        OnStepChanged?.Invoke(1);
        OnStatusChanged?.Invoke("Extracting Bootloader Config...");
        
        switch (_distroToBootLoader[distroName])
        {
            case BootLoaders.GRUB:
                var extractProgress = new Progress<double>(p => OnProgressChanged?.Invoke(p));
                await IsoExtractGrubConfigUtil.ExtractAsync(isoPath, workDir, extractProgress);
                break;
            default:
                throw new NotSupportedException($"Bootloader for {distroName} not supported.");
        }

        // STEP 2: Injection Preparation
        OnStepChanged?.Invoke(2);
        OnStatusChanged?.Invoke("Injecting Config & Migration Scripts...");
        
        
        var bootLoaderConfigUpdater = BootLoaderConfigUpdaterFactory.CreateBootLoaderConfigUpdater(_distroToBootLoader[distroName]);
        bootLoaderConfigUpdater.UpdateAndWriteBootLoaderConfig(workDir);
        

        var installScsriptWriter = InstallScriptFactory.CreateInstallScriptWriter(distroName);
        installScsriptWriter.CreateAndWriteMigrationScripts(workDir);

        // STEP 3: USB/ISO Finalization
        OnStepChanged?.Invoke(3);
        OnStatusChanged?.Invoke("Creating Final Bootable ISO...");
        
        var isoInjector = ISOInjectorFactory.CreateISOInjector(distroName);
        string outputIso = Path.Combine(workDir, "wintolin.iso");
        string xorrisoPath = "xorriso";

        // If Inject supports progress, pass it here; otherwise, manual completion
        await Task.Run(() => isoInjector.Inject(isoPath, outputIso, xorrisoPath));
        
        OnProgressChanged?.Invoke(100);
        OnStatusChanged?.Invoke("Migration Ready!");
    }
}