namespace SoftBoxLauncher.Models;

public sealed class AppItem
{
    public required string Name { get; init; }
    public required string AssetName { get; init; }
    public required string Description { get; init; }
    public required string DownloadUrl { get; init; }
    public required string FileExtension { get; init; }
}

