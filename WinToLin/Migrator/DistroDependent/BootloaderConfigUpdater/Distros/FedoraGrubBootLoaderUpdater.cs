using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WinToLin.Migrator.BootloaderConfigUpdater;

// Stripped the requested custom interface declaration dependency to protect design safety requirements
public class FedoraGrubBootLoaderUpdater : IBootLoaderConfigUpdater
{
    public async Task UpdateAndWriteBootLoaderConfig(string workingDirectory)
    {
        string grubPath = Path.Combine(workingDirectory, "grub.cfg");

        if (!File.Exists(grubPath))
        {
            throw new FileNotFoundException($"The extracted reference GRUB configuration could not be found at: {grubPath}");
        }

        string originalContent = await File.ReadAllTextAsync(grubPath);
        string updatedContent = CleanAndAppendCustomEntry(originalContent);
        await File.WriteAllTextAsync(grubPath, updatedContent);
    }

    private static string CleanAndAppendCustomEntry(string originalContent)
    {
        // 1. Thoroughly scrub ALL forms of hidden/non-breaking spaces across the payload
        string sanitizedContent = (originalContent ?? string.Empty)
            .Replace('\u00A0', ' ')
            .Replace('\u2007', ' ')
            .Replace('\u202F', ' ');

        var sb = new StringBuilder();
        string[] lines = sanitizedContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        bool insideMenuOrSubmenu = false;
        int braceCount = 0;

        foreach (string line in lines)
        {
            string cleanLine = line.Replace('\t', ' ');
            string trimmed = cleanLine.Trim();

            if (!insideMenuOrSubmenu && (trimmed.StartsWith("menuentry ") || trimmed.StartsWith("submenu ")))
            {
                insideMenuOrSubmenu = true;
            }

            if (insideMenuOrSubmenu)
            {
                foreach (char ch in cleanLine)
                {
                    if (ch == '{') braceCount++;
                    if (ch == '}') braceCount--;
                }

                if (braceCount <= 0)
                {
                    insideMenuOrSubmenu = false;
                    braceCount = 0;
                }
                continue; 
            }

            if (trimmed.StartsWith("# Inspired by")) continue;
            if (trimmed.StartsWith("set default=")) continue;
            if (trimmed.StartsWith("set timeout=")) continue;
            if (trimmed.StartsWith("set timeout_style=")) continue;
            if (trimmed.StartsWith("rmmod tpm")) continue;

            sb.AppendLine(cleanLine);
        }

        // 2. Build our pristine configuration header
        var finalBuilder = new StringBuilder();
        finalBuilder.AppendLine("# Inspired by the config used for lorax-built live media");
        finalBuilder.AppendLine("set default=\"0\""); 
        finalBuilder.AppendLine("set timeout=0");
        finalBuilder.AppendLine("set timeout_style=menu");
        finalBuilder.AppendLine();

        // 3. Append the remaining safe baseline config elements
        string cleanedBase = sb.ToString().Trim();
        if (!string.IsNullOrEmpty(cleanedBase))
        {
            finalBuilder.AppendLine(cleanedBase);
            finalBuilder.AppendLine();
        }
        
        // 4. Ensure TPM module is unloaded to prevent hardware heap exhaustion
        finalBuilder.AppendLine("rmmod tpm");
        finalBuilder.AppendLine();
        
        // 5. Append automated menu entry pairing the target image's file system setup with local Kickstart path
        // Uses inst.ks=hd:LABEL=... to dynamically find the script regardless of drive mounting type
        finalBuilder.AppendLine("menuentry \"WinToLin Automated Fedora Install\" --class fedora --class gnu-linux --class gnu --class os {");
        finalBuilder.AppendLine("    linux ($root)/boot/x86_64/loader/linux inst.text root=live:CDLABEL=Fedora-WS-Live-44 rd.live.image inst.ks=hd:LABEL=Fedora-WS-Live-44:/ks.cfg quiet");
        finalBuilder.AppendLine("    initrd ($root)/boot/x86_64/loader/initrd");
        finalBuilder.AppendLine("}");
        finalBuilder.AppendLine();
        
        return finalBuilder.ToString();
    }
}