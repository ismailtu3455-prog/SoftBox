using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public interface IGitHubService
{
    Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppItem>> GetLauncherAppsAsync(CancellationToken cancellationToken = default);
}

