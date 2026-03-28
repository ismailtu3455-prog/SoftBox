using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public interface ISettingsService
{
    Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default);
}

