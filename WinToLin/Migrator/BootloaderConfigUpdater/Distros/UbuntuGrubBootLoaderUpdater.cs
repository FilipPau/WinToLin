using System.IO;

namespace WinToLin.Migrator.BootloaderConfigUpdater;

public class UbuntuGrubBootLoaderUpdater : IBootLoaderConfigUpdater
{
    public async Task UpdateAndWriteBootLoaderConfig(string workingDirectory)
    {
        string grubPath = Path.Combine(workingDirectory, "grub.cfg");

        await File.WriteAllTextAsync(grubPath, GenerateGrubContent());
    }

    private static string GenerateGrubContent() =>
        "set timeout=0\nset default=0\n\nmenuentry \"WinToLin Automated Install\" {\n    set gfxpayload=keep\n    linux /casper/vmlinuz autoinstall ds=nocloud\\;seedfrom=/cdrom/preseed/ --- quiet splash\n    initrd /casper/initrd\n}";
}