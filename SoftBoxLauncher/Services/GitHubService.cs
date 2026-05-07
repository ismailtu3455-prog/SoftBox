using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Services;

public sealed class GitHubService : IGitHubService
{
    private const string Owner = "ismailtu3455-prog";
    private const string Repository = "mystonge";
    private const string RepositoryPath = Owner + "/" + Repository;
    private const string GitHubBaseUrl = "https://github.com/" + RepositoryPath;
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/" + RepositoryPath + "/releases/latest";
    private const string LatestReleaseWebUrl = GitHubBaseUrl + "/releases/latest";
    private const string GitHubApiVersion = "2022-11-28";

    private static readonly Uri GitHubHostUri = new("https://github.com");
    private static readonly Regex AssetHrefRegex = new(
        "href=\"(?<href>/" + RepositoryPath + "/releases/download/[^\"/]+/(?<asset>[^\"]+))\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetLatestReleaseFromApiAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            try
            {
                return await GetLatestReleaseFromWebAsync(cancellationToken);
            }
            catch (Exception fallbackEx) when (fallbackEx is HttpRequestException or InvalidOperationException)
            {
                var statusCode = ex is HttpRequestException httpEx ? httpEx.StatusCode : null;
                throw new HttpRequestException(
                    $"Unable to load GitHub release assets. API failed: {ex.Message} Web fallback failed: {fallbackEx.Message}",
                    fallbackEx,
                    statusCode);
            }
        }
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

    private async Task<GitHubRelease> GetLatestReleaseFromApiAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubRequestExceptionAsync(response, "GitHub API", cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);

        return release ?? throw new InvalidOperationException("GitHub returned empty release payload.");
    }

    private async Task<GitHubRelease> GetLatestReleaseFromWebAsync(CancellationToken cancellationToken)
    {
        using var response = await SendGitHubWebRequestAsync(LatestReleaseWebUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubRequestExceptionAsync(response, "GitHub release page", cancellationToken);
        }

        var tagName = TryGetTagNameFromReleaseUri(response.RequestMessage?.RequestUri);
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new InvalidOperationException("GitHub release page did not redirect to a release tag.");
        }

        var assets = await GetReleaseAssetsFromWebAsync(tagName, cancellationToken);

        return new GitHubRelease
        {
            TagName = tagName,
            Name = tagName,
            Assets = assets
        };
    }

    private async Task<IReadOnlyList<GitHubAsset>> GetReleaseAssetsFromWebAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        var expandedAssetsUrl = $"{GitHubBaseUrl}/releases/expanded_assets/{Uri.EscapeDataString(tagName)}";
        using var response = await SendGitHubWebRequestAsync(expandedAssetsUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubRequestExceptionAsync(response, "GitHub release assets page", cancellationToken);
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseAssetsFromHtml(html);
    }

    private async Task<HttpResponseMessage> SendGitHubWebRequestAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/html");
        request.Headers.Accept.ParseAdd("application/xhtml+xml");

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

    private static IReadOnlyList<GitHubAsset> ParseAssetsFromHtml(string html)
    {
        var assets = new List<GitHubAsset>();
        var seenAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AssetHrefRegex.Matches(html))
        {
            var relativeHref = WebUtility.HtmlDecode(match.Groups["href"].Value);
            var assetName = WebUtility.HtmlDecode(Uri.UnescapeDataString(match.Groups["asset"].Value));

            if (string.IsNullOrWhiteSpace(assetName) || !seenAssets.Add(assetName))
            {
                continue;
            }

            assets.Add(new GitHubAsset
            {
                Name = assetName,
                BrowserDownloadUrl = new Uri(GitHubHostUri, relativeHref).ToString()
            });
        }

        return assets;
    }

    private static string? TryGetTagNameFromReleaseUri(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("tag", StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.UrlDecode(segments[index + 1]);
            }
        }

        return null;
    }

    private static async Task<HttpRequestException> CreateGitHubRequestExceptionAsync(
        HttpResponseMessage response,
        string endpointName,
        CancellationToken cancellationToken)
    {
        var details = await ReadGitHubErrorDetailsAsync(response, cancellationToken);
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.StatusCode.ToString()
            : response.ReasonPhrase;
        var status = $"{(int)response.StatusCode} {reason}";
        var message = string.IsNullOrWhiteSpace(details)
            ? $"{endpointName} error: {status}."
            : $"{endpointName} error: {status}. {details}";

        return new HttpRequestException(message, null, response.StatusCode);
    }

    private static async Task<string> ReadGitHubErrorDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return LimitErrorText(messageElement.GetString());
                }
            }
            catch (JsonException)
            {
                return "GitHub returned an unreadable JSON error response.";
            }
        }

        if (body.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            return "GitHub returned an HTML error page instead of release data.";
        }

        return LimitErrorText(body);
    }

    private static string LimitErrorText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex.Replace(text.Trim(), " ");
        return normalized.Length <= 300
            ? normalized
            : normalized[..300] + "...";
    }
}
