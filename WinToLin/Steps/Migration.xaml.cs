using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinToLin.Migration;

namespace WinToLin.Steps;

public partial class Migration : UserControl
{
    private readonly Manager manager;
    private bool isRunning;

    private readonly List<Step> steps = new();
    private int currentStep = -1;

    public Migration()
    {
        InitializeComponent();
        manager = Manager.Instance;

        steps.AddRange(new[]
        {
            new Step("Download ISO"),
            new Step("Extract ISO"),
            new Step("Inject Config & Data"),
            new Step("Write USB")
        });

        StepsList.ItemsSource = steps;

        Loaded += Migration_Loaded;
    }

    // =========================
    // STEP MODEL
    // =========================
    public class Step
    {
        public string Name { get; }
        public bool IsActive { get; set; }
        public bool IsDone { get; set; }

        public Step(string name)
        {
            Name = name;
        }
    }

    // =========================
    // LOADED (NON-BLOCKING)
    // =========================
    private async void Migration_Loaded(object sender, RoutedEventArgs e)
    {
        ProgressText.Text = "Loading system info...";

        await Task.Run(LoadStorageInfoLive);

        Dispatcher.Invoke(() => { ProgressText.Text = "Ready"; });
    }

    // =========================
    // STORAGE (LIVE UPDATE)
    // =========================
    private void LoadStorageInfoLive()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(manager.InstalationUSBLetter))
                return;

            string root = manager.InstalationUSBLetter.TrimEnd(':') + ":\\";
            var drive = new DriveInfo(root);

            double total = drive.TotalSize / (1024.0 * 1024 * 1024);
            double iso = EstimateIsoSize();

            long backupBytes = 0;
            int fileCounter = 0;

            var paths = manager.BackupPaths.ToList();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                    continue;

                var stack = new Stack<string>();
                stack.Push(path);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();

                    // =========================
                    // FILES (SAFE)
                    // =========================
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(current))
                        {
                            try
                            {
                                backupBytes += new FileInfo(file).Length;
                            }
                            catch
                            {
                            }

                            fileCounter++;

                            // LIVE UI UPDATE
                            if (fileCounter % 50 == 0)
                            {
                                double backupGb = backupBytes / (1024.0 * 1024 * 1024);
                                double used = backupGb + iso;
                                double free = Math.Max(0, total - used);

                                Dispatcher.Invoke(() =>
                                {
                                    SetColumn(IsoColumn, iso, total);
                                    SetColumn(BackupColumn, backupGb, total);
                                    SetColumn(FreeColumn, free, total);

                                    IsoText.Text = $"ISO {iso:F1}GB";
                                    BackupText.Text = $"Backup {backupGb:F1}GB";
                                    FreeText.Text = $"Free {free:F1}GB";
                                });
                            }
                        }
                    }
                    catch
                    {
                        // skip folder we cannot read
                    }

                    // =========================
                    // SUBDIRECTORIES (SAFE)
                    // =========================
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(current))
                        {
                            stack.Push(dir);
                        }
                    }
                    catch
                    {
                        // skip inaccessible directory
                    }
                }
            }

            // =========================
            // FINAL UPDATE
            // =========================
            double finalBackup = backupBytes / (1024.0 * 1024 * 1024);
            double finalUsed = finalBackup + iso;
            double finalFree = Math.Max(0, total - finalUsed);

            Dispatcher.Invoke(() =>
            {
                SetColumn(IsoColumn, iso, total);
                SetColumn(BackupColumn, finalBackup, total);
                SetColumn(FreeColumn, finalFree, total);

                IsoText.Text = $"ISO {iso:F1}GB";
                BackupText.Text = $"Backup {finalBackup:F1}GB";
                FreeText.Text = $"Free {finalFree:F1}GB";
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    // =========================
    // ISO SIZE
    // =========================
    private double EstimateIsoSize()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build", "distro.iso");

        return File.Exists(path)
            ? new FileInfo(path).Length / (1024.0 * 1024 * 1024)
            : 3.0;
    }

    // =========================
    // MIGRATION
    // =========================
    private async void Migrate_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning) return;
        isRunning = true;

        try
        {
            string workDir = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build");
            
            string isoPath = Path.Combine(workDir, "distro.iso");

            Directory.CreateDirectory(workDir);

            await RunPipeline(workDir, isoPath);
        }
        finally
        {
            isRunning = false;
        }
    }

    // =========================
    // PIPELINE
    // =========================
    private async Task RunPipeline(string workDir, string isoPath)
    {
        await RunStep(0, async progress =>
        {
            SetProgress("Downloading ISO...");
            await IsoDownloader.DownloadAsync(manager.DistroName, isoPath, progress);
        });


        //ab hier hängt es von der Distro ab

        switch (manager.DistroName.ToLower())
        {
            case "ubuntu":
                await RunStep(1, async progress =>
                {
                    SetProgress("Extracting ISO...");
                    await IsoExtractor.ExtractAsync(isoPath,  workDir, progress);
                });

                await RunStep(2, async _ =>
                {
                    SetProgress("Injecting config & data...");
                    await UbuntuProvider.GenerateAsync(isoPath, workDir);
                });
                
/*
                await RunStep(3, async _ =>
                {
                    SetProgress("Writing USB...");
                    string usb = manager.InstalationUSBLetter.TrimEnd(':') + ":\\";
                    UsbWriter.Write(targetDir, usb);
                    await Task.CompletedTask;
                });*/
                break;
        }


        SetProgress("Done!");
        MainProgress.Value = 100;
    }

    // =========================
    // STEP SYSTEM
    // =========================
    private async Task RunStep(int index, Func<IProgress<double>, Task> action)
    {
        currentStep = index;

        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].IsActive = i == index;
            steps[i].IsDone = i < index;
        }

        RefreshSteps();

        var progress = new Progress<double>(p => { Dispatcher.Invoke(() => MainProgress.Value = p); });

        await action(progress);

        steps[index].IsDone = true;
        steps[index].IsActive = false;

        RefreshSteps();
    }

    private void RefreshSteps()
    {
        StepsList.ItemsSource = null;
        StepsList.ItemsSource = steps;
    }

    private void SetProgress(string text)
    {
        ProgressText.Text = text;
    }

    private void SetColumn(ColumnDefinition col, double value, double total)
    {
        col.Width = new GridLength(total <= 0 ? 0 : value / total, GridUnitType.Star);
    }
}