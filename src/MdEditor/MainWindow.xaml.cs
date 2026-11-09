using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;
using VibeCode.MdEditor.ViewModels;
using VibeCode.MdEditor.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;

namespace VibeCode.MdEditor;

public sealed partial class MainWindow : Window
{
    private const int MinimumWindowWidth = 800;
    private const int MinimumWindowHeight = 500;
    private const double CompactWindowScale = 0.6;
    private const double ComfortableWindowScale = 0.8;

    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly IFileAssociationService _fileAssociations;
    private readonly ILogger<MainWindow> _logger;
    private readonly UISettings _uiSettings = new();
    private SizeInt32 _lastNormalSize;
    private PointInt32 _lastNormalPosition;
    private double _editorSplitRatio = 0.5;
    private string? _recentMenuSignature;
    private bool _syncingTabs;
    private bool _isClosing;
    private bool _allowClose;
    private bool _initialLayoutApplied;

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
        ViewModel.RecentMenuStateChanged += OnRecentMenuStateChanged;
        ViewModel.RecentFiles.CollectionChanged += OnRecentFilesChanged;
        ExternalChangeBar.Closed += (_, _) => ViewModel.IsExternalChangeVisible = false;
        ErrorBar.Closed += (_, _) => ViewModel.IsErrorVisible = false;
        AppWindow.Closing += OnAppWindowClosing;
        AppWindow.Changed += OnAppWindowChanged;
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
        Closed += (_, _) =>
        {
            _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
            AppWindow.Changed -= OnAppWindowChanged;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.ThemeChanged -= OnThemeChanged;
            ViewModel.PreferencesRequested -= OnPreferencesRequested;
            ViewModel.RecentMenuStateChanged -= OnRecentMenuStateChanged;
            ViewModel.RecentFiles.CollectionChanged -= OnRecentFilesChanged;
            ViewModel.Dispose();
            EditorHost.Close();
            PreviewHost.Close();
        };

