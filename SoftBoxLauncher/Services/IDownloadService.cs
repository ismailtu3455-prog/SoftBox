using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public interface IDownloadService
{
    Task<string> DownloadFileAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

