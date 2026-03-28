using System.Reflection;
using System.Text.RegularExpressions;
using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task ApplyUpdateAndRestartAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class UpdateService : IUpdateService
{
    private readonly IGitHubService _gitHubService;
    private readonly IDownloadService _downloadService;

    public UpdateService(IGitHubService gitHubService, IDownloadService downloadService)
    {
        _gitHubService = gitHubService;
        _downloadService = downloadService;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var release = await _gitHubService.GetLatestReleaseAsync(cancellationToken);
        var latestVersion = ParseVersion(release.TagName);
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

        if (latestVersion <= currentVersion)
        {
            return null;
        }

        var updateAsset = FindLauncherAsset(release.Assets);
        if (updateAsset is null)
        {
            return null;
        }

        return new UpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            AssetName = updateAsset.Name,
            DownloadUrl = updateAsset.BrowserDownloadUrl
        };
    }

    public async Task ApplyUpdateAndRestartAsync(
        UpdateInfo updateInfo,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!updateInfo.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update asset must be an .exe file.");
        }

        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve current launcher executable path.");

        var tempFolder = Path.Combine(Path.GetTempPath(), "SoftBoxLauncher", "Update");
        Directory.CreateDirectory(tempFolder);

        var downloadedUpdatePath = Path.Combine(tempFolder, updateInfo.AssetName);
        var updaterTempPath = Path.Combine(tempFolder, "updater_temp.exe");

        await _downloadService.DownloadFileAsync(updateInfo.DownloadUrl, downloadedUpdatePath, progress, cancellationToken);
        File.Copy(currentExe, updaterTempPath, overwrite: true);

        var arguments = $"--update \"{currentExe}\" \"{downloadedUpdatePath}\"";
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = updaterTempPath,
            Arguments = arguments,
            UseShellExecute = true,
            WorkingDirectory = tempFolder
        };

        var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to launch updater process.");
        }
    }

    private static GitHubAsset? FindLauncherAsset(IReadOnlyList<GitHubAsset> assets)
    {
        var currentExeName = Path.GetFileName(Environment.ProcessPath ?? "SoftBoxLauncher.exe");

        return assets.FirstOrDefault(asset => asset.Name.Equals(currentExeName, StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(asset =>
                   asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                   asset.Name.Contains("softboxlauncher", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(asset =>
                   asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                   asset.Name.Contains("launcher", StringComparison.OrdinalIgnoreCase));
    }

    private static Version ParseVersion(string rawTag)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
        {
            return new Version(0, 0, 0, 0);
        }

        var normalized = rawTag.Trim().TrimStart('v', 'V');
        if (Version.TryParse(normalized, out var parsed))
        {
            return EnsureFourPartVersion(parsed);
        }

        var match = Regex.Match(normalized, @"\d+(?:\.\d+){1,3}");
        if (match.Success && Version.TryParse(match.Value, out parsed))
        {
            return EnsureFourPartVersion(parsed);
        }

        return new Version(0, 0, 0, 0);
    }

    private static Version EnsureFourPartVersion(Version version)
    {
        return new Version(
            version.Major,
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }
}

