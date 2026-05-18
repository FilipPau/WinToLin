using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;

namespace WinToLin.Views.Steps;

public partial class Migration : UserControl
{
    private readonly ConfigManager _configManager;
    private bool isRunning;
    private readonly List<Step> steps = new();

    public Migration()
    {
        InitializeComponent();
        _configManager = ConfigManager.Instance;

        steps.AddRange(new[]
        {
            new Step("Download & Compress"),
            new Step("Extract Config"),
            new Step("Inject Data"),
            new Step("Write USB")
        });

        StepsList.ItemsSource = steps;
        Loaded += Migration_Loaded;
    }

    public class Step
    {
        public string Name { get; }
        public bool IsActive { get; set; }
        public bool IsDone { get; set; }
        public Step(string name) => Name = name;
    }

    private async void Migrate_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning) return;
        isRunning = true;

        var migrator = Migrator.Migrator.Instance;

        // --- Subscribe to Events ---
        migrator.OnProgressChanged += p => Dispatcher.Invoke((Action)(() => MainProgress.Value = p));
        migrator.OnStatusChanged += s => Dispatcher.Invoke((Action)(() => ProgressText.Text = s));
        migrator.OnStepChanged += idx => Dispatcher.Invoke(() => {
            for (int i = 0; i < steps.Count; i++) {
                steps[i].IsActive = (i == idx);
                steps[i].IsDone = (i < idx);
            }
            RefreshSteps();
        });

        try
        {
            string workDir = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build");
            await migrator.RunPipeline(workDir, false);
            StepManager.Instance.MainTaskCompleted();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Migration Error: {ex.Message}");
        }
        finally
        {
            isRunning = false;
        }
    }

    private async void CreateISO_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning) return;

        // 1. Initialize the modern folder selection dialog
        Microsoft.Win32.OpenFolderDialog folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Destination Directory",
            InitialDirectory = Environment.CurrentDirectory
        };

        // 2. Show the dialog and check if the user selected a path
        if (folderDialog.ShowDialog() != true)
        {
            // User cancelled the directory picker selection
            return; 
        }

        // Save the chosen directory path into a variable
        string selectedDir = folderDialog.FolderName;
        isRunning = true;

        var migrator = Migrator.Migrator.Instance;

        // --- Subscribe to Events ---
        migrator.OnProgressChanged += p => Dispatcher.Invoke((Action)(() => MainProgress.Value = p));
        migrator.OnStatusChanged += s => Dispatcher.Invoke((Action)(() => ProgressText.Text = s));
        migrator.OnStepChanged += idx => Dispatcher.Invoke(() => {
            for (int i = 0; i < steps.Count; i++) {
                steps[i].IsActive = (i == idx);
                steps[i].IsDone = (i < idx);
            }
            RefreshSteps();
        });

        try
        {
            // Combine with your build folder name
            string workDir = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build");
        
            // 3. Run the pipeline asynchronously, passing the chosen directory
            await migrator.RunPipeline(workDir, true, selectedDir);
        
            StepManager.Instance.MainTaskCompleted();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Migration Error: {ex.Message}");
        }
        finally
        {
            isRunning = false;
        }
    }
    private void RefreshSteps()
    {
        StepsList.ItemsSource = null;
        StepsList.ItemsSource = steps;
    }

    private async void Migration_Loaded(object sender, RoutedEventArgs e)
    {
        ProgressText.Text = "Loading system info...";
        await Task.Run(LoadStorageInfoLive);
        Dispatcher.Invoke(() => { ProgressText.Text = "Ready"; });
    }

    // Storage logic remains the same...
    private void LoadStorageInfoLive()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_configManager.InstalationUSBLetter)) return;
            string root = _configManager.InstalationUSBLetter.TrimEnd(':') + ":\\";
            var drive = new DriveInfo(root);
            double total = drive.TotalSize / (1024.0 * 1024 * 1024);
            double iso = EstimateIsoSize();
            long backupBytes = 0;
            int fileCounter = 0;

            foreach (var (path, _) in _configManager.BackupPaths.ToList())
            {
                if (!Directory.Exists(path)) continue;
                var stack = new Stack<string>();
                stack.Push(path);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    try {
                        foreach (var file in Directory.EnumerateFiles(current)) {
                            backupBytes += new FileInfo(file).Length;
                            fileCounter++;
                            if (fileCounter % 50 == 0) UpdateStorageUI(backupBytes, iso, total);
                        }
                        foreach (var dir in Directory.EnumerateDirectories(current)) stack.Push(dir);
                    } catch { }
                }
            }
            UpdateStorageUI(backupBytes, iso, total);
        } catch { }
    }

    private void UpdateStorageUI(long backupBytes, double iso, double total)
    {
        double backupGb = backupBytes / (1024.0 * 1024 * 1024);
        double free = Math.Max(0, total - (backupGb + iso));
        Dispatcher.Invoke(() => {
            SetColumn(IsoColumn, iso, total);
            SetColumn(BackupColumn, backupGb, total);
            SetColumn(FreeColumn, free, total);
            IsoText.Text = $"ISO {iso:F1}GB";
            BackupText.Text = $"Backup {backupGb:F1}GB";
            FreeText.Text = $"Free {free:F1}GB";
        });
    }

    private double EstimateIsoSize()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "WinToLin_Build", "distro.iso");
        return File.Exists(path) ? new FileInfo(path).Length / (1024.0 * 1024 * 1024) : 3.0;
    }

    private void SetColumn(ColumnDefinition col, double value, double total) =>
        col.Width = new GridLength(total <= 0 ? 0 : value / total, GridUnitType.Star);
}