        AddPreferencesAccelerator();
        DocumentTabs.AddHandler(
            UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(DocumentTabs_DoubleTapped),
            handledEventsToo: true);
        ApplyWindowIcon();
    }

    public MainViewModel ViewModel { get; }

    public async Task InitializeAsync(string? filePath)
    {
        _dialogs.Initialize(this);
        _dispatcher.Initialize(DispatcherQueue);
        ApplyInitialWindowLayout();

        try
        {
            await ViewModel.InitializeAsync(EditorHost, PreviewHost, filePath);

            _editorSplitRatio = ViewModel.GetSavedSplitRatio();
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

    public void ApplyInitialWindowLayout()
    {
        if (_initialLayoutApplied)
        {
            return;
        }

        _initialLayoutApplied = true;
        ApplyWindowSizeAndCenter();
        RestoreWindowState();
    }

    public void PrewarmEditor()
    {
        _ = EditorHost.InitializeAsync();
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
        var position = ViewModel.GetSavedWindowPosition();

        var displayArea = position.Left is double savedLeft && position.Top is double savedTop
            ? DisplayArea.GetFromRect(
                  new RectInt32((int)savedLeft, (int)savedTop, (int)size.Width, (int)size.Height),
                  DisplayAreaFallback.None)
              ?? DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)
            : DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var width = Math.Clamp(
            (int)size.Width,
            MinimumWindowWidth,
            Math.Max(MinimumWindowWidth, workArea.Width));
        var height = Math.Clamp(
            (int)size.Height,
            MinimumWindowHeight,
            Math.Max(MinimumWindowHeight, workArea.Height));

        int x;
        int y;

        if (position.Left is double left && position.Top is double top)
        {
            x = Math.Clamp(
                (int)left,
                workArea.X,
                workArea.X + Math.Max(0, workArea.Width - width));
            y = Math.Clamp(
                (int)top,
                workArea.Y,
                workArea.Y + Math.Max(0, workArea.Height - height));
        }
        else
        {
            x = workArea.X + (workArea.Width - width) / 2;
            y = workArea.Y + (workArea.Height - height) / 2;
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinimumWindowWidth;
            presenter.PreferredMinimumHeight = MinimumWindowHeight;
        }

        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(x, y));

        _lastNormalSize = new SizeInt32(width, height);
        _lastNormalPosition = new PointInt32(x, y);
    }

    private void ResizeWindowWidthToWorkArea(double scale)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        var workArea = DisplayArea
            .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)
            .WorkArea;
        var width = Math.Clamp(
            (int)Math.Round(workArea.Width * scale),
            MinimumWindowWidth,
            Math.Max(MinimumWindowWidth, workArea.Width));
        var height = _lastNormalSize.Height;
        var x = workArea.X + (workArea.Width - width) / 2;
        var y = Math.Clamp(
            _lastNormalPosition.Y,
            workArea.Y,
            workArea.Y + Math.Max(0, workArea.Height - height));

        presenter.Restore();
        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(x, y));

        _lastNormalSize = new SizeInt32(width, height);
        _lastNormalPosition = new PointInt32(x, y);
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
        if (!args.DidSizeChange && !args.DidPositionChange)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Restored)
        {
            if (args.DidSizeChange)
            {
                _lastNormalSize = sender.Size;
            }

            if (args.DidPositionChange)
            {
                _lastNormalPosition = sender.Position;
            }
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

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        _dispatcher.TryEnqueue(() => _ = ViewModel.RefreshSystemThemeAsync());
    }

    private void OnPreferencesRequested(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(() => _ = ShowPreferencesAsync());
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRecentMenu();
    }

    private void OnRecentMenuStateChanged(object? sender, EventArgs e)
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

        UpdateActiveTabVisuals(ViewModel.ActiveDocument);
    }

    private void UpdateActiveTabVisuals(DocumentViewModel? activeDocument)
    {
        foreach (var document in ViewModel.Documents)
        {
            document.IsActive = ReferenceEquals(document, activeDocument);
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
            UpdateActiveTabVisuals(document);
            await ViewModel.SelectDocumentAsync(document);
        }
    }

    private async void DocumentTabs_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ListViewItem or ScrollBar)
            {
                return;
            }

            if (ReferenceEquals(current, DocumentTabs))
            {
                break;
            }
        }

        e.Handled = true;

        if (ViewModel.NewDocumentCommand.CanExecute(null))
        {
            await ViewModel.NewDocumentCommand.ExecuteAsync(null);
        }
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            var document = FindDocument(id);
            if (document is not null)
            {
                await ViewModel.CloseDocumentAsync(document);
            }
        }
    }

    private async void Tab_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint((UIElement)sender).Properties.IsMiddleButtonPressed ||
            sender is not FrameworkElement { Tag: string id })
        {
            return;
        }

        e.Handled = true;

        var document = FindDocument(id);
        if (document is not null)
        {
            await ViewModel.CloseDocumentAsync(document);
        }
    }

    private async void CloseOtherTabs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            var document = FindDocument(id);
            if (document is not null)
            {
                await ViewModel.CloseOtherDocumentsAsync(document);
            }
        }
    }

    private void CopyTabPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
        {
            return;
        }

        var document = FindDocument(id);
        if (document?.FilePath is null)
        {
            ViewModel.StatusText = "此文件尚未儲存，沒有檔案路徑。";
            return;
        }

        var package = new DataPackage();
        package.SetText(document.FilePath);
        Clipboard.SetContent(package);
        ViewModel.StatusText = "已複製檔案路徑。";
    }

    private void RevealTabFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            RevealInExplorer(FindDocument(id)?.FilePath);
        }
    }

    private void RevealActiveFile_Click(object sender, RoutedEventArgs e)
    {
        RevealInExplorer(ViewModel.ActiveDocument?.FilePath);
    }

    private DocumentViewModel? FindDocument(string id)
    {
        return ViewModel.Documents.FirstOrDefault(item => item.Id == id);
    }

    private void RevealInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ViewModel.StatusText = "找不到檔案。";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法在檔案總管顯示檔案：{Path}", path);
            ViewModel.StatusText = "無法在檔案總管顯示檔案。";
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
        if (RootGrid.XamlRoot is null || _dialogs.IsDialogOpen)
        {
            return;
        }

        var dialog = new PreferencesDialog(
            ViewModel.CurrentTheme,
            ViewModel.SyncScrollingEnabled,
            ViewModel.WordWrapEnabled,
            ViewModel.EditorFontSize,
            ViewModel.EditorFontFamily,
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
            result = await _dialogs.ShowDialogAsync(dialog);
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
            dialog.SyncScrollingEnabled,
            dialog.WordWrapEnabled,
            dialog.SelectedFontFamily,
            dialog.SelectedFontSize);

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
        if (RootGrid.XamlRoot is null || _dialogs.IsDialogOpen)
        {
            return;
        }

        var dialog = new AboutDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = RootGrid.RequestedTheme,
        };

        await _dialogs.ShowDialogAsync(dialog);
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
                ApplySplitRatio(_editorSplitRatio);
                break;
        }
    }

    private void ApplySplitRatio(double ratio)
    {
        var clamped = Math.Clamp(ratio, 0.25, 0.75);
        EditorColumn.Width = new GridLength(clamped, GridUnitType.Star);
        PreviewColumn.Width = new GridLength(1 - clamped, GridUnitType.Star);
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

        _editorSplitRatio = editorWidth / total;
        ApplySplitRatio(_editorSplitRatio);
    }

    private void Splitter_ResetRequested(object? sender, EventArgs e)
    {
        _editorSplitRatio = 0.5;
        ApplySplitRatio(_editorSplitRatio);
    }

    private void RebuildRecentMenu()
    {
        var signature = BuildRecentMenuSignature();
        if (string.Equals(signature, _recentMenuSignature, StringComparison.Ordinal))
        {
            return;
        }

        _recentMenuSignature = signature;
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

        var activePath = ViewModel.ActiveDocument?.FilePath;
        var activeEntry = activePath is null
            ? null
            : ViewModel.RecentFiles.FirstOrDefault(entry =>
                string.Equals(entry.Path, activePath, StringComparison.OrdinalIgnoreCase));

        var pinItem = new MenuFlyoutItem
        {
            Text = activeEntry?.IsPinned == true
                ? "取消釘選目前檔案"
                : "釘選目前檔案",
            IsEnabled = activeEntry is not null,
        };
        pinItem.Click += async (_, _) =>
        {
            if (activeEntry is not null)
            {
                await ViewModel.ToggleRecentPinAsync(activeEntry.Path);
            }
        };

        var removeItem = new MenuFlyoutItem
        {
            Text = "從最近開啟移除目前檔案",
            IsEnabled = activeEntry is not null,
        };
        removeItem.Click += async (_, _) =>
        {
            if (activeEntry is not null)
            {
                await ViewModel.RemoveRecentAsync(activeEntry.Path);
            }
        };

        RecentSubMenu.Items.Add(pinItem);
        RecentSubMenu.Items.Add(removeItem);
        RecentSubMenu.Items.Add(new MenuFlyoutSeparator());

        var clearItem = new MenuFlyoutItem { Text = "清除最近檔案" };
        clearItem.Click += async (_, _) => await ViewModel.ClearRecentFilesAsync();
        RecentSubMenu.Items.Add(clearItem);
    }

    private string BuildRecentMenuSignature()
    {
        var entries = string.Join(
            '|',
            ViewModel.RecentFiles.Select(entry =>
                $"{entry.Path}\u0001{entry.DisplayName}\u0001{entry.IsPinned}\u0001{entry.IsMissing}"));
        return (ViewModel.ActiveDocument?.FilePath ?? string.Empty) + "\u0002" + entries;
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
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.NewDocumentCommand.Execute(null);
    }

    private void OpenAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.OpenDocumentCommand.Execute(null);
    }

    private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.SaveCommand.Execute(null);
    }

    private void SaveAsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.SaveAsCommand.Execute(null);
    }

    private void CloseDocumentAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.CloseActiveDocumentCommand.Execute(null);
    }

    private void ViewEditAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.SetViewModeCommand.Execute("Edit");
    }

    private void ViewPreviewAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.SetViewModeCommand.Execute("Preview");
    }

    private void ViewSplitAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.SetViewModeCommand.Execute("Split");
    }

    private void NextTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        _ = ViewModel.SelectAdjacentDocumentAsync(1);
    }

    private void PreviousTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        _ = ViewModel.SelectAdjacentDocumentAsync(-1);
    }

    private void PreferencesAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        _ = ShowPreferencesAsync();
    }

    private void ReopenClosedAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (HandleAccelerator(args))
        {
            return;
        }

        ViewModel.ReopenClosedDocumentCommand.Execute(null);
    }

    private bool HandleAccelerator(KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        return _isClosing || _dialogs.IsDialogOpen;
    }

    private void CompactWindowSizeItem_Click(object sender, RoutedEventArgs e)
    {
        ResizeWindowWidthToWorkArea(CompactWindowScale);
    }

    private void ComfortableWindowSizeItem_Click(object sender, RoutedEventArgs e)
    {
        ResizeWindowWidthToWorkArea(ComfortableWindowScale);
    }

    private void MaximizeWindowItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        RootGrid.IsHitTestVisible = false;

        try
        {
            await PersistBeforeCloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "視窗關閉前儲存狀態失敗或逾時，仍繼續關閉視窗。");
        }

        _allowClose = true;
        Close();
    }

    private async Task PersistBeforeCloseAsync()
    {
        await ViewModel.BeginClosingAsync();

        var windowState = AppWindow.Presenter is OverlappedPresenter
        {
            State: OverlappedPresenterState.Maximized,
        }
            ? "Maximized"
            : "Normal";

        await ViewModel.SaveWindowSettingsAsync(
            _lastNormalSize.Width,
            _lastNormalSize.Height,
            windowState,
            _lastNormalPosition.X,
            _lastNormalPosition.Y,
            _editorSplitRatio);
        await ViewModel.PersistSessionAsync();
    }
}
