using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public interface IInstallerService
{
    string GetInstallMarkerPath(AppItem app);
    bool IsInstalled(AppItem app);
    Task<InstallResult> InstallAsync(AppItem app, string downloadedPath, CancellationToken cancellationToken = default);
    Task<string?> LaunchInstalledAsync(AppItem app, CancellationToken cancellationToken = default);
}

