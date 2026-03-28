namespace SoftBoxLauncher.Models;

public sealed class UpdateInfo
{
    public required Version CurrentVersion { get; init; }
    public required Version LatestVersion { get; init; }
    public required string AssetName { get; init; }
    public required string DownloadUrl { get; init; }

    public string DisplayVersion => $"{CurrentVersion} -> {LatestVersion}";
}

