using System.Diagnostics;

namespace WinToLin.Migration;

public static class UsbWriterUtil
{
    public static void Write(string isoPath, string usbDevice)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "dd",
            Arguments = $"if={isoPath} of={usbDevice} bs=4M status=progress conv=fsync",
            UseShellExecute = false
        })?.WaitForExit();
    }
}