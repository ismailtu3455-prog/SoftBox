using System.Diagnostics;

namespace SoftBoxLauncher.Services;

public static class SelfUpdateBootstrapper
{
    public static bool TryHandleArguments(string[] args)
    {
        if (args.Length < 3 || !args[0].Equals("--update", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var oldExePath = args[1];
        var newExePath = args[2];

        try
        {
            ReplaceExecutableWithRetry(oldExePath, newExePath, maxAttempts: 40, delayMs: 300);
            StartUpdatedLauncher(oldExePath);
        }
        catch
        {
            // No UI here: updater mode should exit silently on failures.
        }

        return true;
    }

    private static void ReplaceExecutableWithRetry(string destinationExePath, string sourceExePath, int maxAttempts, int delayMs)
    {
        if (!File.Exists(sourceExePath))
        {
            throw new FileNotFoundException("Downloaded update executable not found.", sourceExePath);
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Copy(sourceExePath, destinationExePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }

        throw new IOException("Unable to replace launcher executable after multiple retries.");
    }

    private static void StartUpdatedLauncher(string executablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        };

        Process.Start(startInfo);
    }
}

