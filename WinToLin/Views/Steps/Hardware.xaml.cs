using System.IO;
using System.Management;
using System.Windows.Controls;
using SharpGen.Runtime;
using Vortice.DXGI;
using WinToLin.Logic.Manager;

namespace WinToLin.Views.Steps;

public partial class Hardware : UserControl
{
    private ConfigManager _configManager;

    public Hardware()
    {
        InitializeComponent();
        _configManager = ConfigManager.Instance;
        
        LoadHardwareInfo();
    }

    private void LoadHardwareInfo()
    {
        var gpus = ShowAndGetGpus();
        var nics = LoadNetworkAdapters();
        var drivesAndOsDrive = LoadDisks();
        
        _configManager.AddGPUs(gpus);
        _configManager.AddNICs(nics);
        _configManager.AddDrives(drivesAndOsDrive.allDisks);
        _configManager.SetOsDrive(drivesAndOsDrive.osDrive);
    }

    // ----------------------- GPU -----------------------
    private List<string> ShowAndGetGpus()
    {
        var gpus = new List<GpuInfo>();

        IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        for (int i = 0;; i++)
        {
            IDXGIAdapter1 adapter;
            Result result = factory.EnumAdapters1((uint)i, out adapter);
            if (result.Failure)
                break;

            AdapterDescription1 desc = adapter.Description1;

            // Skip software adapters, virtual adapters, and low VRAM
            ulong vram = (ulong)desc.DedicatedVideoMemory;
            if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.Software &&
                vram > 16 * 1024 * 1024 &&
                desc.VendorId != 0x1414) // skip Microsoft Basic Display Adapter
            {
                // Prevent duplicates by checking existing names
                if (!gpus.Exists(g => g.Name == desc.Description.Trim()))
                {
                    gpus.Add(new GpuInfo
                    {
                        Name = desc.Description.Trim(),
                        Compatibility = GPUCompatiblityCheck(desc.Description.Trim())
                    });
                }
            }
        }

        GpuList.ItemsSource = gpus;
        
        
        return gpus.Select(gpuInfo =>  gpuInfo.Name).ToList();
    }

    public class GpuInfo
    {
        public string Name { get; set; }
        public string Compatibility { get; set; }
    }

    // ----------------------- Network Adapters -----------------------
    private List<string> LoadNetworkAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();

        string query =
            "SELECT Name, Description, NetConnectionStatus FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE";
        using (var searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                var statusObj = mo["NetConnectionStatus"];
                if (statusObj != null)
                {

                    if (mo["Name"] is null || mo["Name"].ToString().Contains("Virtual"))
                    {
                        continue;
                    }
                    
                    uint status = Convert.ToUInt32(statusObj);
                    if (status == 2) // 2 = connected
                    {
                        adapters.Add(new NetworkAdapterInfo
                        {
                            Name = mo["Name"]?.ToString(),
                            Description = mo["Description"]?.ToString(),
                            Compatibility = "to be filled later"
                        });
                    }
                }
            }
        }

        NetworkList.ItemsSource = adapters;
        
        return adapters.Select(a => a.Name).ToList();
    }

    private string GPUCompatiblityCheck(string gpuName)
    {
        return gpuName switch
        {
            string name when name.Contains("amd", StringComparison.OrdinalIgnoreCase) => "Compatible",
            string name when name.Contains("nvidia", StringComparison.OrdinalIgnoreCase) => "Compatible with work to do (todo)",
            _ => "Unknown GPU"
        };
    }
    
    public class NetworkAdapterInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Compatibility { get; set; }
    }

    // ----------------------- Disks -----------------------
    private (List<string> allDisks, string osDrive) LoadDisks()
    {
        var disks = new List<DiskInfo>();

        string osDrive = "undefined";
        
        try
        {
            // 1. Get Physical Disks
            using var diskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            string? systemDrive = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.System)
            )?.Replace("\\", "");
            
            foreach (ManagementObject disk in diskSearcher.Get())
            {
                string deviceId = disk["DeviceID"].ToString(); // e.g., "\\.\PHYSICALDRIVE0"
                var driveLetters = new List<string>();

                // 2. Use an Associator query to find Partitions linked to this Disk
                // This replaces your manual string parsing and loops
                string partitionQuery =
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);

                foreach (ManagementObject partition in partitionSearcher.Get())
                {
                    string partitionId = partition["DeviceID"].ToString(); // e.g., "Disk #0, Partition #1"

                    // 3. Use an Associator query to find Logical Disks (C:, D:, etc.) linked to the Partition
                    string logicalQuery =
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";
                    using var logicalSearcher = new ManagementObjectSearcher(logicalQuery);

                    foreach (ManagementObject logical in logicalSearcher.Get())
                    {
                        driveLetters.Add(logical["DeviceID"].ToString());
                    }
                }

                if (disk["MediaType"] .ToString() == "Removable Media")
                {
                    continue;
                }

                if (driveLetters.Any(dl => 
                        dl.Equals(systemDrive, StringComparison.OrdinalIgnoreCase)))
                {
                    osDrive = string.Join(", ", driveLetters.Select(d => d.Replace(":", "")));
                }
                
                disks.Add(new DiskInfo
                {
                    Model = disk["Model"]?.ToString() ?? "Unknown",
                    Type = disk["MediaType"] .ToString() ?? "Disk",
                    Size = disk["Size"] != null
                        ? $"Space: {((ulong)disk["Size"] / 1073741824.0):F2} GB"
                        : "Unknown",
                    DriveLetters = driveLetters.Any() 
                        ? "Letter: " + string.Join(", ", driveLetters.Select(d => d.Replace(":", ""))) 
                        : "No drive letter",
                    OSDrive = driveLetters.Any(dl => 
                        dl.Equals(systemDrive, StringComparison.OrdinalIgnoreCase)),
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading disks: {ex.Message}");
        }

        DisksList.ItemsSource = disks;

        return (disks.Select(x => x.Model).ToList(), osDrive);
    }

    public class DiskInfo
    {
        public string Model { get; set; }
        public string Type { get; set; }
        public string DriveLetters { get; set; }
        public string Size { get; set; }
        
        public bool OSDrive { get; set; }
    }
}

