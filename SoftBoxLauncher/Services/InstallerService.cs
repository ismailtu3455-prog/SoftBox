using System.Diagnostics;
using System.IO.Compression;
using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public sealed class InstallerService : IInstallerService
{
    private readonly string _desktopPath;
    private readonly string _appsRoot;

    public InstallerService()
    {
        _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _appsRoot = Path.Combine(_desktopPath, "SoftBoxApps");
    }

    public string GetInstallMarkerPath(AppItem app)
    {
        var knownMarker = GetKnownInstalledMarkerPath(app);
        if (!string.IsNullOrWhiteSpace(knownMarker))
        {
            return knownMarker;
        }

        return GetFallbackMarkerPath(app);
    }

    public bool IsInstalled(AppItem app)
    {
        var knownMarker = GetKnownInstalledMarkerPath(app);
        if (!string.IsNullOrWhiteSpace(knownMarker) && PathExists(knownMarker))
        {
            return true;
        }

        return PathExists(GetFallbackMarkerPath(app));
    }

    public async Task<InstallResult> InstallAsync(AppItem app, string downloadedPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_appsRoot);

        var appKey = ResolveAppKey(app.AssetName);

        return appKey switch
        {
            KnownAppKey.DlssUpdater => await InstallDlssUpdaterAsync(downloadedPath, cancellationToken),
            KnownAppKey.WallpaperEngine => await InstallWallpaperEngineAsync(app, downloadedPath, cancellationToken),
            KnownAppKey.WinScript => await InstallWinScriptAsync(app, downloadedPath, cancellationToken),
            _ => await InstallDefaultAsync(app, downloadedPath, cancellationToken)
        };
    }

    public Task<string?> LaunchInstalledAsync(AppItem app, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var knownRunPath = GetKnownRunPath(app);
        if (!string.IsNullOrWhiteSpace(knownRunPath) && PathExists(knownRunPath))
        {
            Launch(knownRunPath);
            return Task.FromResult<string?>(knownRunPath);
        }

        var fallbackRunPath = ResolveFallbackRunPath(app);
        if (!string.IsNullOrWhiteSpace(fallbackRunPath) && PathExists(fallbackRunPath))
        {
            Launch(fallbackRunPath);
            return Task.FromResult<string?>(fallbackRunPath);
        }

        return Task.FromResult<string?>(null);
    }

    private async Task<InstallResult> InstallDefaultAsync(AppItem app, string downloadedPath, CancellationToken cancellationToken)
    {
        if (IsZip(app))
        {
            var appDir = GetAppDirectory(app);
            Directory.CreateDirectory(appDir);

            await Task.Run(() => ZipFile.ExtractToDirectory(downloadedPath, appDir, overwriteFiles: true), cancellationToken);

            var exeToLaunch = Directory
                .GetFiles(appDir, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(exeToLaunch))
            {
                Launch(exeToLaunch);
            }

            return new InstallResult
            {
                InstallMarkerPath = appDir,
                LaunchedExecutablePath = exeToLaunch
            };
        }

        // EXE installers (7-zip, WinRAR, etc.) are launched directly.
        Launch(downloadedPath);

        return new InstallResult
        {
            InstallMarkerPath = GetInstallMarkerPath(app),
            LaunchedExecutablePath = downloadedPath
        };
    }

    private Task<InstallResult> InstallDlssUpdaterAsync(string downloadedPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var preferredPath = @"C:\DLSS_Updater\DLSS_Updater.exe";
        var fallbackPath = Path.Combine(_appsRoot, "DLSS Updater", "DLSS_Updater.exe");
        var finalPath = preferredPath;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferredPath)!);
            File.Copy(downloadedPath, preferredPath, overwrite: true);
        }
        catch (UnauthorizedAccessException)
        {
            finalPath = fallbackPath;
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            File.Copy(downloadedPath, finalPath, overwrite: true);
        }

        CreateDesktopShortcut(finalPath, "DLSS Updater");
        Launch(finalPath);

        return Task.FromResult(new InstallResult
        {
            InstallMarkerPath = finalPath,
            LaunchedExecutablePath = finalPath
        });
    }

    private async Task<InstallResult> InstallWallpaperEngineAsync(AppItem app, string downloadedPath, CancellationToken cancellationToken)
    {
        var appDir = GetAppDirectory(app);
        Directory.CreateDirectory(appDir);

        await Task.Run(() => ZipFile.ExtractToDirectory(downloadedPath, appDir, overwriteFiles: true), cancellationToken);

        // By request: extract folder content from Wallpaper_Engine.zip to Desktop too.
        foreach (var topLevelDir in Directory.GetDirectories(appDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destination = Path.Combine(_desktopPath, Path.GetFileName(topLevelDir));
            CopyDirectory(topLevelDir, destination);
        }

        var exeToLaunch = FindPreferredExecutable(appDir, "launcher32.exe")
                          ?? Directory.GetFiles(appDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(exeToLaunch))
        {
            Launch(exeToLaunch);
        }

        return new InstallResult
        {
            InstallMarkerPath = GetKnownInstalledMarkerPath(app) ?? appDir,
            LaunchedExecutablePath = exeToLaunch
        };
    }

    private async Task<InstallResult> InstallWinScriptAsync(AppItem app, string downloadedPath, CancellationToken cancellationToken)
    {
        var appDir = GetAppDirectory(app);
        Directory.CreateDirectory(appDir);

        await Task.Run(() => ZipFile.ExtractToDirectory(downloadedPath, appDir, overwriteFiles: true), cancellationToken);

        // By request: copy .json files from winscript.zip to Desktop.
        foreach (var jsonPath in Directory.GetFiles(appDir, "*.json", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(_desktopPath, Path.GetFileName(jsonPath));
            File.Copy(jsonPath, destination, overwrite: true);
        }

        var exeToLaunch = FindPreferredExecutable(appDir, "winscript.exe")
                          ?? Directory.GetFiles(appDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(exeToLaunch))
        {
            Launch(exeToLaunch);
        }

        return new InstallResult
        {
            InstallMarkerPath = GetKnownInstalledMarkerPath(app) ?? appDir,
            LaunchedExecutablePath = exeToLaunch
        };
    }

    private string? GetKnownInstalledMarkerPath(AppItem app)
    {
        return ResolveAppKey(app.AssetName) switch
        {
            KnownAppKey.SevenZip => @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip",
            KnownAppKey.DlssUpdater => @"C:\DLSS_Updater\DLSS_Updater.exe",
            KnownAppKey.UniversalMediaPlayer => @"C:\Program Files\Universal Media Player\Universal Media Player.exe",
            KnownAppKey.WallpaperEngine => @"C:\Program Files\Wallpaper Engine\launcher32.exe",
            KnownAppKey.Windhawk => @"C:\Program Files\Windhawk\windhawk.exe",
            KnownAppKey.WinRar => @"C:\Program Files\WinRAR\WinRAR.exe",
            KnownAppKey.WinScript => @"C:\Program Files\WinScript\WinScript.exe",
            _ => null
        };
    }

    private string? GetKnownRunPath(AppItem app)
    {
        return ResolveAppKey(app.AssetName) switch
        {
            KnownAppKey.SevenZip => File.Exists(@"C:\Program Files\7-Zip\7zFM.exe")
                ? @"C:\Program Files\7-Zip\7zFM.exe"
                : @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip",
            KnownAppKey.DlssUpdater => FirstExistingPath(@"C:\DLSS_Updater\DLSS_Updater.exe", Path.Combine(_appsRoot, "DLSS Updater", "DLSS_Updater.exe")),
            KnownAppKey.UniversalMediaPlayer => @"C:\Program Files\Universal Media Player\Universal Media Player.exe",
            KnownAppKey.WallpaperEngine => @"C:\Program Files\Wallpaper Engine\launcher32.exe",
            KnownAppKey.Windhawk => @"C:\Program Files\Windhawk\windhawk.exe",
            KnownAppKey.WinRar => @"C:\Program Files\WinRAR\WinRAR.exe",
            KnownAppKey.WinScript => @"C:\Program Files\WinScript\WinScript.exe",
            _ => null
        };
    }

    private string GetFallbackMarkerPath(AppItem app)
    {
        return IsZip(app)
            ? GetAppDirectory(app)
            : Path.Combine(GetAppDirectory(app), app.AssetName);
    }

    private string? ResolveFallbackRunPath(AppItem app)
    {
        var appDir = GetAppDirectory(app);
        if (!Directory.Exists(appDir))
        {
            return null;
        }

        var preferredName = ResolveAppKey(app.AssetName) switch
        {
            KnownAppKey.WallpaperEngine => "launcher32.exe",
            KnownAppKey.WinScript => "winscript.exe",
            _ => string.Empty
        };

        return FindPreferredExecutable(appDir, preferredName)
               ?? Directory.GetFiles(appDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    private string GetAppDirectory(AppItem app)
    {
        var safeName = string.Concat(app.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return Path.Combine(_appsRoot, safeName);
    }

    private static string? FindPreferredExecutable(string rootDirectory, string preferredExeName)
    {
        if (!string.IsNullOrWhiteSpace(preferredExeName))
        {
            var preferred = Directory.GetFiles(rootDirectory, preferredExeName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }
        }

        return null;
    }

    private static string? FirstExistingPath(params string[] paths)
    {
        return paths.FirstOrDefault(PathExists);
    }

    private static bool IsZip(AppItem app) => app.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool PathExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return File.Exists(path) || Directory.Exists(path);
    }

    private static KnownAppKey ResolveAppKey(string assetName)
    {
        var normalized = assetName.ToLowerInvariant();

        if (normalized.Contains("7z2600"))
        {
            return KnownAppKey.SevenZip;
        }

        if (normalized.Contains("dlss_updater"))
        {
            return KnownAppKey.DlssUpdater;
        }

        if (normalized.Contains("universal.media.player"))
        {
            return KnownAppKey.UniversalMediaPlayer;
        }

        if (normalized.Contains("wallpaper_engine"))
        {
            return KnownAppKey.WallpaperEngine;
        }

        if (normalized.Contains("windhawk_setup_offline"))
        {
            return KnownAppKey.Windhawk;
        }

        if (normalized.Contains("winrar"))
        {
            return KnownAppKey.WinRar;
        }

        if (normalized.Contains("winscript"))
        {
            return KnownAppKey.WinScript;
        }

        return KnownAppKey.Unknown;
    }

    private void CreateDesktopShortcut(string targetPath, string shortcutName)
    {
        var shortcutPath = Path.Combine(_desktopPath, $"{shortcutName}.lnk");

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();
        }
        catch
        {
            // Shortcut creation is optional.
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var destinationSubDir = Path.Combine(destinationDirectory, Path.GetFileName(subDir));
            CopyDirectory(subDir, destinationSubDir);
        }
    }

    private static void Launch(string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
        };

        Process.Start(startInfo);
    }

    private enum KnownAppKey
    {
        Unknown,
        SevenZip,
        DlssUpdater,
        UniversalMediaPlayer,
        WallpaperEngine,
        Windhawk,
        WinRar,
        WinScript
    }
}
