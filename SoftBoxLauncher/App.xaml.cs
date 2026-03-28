using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using SoftBoxLauncher.Services;
using SoftBoxLauncher.ViewModels;

namespace SoftBoxLauncher;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private int _fatalErrorShown;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (SelfUpdateBootstrapper.TryHandleArguments(e.Args))
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            var gitHubService = new GitHubService(_httpClient);
            var downloadService = new DownloadService(_httpClient);
            var installerService = new InstallerService();
            var updateService = new UpdateService(gitHubService, downloadService);
            var settingsService = new SettingsService(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            var dialogService = new DialogService();

            var mainViewModel = new MainViewModel(
                gitHubService,
                downloadService,
                installerService,
                updateService,
                settingsService,
                dialogService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            MainWindow = mainWindow;
            mainWindow.Show();

            await mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex, "Startup");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalError(e.Exception, "UI Thread");
        e.Handled = true;
        Shutdown();
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            CrashLogger.Log(ex, "AppDomain");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.Log(e.Exception, "TaskScheduler");
        e.SetObserved();
    }

    private void ShowFatalError(Exception exception, string source)
    {
        if (Interlocked.Exchange(ref _fatalErrorShown, 1) == 1)
        {
            return;
        }

        var logPath = CrashLogger.Log(exception, source);
        var details = string.IsNullOrWhiteSpace(logPath)
            ? exception.Message
            : $"{exception.Message}{Environment.NewLine}{Environment.NewLine}Log: {logPath}";

        MessageBox.Show(details, "SoftBox Launcher Crash", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
