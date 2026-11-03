using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;
using VibeCode.MdEditor.ViewModels;
using VibeCode.MdEditor.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;

namespace VibeCode.MdEditor;

public sealed partial class MainWindow : Window
{
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly IFileAssociationService _fileAssociations;
    private readonly ILogger<MainWindow> _logger;
    private SizeInt32 _lastNormalSize;
    private bool _syncingTabs;
    private bool _allowClose;

    public MainWindow(
        MainViewModel viewModel,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        IFileAssociationService fileAssociations,
        ILogger<MainWindow> logger)
    {
        ViewModel = viewModel;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _fileAssociations = fileAssociations;
        _logger = logger;

        InitializeComponent();

        Title = ViewModel.WindowTitle;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.ThemeChanged += OnThemeChanged;
        ViewModel.PreferencesRequested += OnPreferencesRequested;
        ViewModel.RecentFiles.CollectionChanged += OnRecentFilesChanged;
        ExternalChangeBar.Closed += (_, _) => ViewModel.IsExternalChangeVisible = false;
        ErrorBar.Closed += (_, _) => ViewModel.IsErrorVisible = false;
        AppWindow.Closing += OnAppWindowClosing;
        AppWindow.Changed += OnAppWindowChanged;
        Closed += (_, _) =>
        {
            AppWindow.Changed -= OnAppWindowChanged;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.ThemeChanged -= OnThemeChanged;
            ViewModel.PreferencesRequested -= OnPreferencesRequested;
            ViewModel.RecentFiles.CollectionChanged -= OnRecentFilesChanged;
            ViewModel.Dispose();
        };

        AddPreferencesAccelerator();
        ApplyWindowIcon();
    }

    public MainViewModel ViewModel { get; }

    public async Task InitializeAsync(string? filePath)
    {
        _dialogs.Initialize(this);
        _dispatcher.Initialize(DispatcherQueue);

        try
        {
            await ViewModel.InitializeAsync(EditorHost, PreviewHost, filePath);

            ApplyWindowSizeAndCenter();
            RestoreWindowState();

            ApplyViewMode();
            SyncTabSelection();
            RebuildRecentMenu();
            Title = ViewModel.WindowTitle;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("啟動失敗", ex.Message);
        }
    }

