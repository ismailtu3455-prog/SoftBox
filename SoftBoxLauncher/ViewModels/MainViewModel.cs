using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using SoftBoxLauncher.Commands;
using SoftBoxLauncher.Models;
using SoftBoxLauncher.Services;

namespace SoftBoxLauncher.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IGitHubService _gitHubService;
    private readonly IDownloadService _downloadService;
    private readonly IInstallerService _installerService;
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    private bool _isInitialized;
    private bool _isBusy;
    private bool _isLightTheme;
    private string _statusText = "Ready";
    private string _updateProgressText = string.Empty;
    private string _errorMessage = string.Empty;

    public MainViewModel(
        IGitHubService gitHubService,
        IDownloadService downloadService,
        IInstallerService installerService,
        IUpdateService updateService,
        ISettingsService settingsService,
        IDialogService dialogService)
    {
        _gitHubService = gitHubService;
        _downloadService = downloadService;
        _installerService = installerService;
        _updateService = updateService;
        _settingsService = settingsService;
        _dialogService = dialogService;

        Apps = new ObservableCollection<AppItemViewModel>();

        RefreshCommand = new AsyncRelayCommand(_ => ReloadAsync(), _ => !IsBusy);
        InstallAllCommand = new AsyncRelayCommand(_ => InstallAllAsync(), _ => !IsBusy && Apps.Count > 0);

        ThemeService.ApplyTheme(isLightTheme: false);
    }

    public ObservableCollection<AppItemViewModel> Apps { get; }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand InstallAllCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                InstallAllCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set
        {
            if (SetProperty(ref _isLightTheme, value))
            {
                ThemeService.ApplyTheme(value);
                OnPropertyChanged(nameof(ThemeLabel));
            }
        }
    }

    public string ThemeLabel => IsLightTheme ? "Light" : "Dark";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string UpdateProgressText
    {
        get => _updateProgressText;
        private set => SetProperty(ref _updateProgressText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ReloadAsync();
        await CheckForUpdatesAsync();
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusText = "Loading apps...";

        try
        {
            var appItems = await _gitHubService.GetLauncherAppsAsync(cancellationToken);

            Apps.Clear();
            foreach (var item in appItems)
            {
                Apps.Add(new AppItemViewModel(item, _downloadService, _installerService));
            }

            StatusText = $"Loaded {Apps.Count} apps";
        }
        catch (Exception ex)
        {
            ErrorMessage = ToUserFacingError(ex);
            StatusText = "Failed to load apps";
        }
        finally
        {
            IsBusy = false;
            InstallAllCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task InstallAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusText = "Installing all apps...";

        try
        {
            var successCount = 0;
            var failureCount = 0;

            foreach (var app in Apps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (app.IsInstalled)
                {
                    continue;
                }

                StatusText = $"Installing {app.Name}...";
                await app.InstallOrLaunchAsync(cancellationToken);

                if (app.State == AppInstallState.Installed)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            StatusText = failureCount == 0
                ? $"Install All completed. Success: {successCount}"
                : $"Install All finished. Success: {successCount}, Failed: {failureCount}";

            if (failureCount > 0)
            {
                ErrorMessage = "Some applications failed to install. Open failed cards and use Retry.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ToUserFacingError(ex);
            StatusText = "Install All failed";
        }
        finally
        {
            IsBusy = false;
            InstallAllCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            if (!settings.AutoUpdateEnabled)
            {
                return;
            }

            var updateInfo = await _updateService.CheckForUpdateAsync(cancellationToken);
            if (updateInfo is null)
            {
                return;
            }

            var installUpdate = await _dialogService.ShowConfirmationAsync(
                "SoftBox Launcher Update",
                $"A new launcher version is available ({updateInfo.DisplayVersion}). Download and restart now?");

            if (!installUpdate)
            {
                return;
            }

            StatusText = "Downloading launcher update...";
            var progress = new Progress<DownloadProgress>(p =>
            {
                UpdateProgressText = $"Updating launcher: {p.Percent:F0}% | {p.ToHumanReadableText()}";
            });

            await _updateService.ApplyUpdateAndRestartAsync(updateInfo, progress, cancellationToken);
            await _dialogService.ShowInfoAsync("Update", "Update downloaded. Launcher will restart now.");
            Application.Current.Shutdown();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            StatusText = "Update check skipped (network unavailable)";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Auto Update Error", ToUserFacingError(ex));
        }
        finally
        {
            UpdateProgressText = string.Empty;
        }
    }

    private static string ToUserFacingError(Exception exception)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Operation failed.";
        }

        var trimmed = message.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) ||
            message.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
        {
            return "GitHub returned an HTML error page instead of release data.";
        }

        return message.Length <= 500
            ? message
            : message[..500] + "...";
    }
}
