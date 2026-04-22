using System.IO;

namespace WinToLin.Migration;

public static class ScriptBuilder
{
    public static void CreatePostInstall(string usbRoot)
    {
        string script =
            """
            #!/bin/bash
            echo "Restoring files..."

            cp -r /cdrom/WinToLinBackup/data/* /home/user/

            echo "Done"
            """;

        File.WriteAllText(Path.Combine(usbRoot, "scripts", "post-install.sh"), script);
    }

    public static void CreateMasterInstaller(string usbRoot)
    {
        string script =
            """
            #!/bin/bash
            echo "WinToLin USB booted"
            echo "Starting automated install..."
            """;

        File.WriteAllText(Path.Combine(usbRoot, "install.sh"), script);
    }
}