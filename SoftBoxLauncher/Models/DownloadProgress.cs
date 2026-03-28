namespace SoftBoxLauncher.Models;

public sealed class DownloadProgress
{
    public required long TotalBytes { get; init; }
    public required long DownloadedBytes { get; init; }

    public double Percent => TotalBytes <= 0 ? 0 : (double)DownloadedBytes / TotalBytes * 100;

    public string ToHumanReadableText()
    {
        var downloadedMb = DownloadedBytes / 1024d / 1024d;
        var totalMb = TotalBytes / 1024d / 1024d;

        if (TotalBytes <= 0)
        {
            return $"{downloadedMb:F1} MB";
        }

        return $"{downloadedMb:F1} / {totalMb:F1} MB";
    }
}

