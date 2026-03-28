using SoftBoxLauncher.Commands;
using SoftBoxLauncher.Models;
using SoftBoxLauncher.Services;
using System.Text;

namespace SoftBoxLauncher.ViewModels;

public sealed class AppItemViewModel : ViewModelBase
{
    private readonly IDownloadService _downloadService;
    private readonly IInstallerService _installerService;
    private readonly string _downloadCacheDirectory;

    private AppInstallState _state;
    private double _progressPercent;
    private string _progressText = "0%";
    private string _statusText = "Not Installed";

    public AppItemViewModel(AppItem appItem, IDownloadService downloadService, IInstallerService installerService)
    {
        AppItem = appItem;
        _downloadService = downloadService;
        _installerService = installerService;
        _downloadCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftBoxLauncher",
            "Downloads");

        InstallCommand = new AsyncRelayCommand(_ => InstallOrLaunchAsync(), _ => !IsBusy);

        RefreshInstalledState();
    }

    public AppItem AppItem { get; }
    public string Name => AppItem.Name;
    public string DisplayNameTwoLines => ToTwoLines(Name, 20);
    public string Description => AppItem.Description;
    public string AssetName => AppItem.AssetName;

    public string IconGlyph => AppItem.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
        ? "\uE8B7"
        : "\uE7C3";

    public AsyncRelayCommand InstallCommand { get; }

    public AppInstallState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(InstallButtonText));
                (InstallCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy => State is AppInstallState.Downloading or AppInstallState.Installing;
    public bool IsInstalled => State == AppInstallState.Installed;

    public bool ShowProgress => State is AppInstallState.Downloading or AppInstallState.Installing;

    public string InstallButtonText => State switch
    {
        AppInstallState.Installed => "Run",
        AppInstallState.Downloading => "Downloading...",
        AppInstallState.Installing => "Installing...",
        AppInstallState.Error => "Retry",
        _ => "Install"
    };

    public void RefreshInstalledState()
    {
        if (_installerService.IsInstalled(AppItem))
        {
            SetState(AppInstallState.Installed, "Installed");
            ProgressPercent = 100;
            ProgressText = "100%";
            return;
        }

        SetState(AppInstallState.NotInstalled, "Not Installed");
        ProgressPercent = 0;
        ProgressText = "0%";
    }

    public async Task InstallOrLaunchAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            if (_installerService.IsInstalled(AppItem))
            {
                SetState(AppInstallState.Installing, "Launching...");
                var launchedPath = await _installerService.LaunchInstalledAsync(AppItem, cancellationToken);

                if (string.IsNullOrWhiteSpace(launchedPath))
                {
                    throw new FileNotFoundException("Installed application executable is missing.");
                }

                SetState(AppInstallState.Installed, "Installed");
                return;
            }

            SetState(AppInstallState.Downloading, "Downloading...");

            var destinationPath = Path.Combine(_downloadCacheDirectory, AppItem.AssetName);

            var progress = new Progress<DownloadProgress>(info =>
            {
                ProgressPercent = Math.Clamp(info.Percent, 0, 100);
                ProgressText = $"{ProgressPercent:F0}% | {info.ToHumanReadableText()}";
                OnPropertyChanged(nameof(ShowProgress));
            });

            var downloadedPath = await _downloadService.DownloadFileAsync(
                AppItem.DownloadUrl,
                destinationPath,
                progress,
                cancellationToken);

            SetState(AppInstallState.Installing, "Installing...");
            await _installerService.InstallAsync(AppItem, downloadedPath, cancellationToken);

            RefreshInstalledState();
            if (!IsInstalled)
            {
                SetState(AppInstallState.NotInstalled, "Installer launched");
                ProgressPercent = 0;
                ProgressText = "0%";
            }
        }
        catch (OperationCanceledException)
        {
            SetState(AppInstallState.Error, "Cancelled");
        }
        catch (Exception ex)
        {
            SetState(AppInstallState.Error, $"Error: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(nameof(ShowProgress));
        }
    }

    private void SetState(AppInstallState state, string status)
    {
        State = state;
        StatusText = status;
        OnPropertyChanged(nameof(ShowProgress));
    }

    private static string ToTwoLines(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line1 = new StringBuilder();
        var line2 = new StringBuilder();
        var overflow = false;

        foreach (var word in words)
        {
            if (CanAppend(line1, word, maxCharsPerLine))
            {
                Append(line1, word);
                continue;
            }

            if (CanAppend(line2, word, maxCharsPerLine))
            {
                Append(line2, word);
                continue;
            }

            overflow = true;
            break;
        }

        if (overflow && line2.Length > 0)
        {
            while (line2.Length > maxCharsPerLine - 3)
            {
                line2.Length--;
            }

            line2.Append("...");
        }

        return line2.Length == 0
            ? line1.ToString()
            : $"{line1}{Environment.NewLine}{line2}";
    }

    private static bool CanAppend(StringBuilder line, string word, int limit)
    {
        var extra = line.Length == 0 ? 0 : 1;
        return line.Length + extra + word.Length <= limit;
    }

    private static void Append(StringBuilder line, string word)
    {
        if (line.Length > 0)
        {
            line.Append(' ');
        }

        line.Append(word);
    }
}

