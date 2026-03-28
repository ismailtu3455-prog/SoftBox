using System.Net.Http;
using System.Text.Json;
using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public sealed class GitHubService : IGitHubService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ismailtu3455-prog/mystonge/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SoftBoxLauncher/1.0");
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"GitHub API error: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);

        return release ?? throw new InvalidOperationException("GitHub returned empty release payload.");
    }

    public async Task<IReadOnlyList<AppItem>> GetLauncherAppsAsync(CancellationToken cancellationToken = default)
    {
        var release = await GetLatestReleaseAsync(cancellationToken);

        return release.Assets
            .Where(IsSupportedLauncherAsset)
            .Select(MapToAppItem)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSupportedLauncherAsset(GitHubAsset asset)
    {
        var extension = Path.GetExtension(asset.Name);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !asset.Name.Contains("softboxlauncher", StringComparison.OrdinalIgnoreCase);
    }

    private static AppItem MapToAppItem(GitHubAsset asset)
    {
        var extension = Path.GetExtension(asset.Name).ToLowerInvariant();
        var nameWithoutExt = Path.GetFileNameWithoutExtension(asset.Name)
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Trim();

        var description = extension == ".zip"
            ? "Portable archive package (.zip)"
            : "Installer executable (.exe)";

        return new AppItem
        {
            Name = nameWithoutExt,
            AssetName = asset.Name,
            Description = description,
            DownloadUrl = asset.BrowserDownloadUrl,
            FileExtension = extension
        };
    }
}

