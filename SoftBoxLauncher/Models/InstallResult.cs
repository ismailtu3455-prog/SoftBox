namespace SoftBoxLauncher.Models;

public sealed class InstallResult
{
    public required string InstallMarkerPath { get; init; }
    public string? LaunchedExecutablePath { get; init; }
}

