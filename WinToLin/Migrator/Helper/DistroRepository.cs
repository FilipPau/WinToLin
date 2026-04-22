using System.IO;
using System.Text.Json;
using WinToLin.Steps;

public static class DistroRepository
{
    public static List<Distro.LinuxDistro> Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<Distro.LinuxDistro>>(json);
    }

    public static Distro.LinuxDistro GetByName(string name)
    {
        var distros = Load(Path.Combine(Directory.GetCurrentDirectory(), "data", "distros.json"));
        return distros.Find(d => d.Name == name);
    }
}