    private void AddPreferencesAccelerator()
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = (VirtualKey)188,
            Modifiers = VirtualKeyModifiers.Control,
        };

        accelerator.Invoked += PreferencesAccelerator_Invoked;
        RootGrid.KeyboardAccelerators.Add(accelerator);
    }

    private void ApplyWindowSizeAndCenter()
    {
        var size = ViewModel.GetSavedWindowSize();
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var width = Math.Clamp(
            (int)size.Width,
            800,
            Math.Max(800, workArea.Width - 80));
        var height = Math.Clamp(
            (int)size.Height,
            500,
            Math.Max(500, workArea.Height - 80));

        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2));

        _lastNormalSize = new SizeInt32(width, height);
    }

    private void RestoreWindowState()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (string.Equals(
                ViewModel.GetSavedWindowState(),
                "Maximized",
                StringComparison.OrdinalIgnoreCase))
        {
            presenter.Maximize();
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Restored)
        {
            _lastNormalSize = sender.Size;
        }
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            AppWindow.SetIcon(iconPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法設定視窗圖示：{Path}", iconPath);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ActiveDocument):
                SyncTabSelection();
                break;

            case nameof(MainViewModel.ViewMode):
                ApplyViewMode();
                break;

            case nameof(MainViewModel.WindowTitle):
                Title = ViewModel.WindowTitle;
                break;
        }
    }

    private void OnThemeChanged(object? sender, AppThemeDefinition theme)
    {
        ThemeResourceManager.Apply(theme);

        var requestedTheme = theme.IsDark
            ? ElementTheme.Dark
            : ElementTheme.Light;

        if (RootGrid.RequestedTheme == requestedTheme)
        {
            RootGrid.RequestedTheme = requestedTheme == ElementTheme.Dark
                ? ElementTheme.Light
                : ElementTheme.Dark;
        }

        RootGrid.RequestedTheme = requestedTheme;
    }

    private void OnPreferencesRequested(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(() => _ = ShowPreferencesAsync());
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRecentMenu();
    }

    private void SyncTabSelection()
    {
        _syncingTabs = true;
        try
        {
            DocumentTabs.SelectedItem = ViewModel.ActiveDocument;
        }
        finally
        {
            _syncingTabs = false;
        }
    }

    private async void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTabs)
        {
            return;
        }

        if (DocumentTabs.SelectedItem is DocumentViewModel document &&
            !ReferenceEquals(document, ViewModel.ActiveDocument))
        {
            await ViewModel.SelectDocumentAsync(document);
        }
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            var document = ViewModel.Documents.FirstOrDefault(item => item.Id == id);
            if (document is not null)
            {
                await ViewModel.CloseDocumentAsync(document);
            }
        }
    }

    private async void PreferencesItem_Click(object sender, RoutedEventArgs e)
    {
        await ShowPreferencesAsync();
    }

    private async void AboutItem_Click(object sender, RoutedEventArgs e)
    {
        await ShowAboutAsync();
    }

    private void ExitItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task ShowPreferencesAsync()
    {
        if (RootGrid.XamlRoot is null)
        {
            return;
        }

        var dialog = new PreferencesDialog(
            ViewModel.CurrentTheme,
            ViewModel.SyncScrollingEnabled,
            _fileAssociations.IsRegistered)
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = RootGrid.RequestedTheme,
        };

        void OnOpenDefaultAppsRequested(object? sender, EventArgs e)
        {
            try
            {
                if (!_fileAssociations.IsRegistered)
                {
                    _fileAssociations.Register();
                }

                _fileAssociations.OpenDefaultAppsSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登錄檔案關聯或開啟預設應用程式設定失敗。");
                dialog.ShowAssociationError(ex.Message);
            }
        }

        dialog.OpenDefaultAppsRequested += OnOpenDefaultAppsRequested;

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            dialog.OpenDefaultAppsRequested -= OnOpenDefaultAppsRequested;
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.ApplyPreferencesAsync(
            dialog.SelectedTheme,
            dialog.SyncScrollingEnabled);

        var shouldRegister = dialog.FileAssociationEnabled;
        if (shouldRegister == _fileAssociations.IsRegistered)
        {
            return;
        }

        try
        {
            if (shouldRegister)
            {
                _fileAssociations.Register();
            }
            else
            {
                _fileAssociations.Unregister();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "套用檔案關聯設定失敗。");
            await _dialogs.ShowMessageAsync("檔案關聯", ex.Message);
            return;
        }

        if (shouldRegister)
        {
            try
            {
                _fileAssociations.OpenDefaultAppsSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "開啟預設應用程式設定失敗。");
            }
        }
    }

    private async Task ShowAboutAsync()
    {
        if (RootGrid.XamlRoot is null)
        {
            return;
        }

        var dialog = new AboutDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = RootGrid.RequestedTheme,
        };

        await dialog.ShowAsync();
    }

    private void ApplyViewMode()
    {
        switch (ViewModel.ViewMode)
        {
            case EditorViewMode.Edit:
                EditorHost.Visibility = Visibility.Visible;
                Grid.SetColumn(EditorHost, 0);
                Grid.SetColumnSpan(EditorHost, 3);
                PreviewHost.Visibility = Visibility.Collapsed;
                Splitter.Visibility = Visibility.Collapsed;
                break;

            case EditorViewMode.Preview:
                PreviewHost.Visibility = Visibility.Visible;
                Grid.SetColumn(PreviewHost, 0);
                Grid.SetColumnSpan(PreviewHost, 3);
                EditorHost.Visibility = Visibility.Collapsed;
                Splitter.Visibility = Visibility.Collapsed;
                break;

            default:
                EditorHost.Visibility = Visibility.Visible;
                Grid.SetColumn(EditorHost, 0);
                Grid.SetColumnSpan(EditorHost, 1);
                PreviewHost.Visibility = Visibility.Visible;
                Grid.SetColumn(PreviewHost, 2);
                Grid.SetColumnSpan(PreviewHost, 1);
                Splitter.Visibility = Visibility.Visible;
                break;
        }
    }

    private void Splitter_DragDelta(object? sender, double horizontalChange)
    {
        var total = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (total <= 0)
        {
            return;
        }

        const double minimum = 220d;
        var editorWidth = Math.Clamp(
            EditorColumn.ActualWidth + horizontalChange,
            minimum,
            Math.Max(minimum, total - minimum));

        EditorColumn.Width = new GridLength(editorWidth, GridUnitType.Pixel);
        PreviewColumn.Width = new GridLength(Math.Max(minimum, total - editorWidth), GridUnitType.Pixel);
    }

    private void RebuildRecentMenu()
    {
        RecentSubMenu.Items.Clear();

        if (ViewModel.RecentFiles.Count == 0)
        {
            RecentSubMenu.Items.Add(new MenuFlyoutItem
            {
                Text = "沒有最近檔案",
                IsEnabled = false,
            });
            return;
        }

        foreach (var entry in ViewModel.RecentFiles.ToList())
        {
            var item = new MenuFlyoutItem
            {
                Text = (entry.IsPinned ? "★ " : string.Empty) +
                       entry.DisplayName +
                       (entry.IsMissing ? "（找不到）" : string.Empty),
                Tag = entry.Path,
            };

            item.Click += async (_, _) => await ViewModel.OpenRecentAsync(entry.Path);
            RecentSubMenu.Items.Add(item);
        }

        RecentSubMenu.Items.Add(new MenuFlyoutSeparator());

        var clearItem = new MenuFlyoutItem { Text = "清除最近檔案" };
        clearItem.Click += async (_, _) => await ViewModel.ClearRecentFilesAsync();
        RecentSubMenu.Items.Add(clearItem);
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "開啟 Markdown 檔案";
        e.DragUIOverride.IsCaptionVisible = true;
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var file in items.OfType<StorageFile>())
        {
            await ViewModel.OpenFileAsync(file.Path);
        }
    }

    private void NewAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.NewDocumentCommand.Execute(null);
    }

    private void OpenAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.OpenDocumentCommand.Execute(null);
    }

    private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.SaveCommand.Execute(null);
    }

    private void SaveAsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.SaveAsCommand.Execute(null);
    }

    private void CloseDocumentAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.CloseActiveDocumentCommand.Execute(null);
    }

    private void ViewEditAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.SetViewModeCommand.Execute("Edit");
    }

    private void ViewPreviewAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.SetViewModeCommand.Execute("Preview");
    }

    private void ViewSplitAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.SetViewModeCommand.Execute("Split");
    }

    private void PreferencesAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = ShowPreferencesAsync();
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;

        try
        {
            if (!await ViewModel.ConfirmCloseApplicationAsync())
            {
                return;
            }

            await ViewModel.PersistSessionAsync();

            var windowState = AppWindow.Presenter is OverlappedPresenter
            {
                State: OverlappedPresenterState.Maximized,
            }
                ? "Maximized"
                : "Normal";

            await ViewModel.SaveWindowSettingsAsync(
                _lastNormalSize.Width,
                _lastNormalSize.Height,
                windowState);
            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("無法關閉程式", ex.Message);
        }
    }
}
