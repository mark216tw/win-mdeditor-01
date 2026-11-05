using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Services;
using VibeCode.MdEditor.Helpers;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;
using VibeCode.MdEditor.ViewModels;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace VibeCode.MdEditor;

public partial class App : Application
{
    private const string InstanceKey = "VibeCode.MdEditor.Main";

    private readonly IHost _host;
    private readonly ILogger<App> _logger;
    private MainWindow? _mainWindow;
    private string? _pendingFile;

    public App()
    {
        InitializeComponent();

        var localDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MdEditor");
        Directory.CreateDirectory(localDirectory);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(localDirectory, "logs")));

        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        builder.Services.AddSingleton<IFileWatcherService, FileWatcherService>();
        builder.Services.AddSingleton<ISettingsService>(_ => new SettingsService(localDirectory));
        builder.Services.AddSingleton<IRecentFilesService, RecentFilesService>();
        builder.Services.AddSingleton<ISessionDraftService>(_ => new SessionDraftService(localDirectory));
        builder.Services.AddSingleton<IDocxExportService, DocxExportService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IFileAssociationService, FileAssociationService>();
        builder.Services.AddSingleton<IUiDispatcher, UiDispatcher>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);

            if (!mainInstance.IsCurrent)
            {
                if (!WindowActivationHelper.AllowSetForeground(mainInstance.ProcessId))
                {
                    _logger.LogWarning("無法將前景視窗權限轉交給主要執行個體。");
                }

                await mainInstance.RedirectActivationToAsync(activation);
                _host.Dispose();
                Exit();
                return;
            }

            mainInstance.Activated += OnInstanceActivated;

            _ = WebViewEnvironmentProvider.GetAsync();

            await ApplyInitialThemeAsync();

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainWindow.Closed += (_, _) =>
            {
                try
                {
                    _host.Dispose();
                }
                catch (Exception)
                {
                }

                _mainWindow = null;
            };

            _mainWindow.Activate();
            _mainWindow.ApplyInitialWindowLayout();
            _mainWindow.PrewarmEditor();

            var filePath = LaunchArguments.TryGetFilePath(
                Environment.GetCommandLineArgs().Skip(1));

            await _mainWindow.InitializeAsync(filePath);

            if (_pendingFile is not null)
            {
                var pending = _pendingFile;
                _pendingFile = null;
                BringToForeground(_mainWindow);
                await _mainWindow.ViewModel.OpenFileAsync(pending);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "應用程式啟動失敗。");

            try
            {
                _host.Dispose();
            }
            catch (Exception)
            {
            }

            Exit();
        }
    }

    private async Task ApplyInitialThemeAsync()
    {
        var settings = _host.Services.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();

        var theme = AppThemeCatalog.Resolve(
            settings.Settings.Theme,
            Application.Current.RequestedTheme == ApplicationTheme.Dark);

        ThemeResourceManager.Apply(theme);
    }

    private void OnInstanceActivated(object? sender, AppActivationArguments args)
    {
        var filePath = GetActivatedFilePath(args);
        if (filePath is null)
        {
            return;
        }

        var window = _mainWindow;
        if (window is null)
        {
            _pendingFile = filePath;
            return;
        }

        window.DispatcherQueue.TryEnqueue(() =>
        {
            _ = OpenFromActivationAsync(window, filePath);
        });
    }

    private async Task OpenFromActivationAsync(MainWindow window, string filePath)
    {
        try
        {
            BringToForeground(window);
            await window.ViewModel.OpenFileAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開啟啟動參數檔案失敗：{Path}", filePath);
        }
    }

    private void BringToForeground(MainWindow window)
    {
        if (!WindowActivationHelper.BringToForeground(window))
        {
            _logger.LogWarning("無法將主要視窗切換到前景。");
        }
    }

    private static string? GetActivatedFilePath(AppActivationArguments args)
    {
        if (args.Data is IFileActivatedEventArgs fileArgs &&
            fileArgs.Files.Count > 0 &&
            fileArgs.Files[0] is StorageFile file)
        {
            return file.Path;
        }

        if (args.Data is ILaunchActivatedEventArgs launchArgs)
        {
            return LaunchArguments.TryGetFilePath(launchArgs.Arguments);
        }

        return null;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "未處理的例外。");
    }
}
