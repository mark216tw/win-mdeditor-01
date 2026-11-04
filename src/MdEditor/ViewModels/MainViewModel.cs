using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;
using VibeCode.MdEditor.Core.Services;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;
using Windows.System;
using Windows.UI.ViewManagement;

namespace VibeCode.MdEditor.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IFileService _fileService;
    private readonly IMarkdownRenderer _markdownRenderer;
    private readonly IRecentFilesService _recentFiles;
    private readonly ISessionDraftService _sessionDrafts;
    private readonly ISettingsService _settings;
    private readonly IFileWatcherService _fileWatcher;
    private readonly IDocxExportService _docxExport;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Dictionary<string, DateTimeOffset> _watcherSuppression = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _documentSwitchLock = new(1, 1);
    private readonly UISettings _uiSettings = new();

    private IEditorHost? _editor;
    private IPreviewHost? _preview;
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _draftSaveCancellation;
    private CancellationTokenSource? _statsCancellation;
    private CancellationTokenSource? _textSyncCancellation;
    private CancellationTokenSource? _fontSizeSaveCancellation;
    private bool _isLoadingDocument;
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
        editor.ShortcutRequested += OnEditorShortcutRequested;
        editor.FontSizeChanged += OnEditorFontSizeChanged;
        preview.LinkRequested += OnPreviewLinkRequested;
        preview.FontSizeChanged += OnPreviewFontSizeChanged;

        if (!_settings.IsLoaded)
        {
            await _settings.LoadAsync();
        }

        await _recentFiles.InitializeAsync();
        await _recentFiles.PruneAsync();

        await editor.InitializeAsync();

        ViewMode = ParseViewMode(_settings.Settings.ViewMode);
        await editor.SetIncludeTextInChangeAsync(ViewMode != EditorViewMode.Edit);
        await editor.SetWordWrapAsync(_settings.Settings.WordWrap);
        await editor.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        await preview.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        await ApplyThemeAsync(_settings.Settings.Theme, save: false);
        await RestoreSessionAsync();

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

    public async Task SaveWindowSettingsAsync(
        double width,
        double height,
        string windowState,
        double? left,
        double? top)
    {
        _settings.Settings.WindowWidth = width;
        _settings.Settings.WindowHeight = height;
        _settings.Settings.WindowState = windowState;
        _settings.Settings.WindowLeft = left;
        _settings.Settings.WindowTop = top;
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
        double fontSize)
    {
        _settings.Settings.Theme = theme;
        _settings.Settings.SyncScrolling = syncScrolling;
        _settings.Settings.WordWrap = wordWrap;
        _settings.Settings.EditorFontSize = Math.Clamp(fontSize, 10, 28);
        await _settings.SaveAsync();
        await ApplyThemeAsync(theme, save: false);

        if (_editor is not null)
        {
            await _editor.SetWordWrapAsync(_settings.Settings.WordWrap);
            await _editor.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        }

        if (_preview is not null)
        {
            await _preview.SetFontSizeAsync(_settings.Settings.EditorFontSize);
        }

        OnPropertyChanged(nameof(CurrentTheme));
        OnPropertyChanged(nameof(SyncScrollingEnabled));
        OnPropertyChanged(nameof(WordWrapEnabled));
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
        }
        finally
        {
            _documentSwitchLock.Release();
        }
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

        var existing = Documents.FirstOrDefault(document =>
            document.FilePath is not null &&
            PathsEqual(document.FilePath, fullPath));

        if (existing is not null)
        {
            await SelectDocumentAsync(existing);
            return;
        }

        SetBusy(true, "正在開啟檔案...");

        try
        {
            var loaded = await _fileService.LoadAsync(fullPath);
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

        var wasActive = ReferenceEquals(document, ActiveDocument);

        if (document.FilePath is not null)
        {
            _fileWatcher.Unwatch(document.FilePath);
        }

        Documents.Remove(document);

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
        if (document is null || _preview is null)
        {
            return;
        }

        SetBusy(true, "正在匯出 PDF...");

        var previousViewMode = ViewMode;

        try
        {
            await FlushEditorAsync();

            var suggestedName = Path.GetFileNameWithoutExtension(document.Title);
            var path = await _dialogs.PickSaveFileAsync(suggestedName, "PDF 檔案", ".pdf");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ViewMode = EditorViewMode.Preview;
            await _preview.InitializeAsync();
            await RenderPreviewNowAsync(document);
            await _preview.PrintToPdfAsync(path);
            StatusText = $"已匯出 PDF：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ShowError("匯出 PDF 失敗", ex.Message);
        }
        finally
        {
            ViewMode = previousViewMode;
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
        if (!Enum.TryParse<EditorViewMode>(mode, ignoreCase: true, out var viewMode))
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

    private async Task<bool> SaveDocumentAsync(bool saveAs)
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

        SetBusy(true, "正在儲存...");

        try
        {
            var fullPath = Path.GetFullPath(path);
            _watcherSuppression[fullPath] = DateTimeOffset.UtcNow.AddSeconds(3);

            var stamp = await _fileService.SaveAsync(
                fullPath,
                document.Text,
                document.EncodingName,
                document.CodePage,
                document.HasBom,
                document.LineEnding);

            var oldPath = document.FilePath;
            document.FilePath = fullPath;
            document.Title = Path.GetFileName(fullPath);
            document.LastKnownHash = stamp.Hash;
            document.LastWriteTimeUtc = stamp.LastWriteTimeUtc;
            document.LastKnownLength = stamp.Length;
            document.IsDirty = false;
            document.IsExternalChange = false;
            document.ExternalChangeMessage = string.Empty;
            document.IsMissing = false;

            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath, fullPath))
            {
                _fileWatcher.Unwatch(oldPath);
            }

            _fileWatcher.Watch(fullPath);
            await _recentFiles.AddOrUpdateAsync(fullPath);
            await RenderPreviewNowAsync(document);

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

    private async Task LoadActiveDocumentAsync()
    {
        var document = ActiveDocument;
        if (document is null || _editor is null || _preview is null)
        {
            return;
        }

        CancelTextSync();
        _activeDocumentTextStale = false;
        _isLoadingDocument = true;
        _previewCancellation?.Cancel();

        try
        {
            CursorText = "行 1，欄 1";
            await _preview.SetDocumentFolderAsync(
                string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory);
            await _editor.SetContentAsync(document.Text, document.Id);
            await RenderPreviewNowAsync(document);
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

    private async Task FlushEditorAsync()
    {
        if (_editor is null || ActiveDocument is null || _isLoadingDocument)
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
                if (!document.IsDirty)
                {
                    document.IsDirty = true;
                }
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

    private async Task RenderPreviewNowAsync(DocumentViewModel? document)
    {
        if (document is null || _preview is null || ViewMode == EditorViewMode.Edit)
        {
            return;
        }

        try
        {
            var directory = string.IsNullOrEmpty(document.FileDirectory) ? null : document.FileDirectory;
            var html = await Task.Run(() => _markdownRenderer.Render(document.Text, directory));

            if (!ReferenceEquals(document, ActiveDocument))
            {
                return;
            }

            await _preview.SetDocumentFolderAsync(directory);
            await _preview.RenderAsync(
                html,
                _currentTheme,
                Guid.NewGuid().ToString("N"),
                document.Id);
        }
        catch (Exception ex)
        {
            ShowError("預覽失敗", ex.Message);
        }
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

        UpdateStatus();

        if (e.PropertyName is nameof(DocumentViewModel.FilePath))
        {
            RecentMenuStateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (e.PropertyName is nameof(DocumentViewModel.IsExternalChange) or
            nameof(DocumentViewModel.ExternalChangeMessage))
        {
            UpdateBanner();
        }

        if (e.PropertyName is nameof(DocumentViewModel.Title) or
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

            UpdateStatus();
            SchedulePreviewRender();
            ScheduleDraftSave();
            ScheduleTextSync();
            return;
        }

        _activeDocumentTextStale = false;

        if (string.Equals(document.Text, e.Text, StringComparison.Ordinal))
        {
            return;
        }

        document.Text = e.Text;
        document.Revision++;
        if (!document.IsDirty)
        {
            document.IsDirty = true;
        }

        UpdateStatus();
        SchedulePreviewRender();
        ScheduleDraftSave();
    }

    private void ScheduleTextSync()
    {
        CancelTextSync();

        var cancellation = new CancellationTokenSource();
        _textSyncCancellation = cancellation;
        _ = SyncEditorTextDebouncedAsync(cancellation.Token);
    }

    private void CancelTextSync()
    {
        var previous = _textSyncCancellation;
        _textSyncCancellation = null;
        previous?.Cancel();
        previous?.Dispose();
    }

    private async Task SyncEditorTextDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);

            if (cancellationToken.IsCancellationRequested || !_activeDocumentTextStale)
            {
                return;
            }

            await FlushEditorAsync();

            if (!cancellationToken.IsCancellationRequested)
            {
                UpdateStatus();
            }
        }
        catch (OperationCanceledException)
        {
        }
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

        _ = _preview.SetScrollPercentAsync(e.Percent);
    }

    private void OnEditorShortcutRequested(object? sender, string name)
    {
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
            case "zoomIn":
                _ = AdjustFontSizeAsync(1);
                break;
            case "zoomOut":
                _ = AdjustFontSizeAsync(-1);
                break;
            case "zoomReset":
                _ = ResetFontSizeAsync();
                break;
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

            var pathPart = href.Split('#')[0];
            if (string.IsNullOrWhiteSpace(pathPart))
            {
                return;
            }

            var path = Path.GetFullPath(Path.Combine(
                document.FileDirectory,
                Uri.UnescapeDataString(pathPart)));

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
        await FlushEditorAsync();
        await SaveSessionDraftsAsync();
    }

    private async Task RestoreSessionAsync()
    {
        DraftSession? session;

        try
        {
            session = await _sessionDrafts.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "載入暫存工作階段失敗。");
            return;
        }

        if (session is null)
        {
            return;
        }

        ViewMode = ParseViewMode(session.ViewMode);

        var restoredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var restored = await Task.WhenAll(
            session.Documents.Select(snapshot => CreateRestoredDocumentAsync(snapshot, restoredPaths)));

        foreach (var document in restored)
        {
            if (document is null)
            {
                continue;
            }

            Documents.Add(document);

            if (document.FilePath is not null && !document.IsMissing)
            {
                _fileWatcher.Watch(document.FilePath);
            }
        }

        if (Documents.Count == 0)
        {
            return;
        }

        var active = Documents.FirstOrDefault(document =>
            string.Equals(document.Id, session.ActiveDocumentId, StringComparison.Ordinal))
            ?? Documents[^1];

        ActiveDocument = active;
        await LoadActiveDocumentAsync();
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
            return document;
        }

        if (!File.Exists(fullPath))
        {
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
                _logger.LogWarning(ex, "還原檔案失敗，改用暫存內容：{Path}", fullPath);
            }
        }
        else
        {
            try
            {
                var stamp = await _fileService.ComputeStampAsync(fullPath);
                if (!string.Equals(stamp.Hash, snapshot.LastKnownHash, StringComparison.OrdinalIgnoreCase))
                {
                    document.IsExternalChange = true;
                    document.ExternalChangeMessage =
                        "檔案在程式關閉期間已變更。重新載入會覆蓋暫存內容。";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "檢查還原檔案變更失敗：{Path}", fullPath);
            }
        }

        document.IsDirty = true;
        return document;
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
        _textSyncCancellation?.Cancel();
        _textSyncCancellation?.Dispose();
        _fontSizeSaveCancellation?.Cancel();
        _fontSizeSaveCancellation?.Dispose();
        _documentSwitchLock.Dispose();
    }
}
