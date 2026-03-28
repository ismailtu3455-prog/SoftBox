namespace SoftBoxLauncher.Services;

public static class CrashLogger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoftBoxLauncher",
        "Logs");

    public static string Log(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var logPath = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyy-MM-dd}.log");

            var payload =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Source: {source}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}" +
                new string('-', 80) +
                Environment.NewLine;

            File.AppendAllText(logPath, payload);
            return logPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
