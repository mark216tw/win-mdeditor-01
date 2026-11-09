using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;
using VibeCode.MdEditor.Core.Services;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;
using VibeCode.MdEditor.Views;
using Windows.System;
using Windows.UI.ViewManagement;

namespace VibeCode.MdEditor.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxClosedDocuments = 10;

    private readonly IFileService _fileService;
    private readonly IMarkdownRenderer _markdownRenderer;
    private readonly IRecentFilesService _recentFiles;
    private readonly ISessionDraftService _sessionDrafts;
    private readonly ISettingsService _settings;
    private readonly IFileWatcherService _fileWatcher;
    private readonly IDocxExportService _docxExport;
    private readonly IHtmlExportService _htmlExport;
    private readonly ILineDiffService _lineDiff;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Dictionary<string, DateTimeOffset> _watcherSuppression = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingRestorePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ClosedDocumentSnapshot> _closedDocuments = [];
    private readonly SemaphoreSlim _documentSwitchLock = new(1, 1);
    private readonly UISettings _uiSettings = new();

    private double _lastSyncedScrollPercent = double.NaN;
    private double _lastSyncedScrollLine = double.NaN;

    private IEditorHost? _editor;
    private IPreviewHost? _preview;
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _draftSaveCancellation;
    private CancellationTokenSource? _statsCancellation;
    private CancellationTokenSource? _fontSizeSaveCancellation;
    private Task? _sessionRestoreTask;
    private TaskCompletionSource? _sessionRestoreCompletion;
    private long _previewRenderGeneration;
    private string? _lastPreviewDocumentId;
    private string? _lastPreviewText;
    private string? _lastPreviewDirectory;
    private bool _isLoadingDocument;
    private bool _isRestoringSession;
    private bool _isClosing;
    private bool _isExportingPdf;
    private bool _isExportingHtml;
    private bool _activeDocumentTextStale;
    private int _untitledCounter;
    private string _currentTheme = "light";
    private bool _disposed;

    public MainViewModel(
        IFileService fileService,
        IMarkdownRenderer markdownRenderer,
        IRecentFilesService recentFiles,
        ISessionDraftService sessionDrafts,
        ISettingsService settings,
        IFileWatcherService fileWatcher,
        IDocxExportService docxExport,
        IHtmlExportService htmlExport,
        ILineDiffService lineDiff,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        ILogger<MainViewModel> logger)
    {
        _fileService = fileService;
        _markdownRenderer = markdownRenderer;
        _recentFiles = recentFiles;
        _sessionDrafts = sessionDrafts;
        _settings = settings;
        _fileWatcher = fileWatcher;
        _docxExport = docxExport;
        _htmlExport = htmlExport;
        _lineDiff = lineDiff;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _logger = logger;
        _fileWatcher.FileChanged += OnFileChanged;

        ViewMode = EditorViewMode.Split;
        ExternalChangeMessage = string.Empty;
        ErrorMessage = string.Empty;
        BusyText = string.Empty;
        StatusText = "就緒";
        CursorText = "行 1，欄 1";
        WordCountText = "0 字";
        EncodingText = "UTF-8";
        LineEndingText = "LF";
        SaveStateText = "已儲存";
        WindowTitle = "Markdown 編輯器";
        FilePathText = "尚未開啟檔案";
    }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public ObservableCollection<RecentFileEntry> RecentFiles => _recentFiles.Items;

    [ObservableProperty]
    public partial DocumentViewModel? ActiveDocument { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewModeLabel))]
    [NotifyPropertyChangedFor(nameof(ViewModeButtonText))]
    [NotifyPropertyChangedFor(nameof(ViewModeToolTip))]
    public partial EditorViewMode ViewMode { get; set; }

    [ObservableProperty]
    public partial bool IsExternalChangeVisible { get; set; }

    [ObservableProperty]
    public partial string ExternalChangeMessage { get; set; }

    [ObservableProperty]
    public partial bool IsErrorVisible { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string CursorText { get; set; }

    [ObservableProperty]
    public partial string WordCountText { get; set; }

    [ObservableProperty]
    public partial string EncodingText { get; set; }

    [ObservableProperty]
    public partial string LineEndingText { get; set; }

    [ObservableProperty]
    public partial string SaveStateText { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; }

    [ObservableProperty]
    public partial string FilePathText { get; set; }

    public bool HasActiveFilePath => ActiveDocument?.FilePath is not null;

    public string ViewModeLabel => ViewMode switch
    {
        EditorViewMode.Edit => "編輯",
        EditorViewMode.Preview => "預覽",
        _ => "並排",
    };

    public string ViewModeButtonText => $"檢視：{ViewModeLabel}";

    public string ViewModeToolTip => $"{ViewModeButtonText}（點擊切換，Ctrl+1/2/3）";

    public string CurrentTheme => _settings.Settings.Theme;

    public bool SyncScrollingEnabled => _settings.Settings.SyncScrolling;

    public bool WordWrapEnabled => _settings.Settings.WordWrap;

    public string EditorFontFamily => _settings.Settings.EditorFontFamily;

    public double EditorFontSize => _settings.Settings.EditorFontSize;

    public event EventHandler<AppThemeDefinition>? ThemeChanged;

    public event EventHandler? PreferencesRequested;

    public event EventHandler? RecentMenuStateChanged;

    public async Task InitializeAsync(IEditorHost editor, IPreviewHost preview, string? filePath)
    {
        _editor = editor;
        _preview = preview;

        editor.ContentChanged += OnEditorContentChanged;
        editor.SelectionChanged += OnEditorSelectionChanged;
        editor.ScrollChanged += OnEditorScrollChanged;
        editor.ShortcutRequested += OnShortcutRequested;
        editor.FontSizeChanged += OnEditorFontSizeChanged;
        editor.ImagePasted += OnEditorImagePasted;
        preview.LinkRequested += OnPreviewLinkRequested;
        preview.ShortcutRequested += OnShortcutRequested;
        preview.FontSizeChanged += OnPreviewFontSizeChanged;
        preview.TaskToggled += OnPreviewTaskToggled;

        var editorInitTask = editor.InitializeAsync();
        var sessionLoadTask = LoadSessionDraftAsync();

        if (!_settings.IsLoaded)
        {
            await _settings.LoadAsync();
        }

        ViewMode = ParseViewMode(_settings.Settings.ViewMode);
        if (ViewMode != EditorViewMode.Edit)
        {
            _ = preview.InitializeAsync();
        }

        await _recentFiles.InitializeAsync();
        _ = PruneRecentFilesAsync();

        await editorInitTask;

        await editor.SetIncludeTextInChangeAsync(ViewMode != EditorViewMode.Edit);
        await editor.SetWordWrapAsync(_settings.Settings.WordWrap);
        await editor.SetFontFamilyAsync(_settings.Settings.EditorFontFamily);
        await editor.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        await preview.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        await ApplyThemeAsync(_settings.Settings.Theme, save: false);

        var session = await sessionLoadTask;
        await RestoreSessionAsync(session);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await OpenFileAsync(filePath);
        }

        if (Documents.Count == 0)
        {
            await NewDocumentAsync();
        }
        else if (ActiveDocument is null)
        {
            await SelectDocumentAsync(Documents[^1]);
        }

        UpdateStatus();
    }

    public async Task BeginClosingAsync()
    {
        _isClosing = true;
        if (_editor is not null)
        {
            await _editor.SetReadOnlyAsync(true);
        }
    }

    public async Task CancelClosingAsync()
    {
        _isClosing = false;
        if (_editor is not null)
        {
            await _editor.SetReadOnlyAsync(false);
            await _editor.FocusAsync();
        }
    }

    public (double Width, double Height) GetSavedWindowSize()
    {
        return (_settings.Settings.WindowWidth, _settings.Settings.WindowHeight);
    }

    public (double? Left, double? Top) GetSavedWindowPosition()
    {
        return (_settings.Settings.WindowLeft, _settings.Settings.WindowTop);
    }

    public string GetSavedWindowState()
    {
        return _settings.Settings.WindowState;
    }

    public double GetSavedSplitRatio()
    {
        return _settings.Settings.EditorSplitRatio;
    }

    public async Task SaveWindowSettingsAsync(
        double width,
        double height,
        string windowState,
        double? left,
        double? top,
        double editorSplitRatio)
    {
        _settings.Settings.WindowWidth = width;
        _settings.Settings.WindowHeight = height;
        _settings.Settings.WindowState = windowState;
        _settings.Settings.WindowLeft = left;
        _settings.Settings.WindowTop = top;
        _settings.Settings.EditorSplitRatio = Math.Clamp(editorSplitRatio, 0.25, 0.75);
        await _settings.SaveAsync();
    }

    public async Task SetThemeAsync(string theme)
    {
        _settings.Settings.Theme = theme;
        await _settings.SaveAsync();
        await ApplyThemeAsync(theme, save: false);
        OnPropertyChanged(nameof(CurrentTheme));
    }

    public async Task RefreshSystemThemeAsync()
    {
        if (!string.Equals(_settings.Settings.Theme, "System", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var resolved = AppThemeCatalog.Resolve("System", IsSystemDark());
        if (string.Equals(resolved.WebTheme, _currentTheme, StringComparison.Ordinal))
        {
            return;
        }

        await ApplyThemeAsync("System", save: false);
    }

    private bool IsSystemDark()
    {
        var background = _uiSettings.GetColorValue(UIColorType.Background);
        return background.R + background.G + background.B < 384;
    }

    public async Task ApplyPreferencesAsync(
        string theme,
        bool syncScrolling,
        bool wordWrap,
        string fontFamily,
        double fontSize)
    {
        _settings.Settings.Theme = theme;
        _settings.Settings.SyncScrolling = syncScrolling;
        _settings.Settings.WordWrap = wordWrap;
        _settings.Settings.EditorFontFamily = string.IsNullOrWhiteSpace(fontFamily)
            ? "Cascadia Mono"
            : fontFamily.Trim();
        _settings.Settings.EditorFontSize = Math.Clamp(fontSize, 10, 28);
        await _settings.SaveAsync();
        await ApplyThemeAsync(theme, save: false);

        if (_editor is not null)
        {
            await _editor.SetWordWrapAsync(_settings.Settings.WordWrap);
            await _editor.SetFontFamilyAsync(_settings.Settings.EditorFontFamily);
            await _editor.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        }

        if (_preview is not null)
        {
            await _preview.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        }

        OnPropertyChanged(nameof(CurrentTheme));
        OnPropertyChanged(nameof(SyncScrollingEnabled));
        OnPropertyChanged(nameof(WordWrapEnabled));
        OnPropertyChanged(nameof(EditorFontFamily));
        OnPropertyChanged(nameof(EditorFontSize));
    }

    public async Task SetWordWrapAsync(bool enabled)
    {
        _settings.Settings.WordWrap = enabled;
        await _settings.SaveAsync();

        if (_editor is not null)
        {
            await _editor.SetWordWrapAsync(enabled);
        }

        OnPropertyChanged(nameof(WordWrapEnabled));
        StatusText = enabled ? "已啟用自動換行" : "已停用自動換行";
    }

    public async Task SetFontSizeAsync(double size)
    {
        var clamped = Math.Clamp(Math.Round(size), 10, 28);
        _settings.Settings.EditorFontSize = clamped;
        await _settings.SaveAsync();
        await ApplyFontSizeAsync(clamped);
        OnPropertyChanged(nameof(EditorFontSize));
        StatusText = $"字型大小：{clamped:0}";
    }

    public Task AdjustFontSizeAsync(int delta)
    {
        return SetFontSizeAsync(_settings.Settings.EditorFontSize + delta);
    }

    public Task ResetFontSizeAsync()
    {
        return SetFontSizeAsync(14);
    }

    private async Task ApplyFontSizeAsync(double size)
    {
        if (_editor is not null)
        {
            await _editor.SetFontSizeAsync(size);
        }

        if (_preview is not null)
        {
            await _preview.SetFontSizeAsync(size);
        }
    }

    private void OnEditorFontSizeChanged(object? sender, double size)
    {
        HandleFontSizeChanged(size);
    }

    private void OnPreviewFontSizeChanged(object? sender, double size)
    {
        HandleFontSizeChanged(size);
    }

    private void HandleFontSizeChanged(double size)
    {
        var clamped = Math.Clamp(Math.Round(size), 10, 28);
        if (Math.Abs(_settings.Settings.EditorFontSize - clamped) < 0.5)
        {
            return;
        }

        _settings.Settings.EditorFontSize = clamped;
        OnPropertyChanged(nameof(EditorFontSize));
        StatusText = $"字型大小：{clamped:0}";
        _ = ApplyFontSizeAsync(clamped);
        ScheduleFontSizeSave();
    }

    private void ScheduleFontSizeSave()
    {
        var previous = _fontSizeSaveCancellation;
        _fontSizeSaveCancellation = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        _ = SaveFontSizeDebouncedAsync(_fontSizeSaveCancellation.Token);
    }

    private async Task SaveFontSizeDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            await _settings.SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task SelectDocumentAsync(DocumentViewModel document)
    {
        await _documentSwitchLock.WaitAsync();

        try
        {
            if (ReferenceEquals(document, ActiveDocument))
            {
                return;
            }

            await FlushEditorAsync();
            ActiveDocument = document;
            await LoadActiveDocumentAsync();
            await SaveSessionDraftsAsync();
            await ResolvePendingConflictAsync(document);
        }
        finally
        {
            _documentSwitchLock.Release();
        }
    }

    public async Task SelectAdjacentDocumentAsync(int offset)
    {
        if (Documents.Count <= 1 || ActiveDocument is null)
        {
            return;
        }

        var index = Documents.IndexOf(ActiveDocument);
        if (index < 0)
        {
            return;
        }

        var nextIndex = (index + (offset % Documents.Count) + Documents.Count) % Documents.Count;
        await SelectDocumentAsync(Documents[nextIndex]);
    }

    public async Task OpenFileAsync(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            ShowError("無法開啟檔案", ex.Message);
            return;
        }

        if (!SupportedFileTypes.IsSupported(fullPath))
        {
            ShowError(
                "不支援的檔案類型",
                "目前僅支援 .md、.markdown、.mdown、.mkd 與 .txt 檔案。");
            return;
        }

        var restoreCompletion = _sessionRestoreCompletion;
        if (_isRestoringSession &&
            restoreCompletion is not null &&
            _pendingRestorePaths.Contains(fullPath))
        {
            await restoreCompletion.Task;
        }

        var existing = Documents.FirstOrDefault(document =>
            document.FilePath is not null &&
            PathsEqual(document.FilePath, fullPath));

        if (existing is not null)
        {
            await SelectDocumentAsync(existing);
            return;
        }

        FileInfo fileInfo;
        try
        {
            fileInfo = new FileInfo(fullPath);
        }
        catch (Exception ex)
        {
            ShowError("無法開啟檔案", ex.Message);
            return;
        }

        if (fileInfo.Exists)
        {
            if (fileInfo.Length > FileService.MaxSupportedFileBytes)
            {
                ShowError(
                    "檔案過大",
                    $"檔案大小（{fileInfo.Length / (1024d * 1024d):F1} MB）超過 {FileService.MaxSupportedFileBytes / (1024 * 1024)} MB 上限，無法開啟。");
                return;
            }

            if (fileInfo.Length > FileService.LargeFileWarningBytes)
            {
                var confirmed = await _dialogs.ConfirmAsync(
                    "檔案較大",
                    $"「{Path.GetFileName(fullPath)}」大小為 {fileInfo.Length / (1024d * 1024d):F1} MB，開啟與即時預覽可能較慢。確定要繼續開啟嗎？",
                    "繼續開啟",
                    "取消");
                if (!confirmed)
                {
                    return;
                }
            }
        }

        SetBusy(true, "正在開啟檔案...");

        try
        {
            var loaded = await _fileService.LoadAsync(fullPath);
            existing = Documents.FirstOrDefault(document =>
                document.FilePath is not null &&
                PathsEqual(document.FilePath, fullPath));
            if (existing is not null)
            {
                await SelectDocumentAsync(existing);
                return;
            }

            var document = new DocumentViewModel(Path.GetFileName(fullPath))
            {
                FilePath = fullPath,
            };
            document.Load(loaded);

            Documents.Add(document);
            _fileWatcher.Watch(fullPath);
            _logger.LogInformation("開始監看檔案：{Path}", fullPath);
            await _recentFiles.AddOrUpdateAsync(fullPath);
            await SelectDocumentAsync(document);
            StatusText = $"已開啟：{document.Title}";
        }
        catch (Exception ex)
        {
            ShowError("無法開啟檔案", ex.Message);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    public async Task OpenRecentAsync(string path)
    {
        if (File.Exists(path))
        {
            await OpenFileAsync(path);
            return;
        }

        await _recentFiles.PruneAsync();
        ShowError("找不到檔案", path);
    }

    public async Task ClearRecentFilesAsync()
    {
        await _recentFiles.ClearAsync();
    }

    public Task ToggleRecentPinAsync(string path)
    {
        return _recentFiles.TogglePinAsync(path);
    }

    public Task RemoveRecentAsync(string path)
    {
        return _recentFiles.RemoveAsync(path);
    }

    public async Task CloseOtherDocumentsAsync(DocumentViewModel keep)
    {
        foreach (var document in Documents.Where(item => !ReferenceEquals(item, keep)).ToList())
        {
            await CloseDocumentAsync(document);

            if (Documents.Contains(document))
            {
                return;
            }
        }

        if (!ReferenceEquals(ActiveDocument, keep) && Documents.Contains(keep))
        {
            await SelectDocumentAsync(keep);
        }
    }

    public async Task CloseDocumentAsync(DocumentViewModel document)
    {
        if (!Documents.Contains(document))
        {
            return;
        }

        if (document.IsDirty)
        {
            await SelectDocumentAsync(document);
            await FlushEditorAsync();

            var decision = await _dialogs.ConfirmSaveAsync(
                "關閉文件",
                $"「{document.Title}」有尚未儲存的變更。關閉後會從應用程式移除，暫存也會一併刪除。");

            if (decision == SaveDecision.Cancel)
            {
                return;
            }

            if (decision == SaveDecision.Save && !await SaveDocumentAsync(saveAs: false))
            {
                return;
            }
        }

        if (ReferenceEquals(document, ActiveDocument))
        {
            await FlushEditorAsync();
        }

        RememberClosedDocument(document);
        var wasActive = ReferenceEquals(document, ActiveDocument);

        if (document.FilePath is not null)
        {
            _fileWatcher.Unwatch(document.FilePath);
        }

        Documents.Remove(document);

        if (_editor is not null)
        {
            try
            {
                await _editor.DisposeDocumentAsync(document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "釋放編輯器分頁快取失敗：{DocumentId}", document.Id);
            }
        }

        try
        {
            await _sessionDrafts.DeleteDocumentAsync(document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刪除文件暫存失敗：{DocumentId}", document.Id);
        }

        if (wasActive)
        {
            ActiveDocument = null;

            if (Documents.Count > 0)
            {
                await SelectDocumentAsync(Documents[^1]);
            }
            else
            {
                await NewDocumentAsync();
            }
        }

        await SaveSessionDraftsAsync();
    }

    [RelayCommand]
    private async Task ReopenClosedDocumentAsync()
    {
        while (_closedDocuments.Count > 0)
        {
            var snapshot = _closedDocuments[^1];
            _closedDocuments.RemoveAt(_closedDocuments.Count - 1);

            if (!snapshot.IsDirty && snapshot.FilePath is not null)
            {
                if (!File.Exists(snapshot.FilePath))
                {
                    continue;
                }

                await OpenFileAsync(snapshot.FilePath);
                return;
            }

            if (snapshot.FilePath is not null)
            {
                var existing = Documents.FirstOrDefault(document =>
                    document.FilePath is not null && PathsEqual(document.FilePath, snapshot.FilePath));
                if (existing is not null)
                {
                    await SelectDocumentAsync(existing);
                    return;
                }
            }

            var document = new DocumentViewModel(snapshot.Title)
            {
                FilePath = snapshot.FilePath,
                Text = snapshot.Text,
                EncodingName = snapshot.EncodingName,
                CodePage = snapshot.CodePage,
                HasBom = snapshot.HasBom,
                LineEnding = snapshot.LineEnding,
                IsDirty = snapshot.IsDirty || snapshot.Text.Length > 0,
            };
            document.SetSavedText(snapshot.SavedText);

            Documents.Add(document);
            await SelectDocumentAsync(document);

            if (document.FilePath is not null && File.Exists(document.FilePath))
            {
                _fileWatcher.Watch(document.FilePath);
            }

            return;
        }

        StatusText = "沒有可重新開啟的文件。";
    }

    private void RememberClosedDocument(DocumentViewModel document)
    {
        var keepText = document.IsDirty || string.IsNullOrEmpty(document.FilePath);
        _closedDocuments.Add(new ClosedDocumentSnapshot(
            document.Title,
            document.FilePath,
            keepText ? document.Text : string.Empty,
            keepText ? document.SavedText : string.Empty,
            document.EncodingName,
            document.CodePage,
            document.HasBom,
            document.LineEnding,
            document.IsDirty));

        if (_closedDocuments.Count > MaxClosedDocuments)
        {
            _closedDocuments.RemoveAt(0);
        }
    }

    [RelayCommand]
    private async Task NewDocumentAsync()
    {
        await FlushEditorAsync();

        var document = new DocumentViewModel($"未命名 {++_untitledCounter}");
        Documents.Add(document);
        await SelectDocumentAsync(document);

        if (_editor is not null)
        {
            await _editor.FocusAsync();
        }
    }

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        try
        {
            var path = await _dialogs.PickOpenFileAsync();
            if (!string.IsNullOrEmpty(path))
            {
                await OpenFileAsync(path);
            }
        }
        catch (Exception ex)
        {
            ShowError("無法開啟檔案", ex.Message);
        }
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        return SaveDocumentAsync(saveAs: false);
    }

    [RelayCommand]
    private Task SaveAsAsync()
    {
        return SaveDocumentAsync(saveAs: true);
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        foreach (var document in Documents.Where(item => item.IsDirty).ToList())
        {
            if (!ReferenceEquals(document, ActiveDocument))
            {
                await SelectDocumentAsync(document);
            }

            if (!await SaveDocumentAsync(saveAs: false))
            {
                return;
            }
        }

        StatusText = "已儲存全部文件";
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        var document = ActiveDocument;
        if (document is null || _preview is null || _isExportingPdf || _isExportingHtml)
        {
            return;
        }

        _isExportingPdf = true;
        SetBusy(true, "正在匯出 PDF...");

        var previousViewMode = ViewMode;
        var switchedViewMode = false;

        try
        {
            await FlushEditorAsync();

            var suggestedName = Path.GetFileNameWithoutExtension(document.Title);
            var path = await _dialogs.PickSaveFileAsync(suggestedName, "PDF 檔案", ".pdf");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (ViewMode == EditorViewMode.Edit)
            {
                ViewMode = EditorViewMode.Preview;
                switchedViewMode = true;
            }

            await _preview.InitializeAsync();
            await RenderPreviewNowAsync(document, force: true);
            await _preview.PrintToPdfAsync(path);
            StatusText = $"已匯出 PDF：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ShowError("匯出 PDF 失敗", ex.Message);
        }
        finally
        {
            if (switchedViewMode)
            {
                ViewMode = previousViewMode;
            }

            _isExportingPdf = false;
            SetBusy(false, string.Empty);
        }
    }

    [RelayCommand]
    private async Task ExportWordAsync()
    {
        var document = ActiveDocument;
        if (document is null)
        {
            return;
        }

        SetBusy(true, "正在匯出 Word...");

        try
        {
            await FlushEditorAsync();

            var suggestedName = Path.GetFileNameWithoutExtension(document.Title);
            var path = await _dialogs.PickSaveFileAsync(suggestedName, "Word 文件", ".docx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await _docxExport.ExportAsync(
                document.Text,
                path,
                string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory);
            StatusText = $"已匯出 Word：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ShowError("匯出 Word 失敗", ex.Message);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        var document = ActiveDocument;
        if (document is null || _preview is null || _isExportingHtml || _isExportingPdf)
        {
            return;
        }

        _isExportingHtml = true;
        SetBusy(true, "正在匯出 HTML...");

        try
        {
            await FlushEditorAsync();
            _previewCancellation?.Cancel();

            var suggestedName = Path.GetFileNameWithoutExtension(document.Title);
            var path = await _dialogs.PickSaveFileAsync(suggestedName, "HTML 檔案", ".html");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var rendered = await RenderPreviewNowAsync(document, force: true);
            if (!rendered)
            {
                throw new InvalidOperationException("預覽尚未就緒，無法匯出 HTML。");
            }

            var contentHtml = await _preview.GetRenderedHtmlAsync();
            var directory = string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory;

            await _htmlExport.ExportAsync(
                contentHtml,
                document.Title,
                _currentTheme,
                directory,
                path);

            StatusText = $"已匯出 HTML：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ShowError("匯出 HTML 失敗", ex.Message);
        }
        finally
        {
            _isExportingHtml = false;
            SetBusy(false, string.Empty);
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        var document = ActiveDocument;
        var path = document?.FilePath;
        if (document is null || path is null)
        {
            return;
        }

        if (document.IsDirty)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "重新載入",
                "重新載入會覆蓋目前編輯器內的內容，尚未儲存的變更將會遺失。確定要繼續嗎？",
                "重新載入",
                "取消");

            if (!confirmed)
            {
                return;
            }
        }

        SetBusy(true, "正在重新載入...");

        try
        {
            var loaded = await _fileService.LoadAsync(path);
            document.Load(loaded);
            await LoadActiveDocumentAsync();
            await SaveSessionDraftsAsync();
            StatusText = $"已重新載入：{document.Title}";
        }
        catch (Exception ex)
        {
            ShowError("重新載入失敗", ex.Message);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    [RelayCommand]
    private async Task FormatAsync(string? command)
    {
        if (_editor is null || string.IsNullOrEmpty(command))
        {
            return;
        }

        await _editor.ExecuteCommandAsync(command);
        await _editor.FocusAsync();
    }

    [RelayCommand]
    private async Task EditorCommandAsync(string? command)
    {
        if (_editor is null || string.IsNullOrEmpty(command))
        {
            return;
        }

        await _editor.ExecuteCommandAsync(command);
        await _editor.FocusAsync();
    }

    [RelayCommand]
    private Task ToggleWordWrapAsync()
    {
        return SetWordWrapAsync(!_settings.Settings.WordWrap);
    }

    [RelayCommand]
    private Task ZoomInAsync()
    {
        return AdjustFontSizeAsync(1);
    }

    [RelayCommand]
    private Task ZoomOutAsync()
    {
        return AdjustFontSizeAsync(-1);
    }

    [RelayCommand]
    private Task ZoomResetAsync()
    {
        return ResetFontSizeAsync();
    }

    [RelayCommand]
    private async Task SetViewModeAsync(string? mode)
    {
        if (_isExportingPdf || _isExportingHtml || !Enum.TryParse<EditorViewMode>(mode, ignoreCase: true, out var viewMode))
        {
            return;
        }

        ViewMode = viewMode;
        _settings.Settings.ViewMode = viewMode.ToString();
        await _settings.SaveAsync();
        ScheduleDraftSave();

        if (_editor is not null)
        {
            await _editor.SetIncludeTextInChangeAsync(viewMode != EditorViewMode.Edit);
        }

        if (viewMode == EditorViewMode.Edit)
        {
            await FlushEditorAsync();
            _previewCancellation?.Cancel();
        }
        else
        {
            await FlushEditorAsync();
            await RenderPreviewNowAsync(ActiveDocument);
        }

        if (viewMode != EditorViewMode.Preview && _editor is not null)
        {
            await _editor.FocusAsync();
        }
    }

    [RelayCommand]
    private async Task CycleViewModeAsync()
    {
        var next = ViewMode switch
        {
            EditorViewMode.Edit => EditorViewMode.Preview,
            EditorViewMode.Preview => EditorViewMode.Split,
            _ => EditorViewMode.Edit,
        };

        await SetViewModeAsync(next.ToString());
    }

    [RelayCommand]
    private async Task CloseActiveDocumentAsync()
    {
        var document = ActiveDocument;
        if (document is not null)
        {
            await CloseDocumentAsync(document);
        }
    }

    private async Task<bool> SaveDocumentAsync(bool saveAs, bool skipExternalCheck = false)
    {
        var document = ActiveDocument;
        if (document is null)
        {
            return false;
        }

        await FlushEditorAsync();

        var path = document.FilePath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            try
            {
                path = await _dialogs.PickSaveFileAsync(
                    document.Title,
                    "Markdown 檔案",
                    ".md");
            }
            catch (Exception ex)
            {
                ShowError("無法選擇儲存位置", ex.Message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            ShowError("無法儲存檔案", ex.Message);
            return false;
        }

        var duplicateTab = Documents.FirstOrDefault(item =>
            !ReferenceEquals(item, document) &&
            item.FilePath is not null &&
            PathsEqual(item.FilePath, fullPath));

        if (duplicateTab is not null)
        {
            var prompt = duplicateTab.IsDirty
                ? $"「{duplicateTab.Title}」已在另一個分頁開啟且有未儲存變更。覆蓋儲存將捨棄該分頁的變更並關閉該分頁，確定要繼續嗎？"
                : $"「{duplicateTab.Title}」已在另一個分頁開啟。覆蓋儲存將一併關閉該分頁，確定要繼續嗎？";

            var confirmed = await _dialogs.ConfirmAsync(
                "目標檔案已在分頁開啟",
                prompt,
                "覆蓋並關閉重複分頁",
                "取消");

            if (!confirmed)
            {
                return false;
            }
        }

        if (!saveAs &&
            !skipExternalCheck &&
            document.FilePath is not null &&
            !string.IsNullOrEmpty(document.LastKnownHash) &&
            PathsEqual(document.FilePath, fullPath))
        {
            var saveConflict = await CheckSaveConflictAsync(document, fullPath);
            switch (saveConflict)
            {
                case SaveConflictDecision.SaveAs:
                    return await SaveDocumentAsync(saveAs: true, skipExternalCheck: true);
                case SaveConflictDecision.Cancel:
                    return false;
            }
        }

        SetBusy(true, "正在儲存...");

        try
        {
            _watcherSuppression[fullPath] = DateTimeOffset.UtcNow.AddSeconds(3);

            var stamp = await _fileService.SaveAsync(
                fullPath,
                document.Text,
                document.EncodingName,
                document.CodePage,
                document.HasBom,
                document.LineEnding);

            if (duplicateTab is not null && Documents.Contains(duplicateTab))
            {
                _fileWatcher.Unwatch(fullPath);
                Documents.Remove(duplicateTab);

                if (_editor is not null)
                {
                    try
                    {
                        await _editor.DisposeDocumentAsync(duplicateTab.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "釋放重複分頁編輯器快取失敗：{DocumentId}", duplicateTab.Id);
                    }
                }

                try
                {
                    await _sessionDrafts.DeleteDocumentAsync(duplicateTab.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "刪除重複分頁暫存失敗：{DocumentId}", duplicateTab.Id);
                }
            }

            var oldPath = document.FilePath;
            document.FilePath = fullPath;
            document.Title = Path.GetFileName(fullPath);
            document.LastKnownHash = stamp.Hash;
            document.LastWriteTimeUtc = stamp.LastWriteTimeUtc;
            document.LastKnownLength = stamp.Length;
            document.MarkSaved();
            document.IsExternalChange = false;
            document.ExternalChangeMessage = string.Empty;
            document.IsMissing = false;
            document.PendingConflict = null;
            document.ConflictPromptDeferred = false;

            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath, fullPath))
            {
                _fileWatcher.Unwatch(oldPath);
            }

            _fileWatcher.Watch(fullPath);
            if (!IsRecentFileTracked(fullPath))
            {
                await _recentFiles.AddOrUpdateAsync(fullPath);
            }

            if (ViewMode != EditorViewMode.Edit && !IsPreviewUpToDate(document))
            {
                _ = RenderPreviewNowAsync(document);
            }

            StatusText = $"已儲存：{document.Title}";
            UpdateStatus();
            UpdateBanner();
            await SaveSessionDraftsAsync();
            return true;
        }
        catch (Exception ex)
        {
            ShowError("儲存失敗", ex.Message);
            return false;
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    private async Task<SaveConflictDecision> CheckSaveConflictAsync(DocumentViewModel document, string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return await _dialogs.ConfirmSaveConflictAsync(
                    "檔案已不存在",
                    $"「{document.Title}」對應的檔案已不存在。\n" +
                    "覆蓋儲存會重新建立檔案；另存新檔可選擇其他位置。");
            }

            var stamp = await _fileService.ComputeStampAsync(fullPath);
            if (string.Equals(stamp.Hash, document.LastKnownHash, StringComparison.OrdinalIgnoreCase))
            {
                return SaveConflictDecision.Overwrite;
            }

            return await _dialogs.ConfirmSaveConflictAsync(
                "檔案已在外部變更",
                $"「{document.Title}」的磁碟版本已在外部變更（{stamp.LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}）。\n" +
                "覆蓋儲存會取代磁碟上的版本；另存新檔可保留該版本。");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "儲存前檢查外部變更失敗：{Path}", fullPath);
            return SaveConflictDecision.Overwrite;
        }
    }

    private async Task LoadActiveDocumentAsync()
    {
        var document = ActiveDocument;
        if (document is null || _editor is null || _preview is null)
        {
            return;
        }

        _activeDocumentTextStale = false;
        _isLoadingDocument = true;
        _previewCancellation?.Cancel();

        if (ViewMode != EditorViewMode.Edit)
        {
            _ = RenderPreviewNowAsync(document);
        }

        try
        {
            await _editor.SetContentAsync(document.Text, document.Id);
        }
        catch (Exception ex)
        {
            ShowError("載入文件失敗", ex.Message);
        }
        finally
        {
            _isLoadingDocument = false;
            UpdateStatus();
            UpdateBanner();
        }
    }

    private async Task PruneRecentFilesAsync()
    {
        try
        {
            await _recentFiles.PruneAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "檢查最近檔案失敗。");
        }
    }

    private async Task FlushEditorAsync()
    {
        if (_editor is null || !_editor.IsReady || ActiveDocument is null || _isLoadingDocument)
        {
            return;
        }

        try
        {
            var document = ActiveDocument;
            var text = await _editor.GetContentAsync(document.Id);

            if (ReferenceEquals(document, ActiveDocument))
            {
                _activeDocumentTextStale = false;
            }

            if (!string.Equals(document.Text, text, StringComparison.Ordinal))
            {
                document.Text = text;
                document.Revision++;
                document.RefreshDirtyState();
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("取得編輯器內容逾時。");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "取得編輯器內容失敗。");
        }
    }

    private void SchedulePreviewRender()
    {
        var previous = _previewCancellation;
        _previewCancellation = null;
        previous?.Cancel();
        previous?.Dispose();

        if (ViewMode == EditorViewMode.Edit)
        {
            return;
        }

        var document = ActiveDocument;
        if (document is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        _ = RenderPreviewDebouncedAsync(document, cancellation.Token);
    }

    private async Task RenderPreviewDebouncedAsync(DocumentViewModel document, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !ReferenceEquals(document, ActiveDocument))
        {
            return;
        }

        await RenderPreviewNowAsync(document);
    }

    private async Task<bool> RenderPreviewNowAsync(DocumentViewModel? document, bool force = false)
    {
        if (document is null || _preview is null || (!force && ViewMode == EditorViewMode.Edit))
        {
            return false;
        }

        var generation = Interlocked.Increment(ref _previewRenderGeneration);
        var revision = document.Revision;
        var text = document.Text;
        var directory = string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory;

        try
        {
            var html = await Task.Run(() => _markdownRenderer.Render(text, directory));

            if (!ReferenceEquals(document, ActiveDocument) ||
                document.Revision != revision ||
                generation != Volatile.Read(ref _previewRenderGeneration))
            {
                return false;
            }

            await _preview.SetDocumentFolderAsync(directory);
            if (!ReferenceEquals(document, ActiveDocument) ||
                document.Revision != revision ||
                generation != Volatile.Read(ref _previewRenderGeneration))
            {
                return false;
            }

            await _preview.RenderAsync(
                html,
                _currentTheme,
                Guid.NewGuid().ToString("N"),
                document.Id);

            if (ReferenceEquals(document, ActiveDocument) &&
                ReferenceEquals(document.Text, text) &&
                generation == Volatile.Read(ref _previewRenderGeneration))
            {
                _lastPreviewDocumentId = document.Id;
                _lastPreviewText = text;
                _lastPreviewDirectory = directory;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            if (generation == Volatile.Read(ref _previewRenderGeneration))
            {
                ShowError("預覽失敗", ex.Message);
            }

            return false;
        }
    }

    private bool IsPreviewUpToDate(DocumentViewModel document)
    {
        var directory = string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory;

        return string.Equals(_lastPreviewDocumentId, document.Id, StringComparison.Ordinal) &&
               ReferenceEquals(_lastPreviewText, document.Text) &&
               string.Equals(_lastPreviewDirectory, directory, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRecentFileTracked(string path)
    {
        return _recentFiles.Items.Any(entry =>
            !entry.IsMissing &&
            string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ApplyThemeAsync(string theme, bool save)
    {
        var resolved = AppThemeCatalog.Resolve(
            theme,
            IsSystemDark());

        _currentTheme = resolved.WebTheme;
        ThemeChanged?.Invoke(this, resolved);

        if (_editor is not null)
        {
            await _editor.SetThemeAsync(resolved.WebTheme);
        }

        if (_preview is not null)
        {
            await _preview.SetThemeAsync(resolved.WebTheme);
        }

        if (save)
        {
            await _settings.SaveAsync();
        }
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        _lastSyncedScrollPercent = double.NaN;
        _lastSyncedScrollLine = double.NaN;

        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnDocumentPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnDocumentPropertyChanged;
        }

        UpdateStatus();
        UpdateBanner();
        RecentMenuStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ActiveDocument))
        {
            return;
        }

        var propertyName = e.PropertyName;

        if (propertyName is nameof(DocumentViewModel.Text) or
            nameof(DocumentViewModel.Title) or
            nameof(DocumentViewModel.FilePath) or
            nameof(DocumentViewModel.IsDirty) or
            nameof(DocumentViewModel.EncodingName) or
            nameof(DocumentViewModel.CodePage) or
            nameof(DocumentViewModel.HasBom) or
            nameof(DocumentViewModel.LineEnding))
        {
            UpdateStatus();
        }

        if (propertyName is nameof(DocumentViewModel.FilePath))
        {
            RecentMenuStateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (propertyName is nameof(DocumentViewModel.IsExternalChange) or
            nameof(DocumentViewModel.ExternalChangeMessage))
        {
            UpdateBanner();
        }

        if (propertyName is nameof(DocumentViewModel.Title) or
            nameof(DocumentViewModel.FilePath) or
            nameof(DocumentViewModel.IsDirty) or
            nameof(DocumentViewModel.EncodingName) or
            nameof(DocumentViewModel.CodePage) or
            nameof(DocumentViewModel.HasBom) or
            nameof(DocumentViewModel.LineEnding) or
            nameof(DocumentViewModel.LastKnownHash) or
            nameof(DocumentViewModel.LastWriteTimeUtc))
        {
            if (sender is DocumentViewModel changed)
            {
                changed.Revision++;
            }

            ScheduleDraftSave();
        }
    }

    private void OnEditorContentChanged(object? sender, EditorContentChangedEventArgs e)
    {
        if (_isLoadingDocument)
        {
            return;
        }

        var document = ActiveDocument;
        if (document is null || !string.Equals(document.Id, e.DocumentId, StringComparison.Ordinal))
        {
            return;
        }

        if (e.Text is null)
        {
            _activeDocumentTextStale = true;

            if (!document.IsDirty)
            {
                document.IsDirty = true;
            }

            ScheduleDraftSave();
            return;
        }

        _activeDocumentTextStale = false;

        if (string.Equals(document.Text, e.Text, StringComparison.Ordinal))
        {
            return;
        }

        document.Text = e.Text;
        document.Revision++;
        document.RefreshDirtyState();

        SchedulePreviewRender();
        ScheduleDraftSave();
    }

    private void OnEditorSelectionChanged(object? sender, EditorSelectionChangedEventArgs e)
    {
        var document = ActiveDocument;
        if (document is null || !string.Equals(document.Id, e.DocumentId, StringComparison.Ordinal))
        {
            return;
        }

        CursorText = e.SelectionLength > 0
            ? $"行 {e.Line}，欄 {e.Column}（已選 {e.SelectionLength} 字元）"
            : $"行 {e.Line}，欄 {e.Column}";
    }

    private void OnEditorScrollChanged(object? sender, EditorScrollEventArgs e)
    {
        var document = ActiveDocument;
        if (document is null ||
            !string.Equals(document.Id, e.DocumentId, StringComparison.Ordinal) ||
            ViewMode != EditorViewMode.Split ||
            !_settings.Settings.SyncScrolling ||
            _preview is null)
        {
            return;
        }

        if (Math.Abs(e.Percent - _lastSyncedScrollPercent) < 0.0005 &&
            Math.Abs(e.Line - _lastSyncedScrollLine) < 0.05)
        {
            return;
        }

        _lastSyncedScrollPercent = e.Percent;
        _lastSyncedScrollLine = e.Line;
        _ = SyncPreviewScrollAsync(e.Percent, e.Line);
    }

    private async Task SyncPreviewScrollAsync(double percent, double line)
    {
        try
        {
            if (_preview is not null)
            {
                await _preview.SetScrollPercentAsync(percent, line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "同步預覽捲動失敗。");
        }
    }

    private void OnShortcutRequested(object? sender, string name)
    {
        if (_isClosing || _isExportingPdf || _isExportingHtml)
        {
            return;
        }

        switch (name)
        {
            case "save":
                _ = SaveDocumentAsync(saveAs: false);
                break;
            case "saveAs":
                _ = SaveDocumentAsync(saveAs: true);
                break;
            case "new":
                _ = NewDocumentAsync();
                break;
            case "open":
                _ = OpenDocumentAsync();
                break;
            case "viewEdit":
                _ = SetViewModeAsync("Edit");
                break;
            case "viewPreview":
                _ = SetViewModeAsync("Preview");
                break;
            case "viewSplit":
                _ = SetViewModeAsync("Split");
                break;
            case "close":
                _ = CloseActiveDocumentAsync();
                break;
            case "preferences":
                PreferencesRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "reopenClosed":
                _ = ReopenClosedDocumentAsync();
                break;
            case "zoomIn":
                _ = AdjustFontSizeAsync(1);
                break;
            case "zoomOut":
                _ = AdjustFontSizeAsync(-1);
                break;
            case "zoomReset":
                _ = ResetFontSizeAsync();
                break;
            case "nextTab":
                _ = SelectAdjacentDocumentAsync(1);
                break;
            case "previousTab":
                _ = SelectAdjacentDocumentAsync(-1);
                break;
        }
    }

    private void OnPreviewTaskToggled(object? sender, PreviewTaskToggledEventArgs e)
    {
        _ = HandlePreviewTaskToggledAsync(e);
    }

    private async Task HandlePreviewTaskToggledAsync(PreviewTaskToggledEventArgs e)
    {
        if (_isClosing || _editor is null)
        {
            return;
        }

        var document = ActiveDocument;
        if (document is null || !string.Equals(document.Id, e.DocumentId, StringComparison.Ordinal))
        {
            return;
        }

        await _editor.ToggleTaskLineAsync(document.Id, e.Line, e.IsChecked);
    }

    private void OnEditorImagePasted(object? sender, EditorImagePasteEventArgs e)
    {
        _ = HandleEditorImagePastedAsync(e);
    }

    private async Task HandleEditorImagePastedAsync(EditorImagePasteEventArgs e)
    {
        if (_isClosing || _editor is null)
        {
            return;
        }

        var document = ActiveDocument;
        if (document is null || !string.Equals(document.Id, e.DocumentId, StringComparison.Ordinal))
        {
            return;
        }

        if (e.IsTooLarge)
        {
            ShowError(
                "無法貼上圖片",
                $"剪貼簿圖片超過 {FileService.MaxPastedImageBytes / (1024 * 1024)} MB 上限。");
            return;
        }

        if (string.IsNullOrWhiteSpace(document.FileDirectory))
        {
            ShowError("無法貼上圖片", "請先儲存目前文件（Ctrl+S），再貼上剪貼簿圖片。");
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(e.Base64Data);
            var relativePath = await _fileService.SavePastedImageAsync(
                document.FileDirectory,
                bytes,
                e.MimeType);
            await _editor.InsertTextAsync(document.Id, $"![圖片]({relativePath})");
            StatusText = $"已儲存貼上圖片：{relativePath}";
        }
        catch (Exception ex)
        {
            ShowError("貼上圖片失敗", ex.Message);
        }
    }

    private void OnPreviewLinkRequested(object? sender, string href)
    {
        _ = OpenPreviewLinkAsync(href);
    }

    private async Task OpenPreviewLinkAsync(string href)
    {
        try
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 uri.Scheme == Uri.UriSchemeMailto))
            {
                await Launcher.LaunchUriAsync(uri);
                return;
            }

            var document = ActiveDocument;
            if (document is null || string.IsNullOrEmpty(document.FileDirectory))
            {
                return;
            }

            var pathPart = href.Split('#')[0].Split('?')[0];
            if (string.IsNullOrWhiteSpace(pathPart))
            {
                return;
            }

            var path = PathSafety.TryResolveRelativePathWithinDirectory(document.FileDirectory, pathPart);
            if (path is null)
            {
                ShowError("無法開啟連結", "連結指向文件資料夾以外的位置。");
                return;
            }

            if (File.Exists(path))
            {
                await OpenFileAsync(path);
            }
            else
            {
                ShowError("找不到連結檔案", path);
            }
        }
        catch (Exception ex)
        {
            ShowError("無法開啟連結", ex.Message);
        }
    }

    private void OnFileChanged(object? sender, FileWatcherEventArgs e)
    {
        _dispatcher.TryEnqueue(() => _ = HandleFileChangedAsync(e));
    }

    private async Task HandleFileChangedAsync(FileWatcherEventArgs e)
    {
        var fullPath = Path.GetFullPath(e.Path);
        var document = Documents.FirstOrDefault(item =>
            item.FilePath is not null &&
            PathsEqual(item.FilePath, fullPath));

        if (document is null)
        {
            return;
        }

        if (IsSuppressed(fullPath))
        {
            return;
        }

        if (e.Kind == FileChangeKind.Deleted)
        {
            MarkMissing(document);
            return;
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                MarkMissing(document);
                return;
            }

            if (info.LastWriteTimeUtc == document.LastWriteTimeUtc.UtcDateTime &&
                info.Length == document.LastKnownLength)
            {
                return;
            }

            var stamp = await _fileService.ComputeStampAsync(fullPath);
            if (IsSuppressed(fullPath) ||
                string.Equals(stamp.Hash, document.LastKnownHash, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _logger.LogInformation("偵測到外部變更：{Path}", fullPath);

            document.IsMissing = false;
            document.IsExternalChange = true;
            document.ExternalChangeMessage =
                $"檔案已在外部變更（{stamp.LastWriteTimeUtc.ToLocalTime():HH:mm:ss}）。重新載入會覆蓋目前編輯器內的版本。";

            if (ReferenceEquals(document, ActiveDocument))
            {
                UpdateBanner();
            }
        }
        catch (FileNotFoundException)
        {
            MarkMissing(document);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "檢查外部變更失敗：{Path}", fullPath);
        }
    }

    private void MarkMissing(DocumentViewModel document)
    {
        document.IsMissing = true;
        document.IsExternalChange = true;
        document.ExternalChangeMessage = "檔案已刪除或被移動。";

        if (ReferenceEquals(document, ActiveDocument))
        {
            UpdateBanner();
        }
    }

    private bool IsSuppressed(string path)
    {
        if (!_watcherSuppression.TryGetValue(path, out var until))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow >= until)
        {
            _watcherSuppression.Remove(path);
            return false;
        }

        return true;
    }

    private void UpdateStatus()
    {
        var document = ActiveDocument;

        if (document is null)
        {
            CancelStatsUpdate();
            WordCountText = "0 字";
            EncodingText = "UTF-8";
            LineEndingText = "LF";
            SaveStateText = "已儲存";
            WindowTitle = "Markdown 編輯器";
            FilePathText = "尚未開啟檔案";
            OnPropertyChanged(nameof(HasActiveFilePath));
            return;
        }

        EncodingText = document.HasBom
            ? $"{document.EncodingName} (BOM)"
            : document.EncodingName;
        LineEndingText = document.LineEnding;
        SaveStateText = document.IsDirty ? "未儲存" : "已儲存";
        WindowTitle = $"{document.Title}{(document.IsDirty ? " *" : string.Empty)} - Markdown 編輯器";
        FilePathText = document.FilePath ?? "尚未儲存";
        OnPropertyChanged(nameof(HasActiveFilePath));
        ScheduleWordCountUpdate(document);
    }

    private void CancelStatsUpdate()
    {
        var previous = _statsCancellation;
        _statsCancellation = null;
        previous?.Cancel();
        previous?.Dispose();
    }

    private void ScheduleWordCountUpdate(DocumentViewModel document)
    {
        CancelStatsUpdate();

        var cancellation = new CancellationTokenSource();
        _statsCancellation = cancellation;
        _ = UpdateWordCountDebouncedAsync(document, cancellation.Token);
    }

    private async Task UpdateWordCountDebouncedAsync(
        DocumentViewModel document,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);

            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(document, ActiveDocument))
            {
                return;
            }

            var (words, characters) = await Task.Run(
                () => (document.WordCount, document.CharCount),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(document, ActiveDocument))
            {
                return;
            }

            WordCountText = $"{words:N0} 字 · {characters:N0} 字元";
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateBanner()
    {
        var document = ActiveDocument;
        IsExternalChangeVisible = document?.IsExternalChange == true;
        ExternalChangeMessage = document?.ExternalChangeMessage ?? string.Empty;
    }

    private void SetBusy(bool busy, string text)
    {
        IsBusy = busy;
        BusyText = text;
    }

    private void ShowError(string title, string message)
    {
        _logger.LogError("{Title}: {Message}", title, message);
        ErrorMessage = $"{title}：{message}";
        IsErrorVisible = true;
    }

    private static EditorViewMode ParseViewMode(string value)
    {
        return Enum.TryParse<EditorViewMode>(value, ignoreCase: true, out var mode)
            ? mode
            : EditorViewMode.Split;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task PersistSessionAsync()
    {
        var restoreTask = _sessionRestoreTask;
        if (restoreTask is not null)
        {
            try
            {
                await restoreTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等待背景工作階段還原失敗。");
            }
        }

        await FlushEditorAsync();
        await SaveSessionDraftsAsync();
    }

    private async Task<DraftSession?> LoadSessionDraftAsync()
    {
        try
        {
            return await _sessionDrafts.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "載入暫存工作階段失敗。");
            return null;
        }
    }

    private async Task RestoreSessionAsync(DraftSession? session)
    {
        if (session is null)
        {
            return;
        }

        ViewMode = ParseViewMode(session.ViewMode);

        if (session.Documents.Count == 0)
        {
            return;
        }

        _isRestoringSession = true;
        _sessionRestoreCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRestorePaths.Clear();
        foreach (var snapshot in session.Documents)
        {
            if (string.IsNullOrWhiteSpace(snapshot.FilePath))
            {
                continue;
            }

            try
            {
                _pendingRestorePaths.Add(Path.GetFullPath(snapshot.FilePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "建立待還原路徑索引失敗：{Path}", snapshot.FilePath);
            }
        }

        var activeIndex = 0;
        for (var index = 0; index < session.Documents.Count; index++)
        {
            if (string.Equals(
                    session.Documents[index].DocumentId,
                    session.ActiveDocumentId,
                    StringComparison.Ordinal))
            {
                activeIndex = index;
                break;
            }
        }

        var restoredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = await CreateRestoredDocumentAsync(session.Documents[activeIndex], restoredPaths);
        if (active is null)
        {
            for (var index = 0; index < session.Documents.Count; index++)
            {
                if (index == activeIndex)
                {
                    continue;
                }

                active = await CreateRestoredDocumentAsync(session.Documents[index], restoredPaths);
                if (active is not null)
                {
                    activeIndex = index;
                    break;
                }
            }
        }

        if (active is null)
        {
            CompleteSessionRestore();
            return;
        }

        Documents.Add(active);
        WatchRestoredDocument(active);
        ActiveDocument = active;
        await LoadActiveDocumentAsync();
        await ResolvePendingConflictAsync(active);

        _sessionRestoreTask = RestoreRemainingDocumentsAsync(
            session.Documents,
            activeIndex,
            restoredPaths);
    }

    private async Task RestoreRemainingDocumentsAsync(
        IReadOnlyList<DraftDocumentSnapshot> snapshots,
        int activeIndex,
        HashSet<string> restoredPaths)
    {
        (int Index, DocumentViewModel? Document)[] restored;
        try
        {
            using var concurrency = new SemaphoreSlim(2, 2);
            var tasks = snapshots
                .Select((snapshot, index) => (Snapshot: snapshot, Index: index))
                .Where(item => item.Index != activeIndex)
                .Select(async item =>
                {
                    await concurrency.WaitAsync();
                    try
                    {
                        var document = await Task.Run(
                            () => CreateRestoredDocumentAsync(item.Snapshot, restoredPaths));
                        return (item.Index, Document: document);
                    }
                    finally
                    {
                        concurrency.Release();
                    }
                })
                .ToArray();

            restored = await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "背景還原工作階段失敗。");
            restored = [];
        }

        if (_disposed)
        {
            CompleteSessionRestore();
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    var addedAny = false;
                    foreach (var item in restored.OrderBy(item => item.Index))
                    {
                        var document = item.Document;
                        if (document is null || IsDocumentOpen(document))
                        {
                            continue;
                        }

                        Documents.Insert(Math.Min(item.Index, Documents.Count), document);
                        WatchRestoredDocument(document);
                        addedAny = true;
                    }

                    if (addedAny)
                    {
                        ScheduleDraftSave();
                    }
                }
                finally
                {
                    CompleteSessionRestore();
                    completion.TrySetResult();
                }
            }))
        {
            CompleteSessionRestore();
            return;
        }

        await completion.Task;
    }

    private void CompleteSessionRestore()
    {
        _isRestoringSession = false;
        _pendingRestorePaths.Clear();
        _sessionRestoreCompletion?.TrySetResult();
    }

    private bool IsDocumentOpen(DocumentViewModel document)
    {
        return Documents.Any(existing =>
            string.Equals(existing.Id, document.Id, StringComparison.Ordinal) ||
            (existing.FilePath is not null &&
             document.FilePath is not null &&
             PathsEqual(existing.FilePath, document.FilePath)));
    }

    private void WatchRestoredDocument(DocumentViewModel document)
    {
        if (document.FilePath is not null && !document.IsMissing)
        {
            _fileWatcher.Watch(document.FilePath);
        }
    }

    private async Task<DocumentViewModel?> CreateRestoredDocumentAsync(
        DraftDocumentSnapshot snapshot,
        HashSet<string> restoredPaths)
    {
        string? fullPath = null;

        if (!string.IsNullOrWhiteSpace(snapshot.FilePath))
        {
            try
            {
                fullPath = Path.GetFullPath(snapshot.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "還原文件路徑失敗：{Path}", snapshot.FilePath);
                return null;
            }

            lock (restoredPaths)
            {
                if (!restoredPaths.Add(fullPath))
                {
                    return null;
                }
            }
        }

        var document = new DocumentViewModel(snapshot.Title, snapshot.DocumentId)
        {
            FilePath = fullPath,
            Text = snapshot.Text,
            EncodingName = snapshot.EncodingName,
            CodePage = snapshot.CodePage,
            HasBom = snapshot.HasBom,
            LineEnding = snapshot.LineEnding,
            LastKnownHash = snapshot.LastKnownHash,
            LastWriteTimeUtc = snapshot.LastWriteTimeUtc,
            IsDirty = snapshot.IsDirty,
            Revision = snapshot.Revision,
        };

        if (fullPath is null)
        {
            document.SetSavedText(string.Empty);
            return document;
        }

        if (!File.Exists(fullPath))
        {
            document.SetSavedText(snapshot.Text);
            document.IsDirty = true;
            document.IsMissing = true;
            document.IsExternalChange = true;
            document.ExternalChangeMessage = "檔案已刪除或被移動。";
            return document;
        }

        if (!snapshot.IsDirty)
        {
            try
            {
                var loaded = await _fileService.LoadAsync(fullPath);
                document.Load(loaded);
                document.Title = Path.GetFileName(fullPath);
                return document;
            }
            catch (Exception ex)
            {
                document.SetSavedText(snapshot.Text);
                _logger.LogWarning(ex, "還原檔案失敗，改用暫存內容：{Path}", fullPath);
            }
        }
        else
        {
            try
            {
                var loaded = await _fileService.LoadAsync(fullPath);
                document.SetSavedText(loaded.Text);
                if (!string.Equals(loaded.Hash, snapshot.LastKnownHash, StringComparison.OrdinalIgnoreCase))
                {
                    document.PendingConflict = new DocumentConflict(
                        snapshot.Text,
                        loaded.Text,
                        loaded.Hash,
                        loaded.LastWriteTimeUtc,
                        loaded.Length);
                    document.IsExternalChange = true;
                    document.ExternalChangeMessage =
                        "檔案在程式關閉期間已變更。重新載入會覆蓋暫存內容。";
                }
            }
            catch (Exception ex)
            {
                document.SetSavedText(snapshot.Text);
                _logger.LogWarning(ex, "檢查還原檔案變更失敗：{Path}", fullPath);
            }
        }

        document.IsDirty = true;
        return document;
    }

    private async Task ResolvePendingConflictAsync(DocumentViewModel document)
    {
        if (document.PendingConflict is null ||
            document.ConflictPromptDeferred ||
            _disposed ||
            _isClosing)
        {
            return;
        }

        while (document.PendingConflict is not null)
        {
            var dialog = new ConflictResolutionDialog(document.Title, document.PendingConflict);
            if (await ShowDialogSafeAsync(dialog) is null)
            {
                document.ConflictPromptDeferred = true;
                return;
            }

            switch (dialog.Choice)
            {
                case ConflictResolution.KeepDraft:
                    document.PendingConflict = null;
                    document.ConflictPromptDeferred = false;
                    return;

                case ConflictResolution.LoadDisk:
                    await ApplyDiskVersionAsync(document);
                    return;

                case ConflictResolution.Compare:
                    var compare = new ConflictCompareDialog(
                        document.Title,
                        document.Text,
                        document.PendingConflict.DiskText,
                        _lineDiff);
                    var result = await ShowDialogSafeAsync(compare);
                    if (result is null)
                    {
                        document.ConflictPromptDeferred = true;
                        return;
                    }

                    if (result == ContentDialogResult.Primary)
                    {
                        document.PendingConflict = null;
                        document.ConflictPromptDeferred = false;
                        return;
                    }

                    if (result == ContentDialogResult.Secondary)
                    {
                        await ApplyDiskVersionAsync(document);
                        return;
                    }

                    break;

                default:
                    document.ConflictPromptDeferred = true;
                    return;
            }
        }
    }

    private async Task<ContentDialogResult?> ShowDialogSafeAsync(ContentDialog dialog)
    {
        try
        {
            return await _dialogs.ShowDialogAsync(dialog);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "顯示衝突對話框失敗。");
            return null;
        }
    }

    private async Task<bool> ApplyDiskVersionAsync(DocumentViewModel document)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return false;
        }

        try
        {
            var loaded = await _fileService.LoadAsync(document.FilePath);
            document.ApplyDiskVersion(loaded);

            if (ReferenceEquals(document, ActiveDocument))
            {
                await LoadActiveDocumentAsync();
            }

            await SaveSessionDraftsAsync();
            return true;
        }
        catch (Exception ex)
        {
            ShowError("無法載入磁碟版本", ex.Message);
            return false;
        }
    }

    private void ScheduleDraftSave()
    {
        if (_disposed)
        {
            return;
        }

        var previous = _draftSaveCancellation;
        _draftSaveCancellation = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        var cancellationToken = _draftSaveCancellation.Token;
        _ = SaveDraftDebouncedAsync(cancellationToken);
    }

    private async Task SaveDraftDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(1500, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await SaveSessionDraftsAsync(cancellationToken);
    }

    private async Task SaveSessionDraftsAsync(CancellationToken cancellationToken = default)
    {
        if (_isRestoringSession)
        {
            return;
        }

        if (_disposed)
        {
            return;
        }

        try
        {
            if (_activeDocumentTextStale && !_isLoadingDocument)
            {
                await FlushEditorAsync();

                if (_activeDocumentTextStale)
                {
                    return;
                }
            }

            var session = BuildDraftSession();
            await Task.Run(
                () => _sessionDrafts.SaveAsync(session, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "儲存暫存工作階段失敗。");
        }
    }

    private DraftSession BuildDraftSession()
    {
        var documents = Documents
            .Select(document => new DraftDocumentSnapshot(
                document.Id,
                document.Title,
                document.FilePath,
                document.Text,
                document.EncodingName,
                document.CodePage,
                document.HasBom,
                document.LineEnding,
                document.IsDirty,
                document.LastKnownHash,
                document.LastWriteTimeUtc,
                document.Revision,
                document.IsDirty || document.IsMissing || string.IsNullOrEmpty(document.FilePath)))
            .ToList();

        return new DraftSession(
            ActiveDocument?.Id,
            ViewMode.ToString(),
            documents);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fileWatcher.FileChanged -= OnFileChanged;
        _fileWatcher.Dispose();
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _draftSaveCancellation?.Cancel();
        _draftSaveCancellation?.Dispose();
        _statsCancellation?.Cancel();
        _statsCancellation?.Dispose();
        _fontSizeSaveCancellation?.Cancel();
        _fontSizeSaveCancellation?.Dispose();
        _documentSwitchLock.Dispose();
    }

    private sealed record ClosedDocumentSnapshot(
        string Title,
        string? FilePath,
        string Text,
        string SavedText,
        string EncodingName,
        int CodePage,
        bool HasBom,
        string LineEnding,
        bool IsDirty);
}
