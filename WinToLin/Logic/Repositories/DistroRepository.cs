using System.IO;
using System.Text.Json;
using WinToLin.Logic.Enums;
using WinToLin.Steps;

namespace WinToLin.Logic.Repositories;

public static class DistroRepository
{
    public static List<LinuxDistro> Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<LinuxDistro>>(json);
    }

    public static LinuxDistro GetByName(Distros distro)
    {
        //Path.Combine(Directory.GetCurrentDirectory(), 
        var distros = Load(Path.Combine(Directory.GetCurrentDirectory(), "data", "distros.json"));
        return distros.Find(d => d.Distro == distro);
    }
}