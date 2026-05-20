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
        @"set timeout=0
set default=0

menuentry ""WinToLin Automated Install"" {
    set gfxpayload=keep
    linux /casper/vmlinuz autoinstall ds=nocloud\;seedfrom=/cdrom/preseed/ --- quiet splash
    initrd /casper/initrd
}";
}