# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-04 - v0.10.3

### 目標
關閉視窗時不再詢問是否儲存，直接關閉程式。

### 變更
- `MainWindow`：`OnAppWindowClosing` 不再呼叫關閉確認，保留最後一次工作階段暫存與視窗設定儲存後直接關閉。
- `MainViewModel`：移除不再使用的 `ConfirmCloseApplicationAsync`。
- 文件更新（README、使用指南、系統設計、CHANGELOG、RELEASE_NOTES）。
- 版號更新為 0.10.3。

### 問題與解法
- 原有流程會先取消關閉再顯示確認對話框；移除確認後仍保留 `Cancel = true` 等待暫存與視窗設定寫入，完成後再關閉，避免遺失最後編輯內容。
- 關閉單一文件的未儲存確認不受影響，仍可選擇儲存、不儲存或取消。

### 待辦／已知限制
- 關閉程式不再提供「儲存後結束」選項；未儲存內容僅保留於工作階段暫存。

## 2026-11-04 - v0.10.2

### 目標
修正「最近開啟」項目右鍵選單會關閉系統選單的問題，改用不影響外層選單的管理方式。

### 變更
- `MainWindow.RebuildRecentMenu`：移除每個最近檔案項目的 `ContextFlyout`；最近檔案恢復單擊開啟。
- 在「最近開啟」子選單底部新增「釘選目前檔案／取消釘選目前檔案」與「從最近開啟移除目前檔案」。
- `MainViewModel`：新增 `RecentMenuStateChanged` 事件，於切換使用中文件或 `FilePath` 變更時觸發。
- `MainWindow`：訂閱此事件以更新子選單狀態；移除的檔案仍保持開啟，不會關閉文件。
- 文件更新（README、使用指南、CHANGELOG、RELEASE_NOTES）。
- 版號更新為 0.10.2。

### 問題與解法
- 原因是 WinUI 一次只能顯示一個 Flyout，將 `ContextFlyout` 掛在 `MenuFlyoutItem` 上時，開啟子選單會關閉外層「最近開啟」選單。
- 改為在子選單內直接提供目前檔案的管理動作，避免巢狀 Flyout。
- 為避免每次文字變更都重建選單，改用 `RecentMenuStateChanged` 僅在文件切換或路徑變更時重建。

### 待辦／已知限制
- 目前只能管理「目前開啟」的檔案；若需管理任意最近檔案，需再新增管理清單對話框。

## 2026-11-04 - v0.10.1

### 目標
讓編輯器與預覽使用同一個字型大小設定，並支援 `Ctrl+滑鼠滾輪` 快速縮放。

### 變更
- `IPreviewHost`／`PreviewHostView`：新增 `SetFontSizeAsync` 與 `FontSizeChanged` 事件。
- `preview.css`：`body` 字級改為 `var(--preview-font-size, 15px)`；`@media print` 維持 11pt。
- `preview.js`：新增 `font` 訊息、`setFontSize`，以及 `Ctrl+滾輪` 累積調整與回報。
- `editor.js`／`EditorHostView`：新增 `Ctrl+滾輪` 與 `fontSize` 回報事件。
- `MainViewModel`：兩邊字級同步；字型事件以 500ms debounce 寫入設定；縮放方法改名為 `SetFontSizeAsync`／`AdjustFontSizeAsync`／`ResetFontSizeAsync`。
- `PreferencesDialog`：單一「字型大小」設定（10–28，預設 14）。
- 版號更新為 0.10.1。

### 問題與解法
- 需要避免 Ctrl+滾輪回報後 C# 又回呼造成無限循環 → WebView 收到 `font` 訊息只套用、不回報；C# 端僅負責同步另一邊與儲存。
- 觸控板會產生大量小幅度 wheel 事件 → 以 40 為門檻累積 `deltaY` 後才調整一級。
- 連續滾動會密集寫入 `settings.json` → 以 CancellationTokenSource 做 500ms debounce。
- `PreferencesDialog.FontSize` 會與 `Control.FontSize` 衝突 → 屬性改名為 `SelectedFontSize`。

### 待辦／已知限制
- 尚未實機驗證不同滑鼠／觸控板的滾輪靈敏度。
- PDF 固定 11pt，不會跟隨螢幕字級。

## 2026-11-04 - v0.10.0

### 目標
提升編輯器與多文件操作的日常體驗：讓常用功能可被發現、補齊既有設定欄位的 UI、改善分頁與最近檔案操作，並記住視窗與系統主題狀態。

### 變更
- `MainWindow.xaml`／`MainWindow.xaml.cs`：新增「編輯」與「檢視」選單；檔案選單新增「儲存全部」。
- `PreferencesDialog`：新增自動換行與編輯器字型大小（10–28 pt）；`MainViewModel.ApplyPreferencesAsync` 同步套用並保存。
- `IEditorHost`／`EditorHostView`／`editor.js`：新增 `font` 訊息與 `--editor-font-size` 設定；加入 `Ctrl+=`／`Ctrl+-`／`Ctrl+0` 縮放。
- `editor.js`：加入繁體中文 `EditorState.phrases`，將搜尋／取代面板中文化；新增復原、重做、全選、開啟搜尋的指令。
- 頁籤新增右鍵選單與中鍵關閉；`MainViewModel` 新增 `CloseOtherDocumentsAsync`。
- 最近開啟項目可釘選、取消釘選、移除，並可在檔案總管中顯示。
- 狀態列新增檔案路徑提示與「在檔案總管中顯示」按鈕。
- `AppSettings`：新增 `WindowLeft`／`WindowTop`；啟動時還原位置並檢查可見螢幕，關閉時保存。
- `MainWindow`：以 `OverlappedPresenter.PreferredMinimumWidth`／`PreferredMinimumHeight` 設定最小視窗 800×500。
- 「跟隨系統」主題改以 `UISettings` 判斷深色並訂閱 `ColorValuesChanged`，執行期即時更新。
- 版號更新為 0.10.0。

### 問題與解法
- WinUI 3 的 `Microsoft.UI.Xaml.Application` 沒有 `RequestedThemeChanged` 事件，編譯失敗 → 改用 `Windows.UI.ViewManagement.UISettings.ColorValuesChanged`，並透過 `IUiDispatcher` 回到 UI 執行緒。
- `ColorValuesChanged` 也可能因強調色變更而觸發 → 解析後比對目前 Web 主題，相同就不重新套用。
- 視窗位置可能來自已拔除的螢幕 → 以 `DisplayArea.GetFromRect(..., None)` 檢查，找不到就回到主要螢幕並置中。
- 最近開啟的右鍵選單掛在 `MenuFlyoutItem.ContextFlyout`，需實機確認 WinUI 3 的彈出選單是否正確轉送右鍵事件；若不行，改用子選單呈現釘選／移除。
- 偏好設定與選單的變更需同步 `WordWrapEnabled`、`EditorFontSize` 等通知，否則 `ToggleMenuFlyoutItem` 的勾選狀態不會更新。

### 待辦／已知限制
- 尚未實機逐項驗證自訂主題、高對比、跨螢幕視窗位置與分頁右鍵選單。
- 搜尋面板的翻譯依 `@codemirror/search` 目前使用的 phrase key；套件升級時需重新確認。
- 自動換行與字型大小目前只影響編輯器，預覽字型仍由預覽樣式決定。

## 2026-11-04 - v0.9.1

### 目標
調整工具列：檢視按鈕固定在最右側、滑鼠移入回饋更明顯，並以分隔線區分按鈕群組。

### 變更
- `MainWindow.xaml`：工具列改為三欄 `Grid`，左欄格式按鈕置於可橫向捲動的 `ScrollViewer`，右欄固定顯示檢視模式按鈕。
- `MainWindow.xaml`：格式按鈕依「行內格式／標題／清單與區塊／插入」分為四組，組間加入分隔線。
- `MainWindow.xaml`：於工具列 `Grid.Resources` 覆寫 `ButtonBackgroundPointerOver`／`ButtonBackgroundPressed`，以 `TextFillColorPrimary` 加上透明度做為 hover／pressed 底色，效果比預設明顯且會跟隨主題。
- 版號更新為 0.9.1。

### 問題與解法
- 淺色主題預設 hover 使用接近白色的 `ControlFillColorSecondary`，幾乎看不見；改用文字主色加透明度，淺色／深色／自訂主題都能維持明顯但一致的回饋。
- 以 `ThemeResource` 建立筆刷顏色，切換主題時會重新解析，不會像 `StaticResource` 別名那樣停留在舊主題。

### 待辦／已知限制
- 尚未實機檢視各主題與高對比模式下的實際對比度。

## 2026-11-04 - v0.9.0

### 目標
進一步降低大檔案輸入、預覽重繪、外部變更偵測與工作階段還原的成本，並改善預覽體驗與資源清理。

### 變更
- `EditorHostView`、`editor.js`、`MainViewModel`：Edit 模式只傳送輕量 `change` 通知；C# 在停止輸入 250ms、存檔、切換文件或暫存前才以 `getContent` 取得全文。
- `PreviewHostView`、`preview.js`：`render` 訊息帶 `documentId`，同文件重繪保留捲動比例；新渲染會提前結束舊請求（single-flight）。
- `preview.js`：程式碼高亮改用 `IntersectionObserver` 只處理可見區塊，並快取 highlight.js、KaTeX 與 Mermaid 結果；`preparePrint` 會在列印前補齊全部高亮。
- `MainViewModel`：外部變更先用 `LastWriteTimeUtc` 與 `LastKnownLength` 快篩，再計算 SHA-256。
- `SettingsService`：新增 `IsLoaded`，避免 App 與 ViewModel 重複載入設定。
- `App`：主視窗關閉與啟動失敗時處置 `IHost`，確保 logger 與 watcher 釋放。
- `FileLoggerProvider`：啟動時清除超過 14 天的日誌。
- `FileService`：移除 `FileOptions.WriteThrough`；`FileStamp`／`LoadedDocument` 增加檔案長度。
- 新增 `WebViewEnvironmentProvider`：兩個 WebView2 共用 `%LocalAppData%\MdEditor\WebView2` 的環境。
- `SessionDraftService`：manifest 增加 `PersistText`；已存檔且乾淨的文件不寫入全文 draft，還原時仍可從檔案重新載入。
- `MainViewModel`：工作階段還原以 `Task.WhenAll` 平行載入。
- `DocxExportService`：OpenXML 完整驗證改為僅 Debug 執行。
- 版號更新為 0.9.0。

### 問題與解法
- Edit 模式為避免全文往返而延後同步，若直接切到預覽會拿到舊內容 → 切換檢視、存檔、暫存與匯出前都先 `FlushEditorAsync`。
- 輕量變更與文字同步可能交錯，舊同步工作不能覆蓋新文件狀態 → `FlushEditorAsync` 只在文件仍是 `ActiveDocument` 時清除 stale 旗標。
- WebView2 WinRT 投影的環境建立方法是 `CreateWithOptionsAsync`，不是傳統的 `CreateAsync` 三參數多載。
- 只高亮可見區塊會讓 PDF 匯出漏掉畫面外程式碼 → 列印前送 `preparePrint`，完成全部高亮後才輸出。

### 待辦／已知限制
- 需實機驗證共用 WebView2 user data folder 後的啟動、快取與多視窗行為。
- 乾淨文件不保存全文 draft，若檔案在程式關閉後被刪除，將無法從 draft 還原該內容。
- 尚未在 Windows 10 1809 與 ARM64 實機測試。

## 2026-11-04 - v0.8.0

### 目標
改善啟動速度、記憶體使用、UI 執行緒阻塞與自動暫存 I/O，不改變使用者操作流程。

### 變更
- `MainViewModel`：預覽 WebView 改為第一次需要時才初始化；Edit 模式不再排程預覽渲染；PDF 匯出先初始化並等待渲染完成；字數統計改為 350ms debounce 與背景計算；自動暫存序列化移到執行緒集區。
- `Assets/Preview/preview.html`、`web/preview/src/preview.js`：移除預先載入的 `mermaid.min.js`，改為偵測到 Mermaid 區塊才動態載入，載入失敗時顯示錯誤區塊。
- `DocumentViewModel`、`DraftDocumentSnapshot`、`DraftManifestEntry`：加入 `Revision`，用於判斷暫存是否需要重寫。
- `SessionDraftService`：只寫入 revision 有變更或檔案不存在的文件；JSON 改用 `SerializeAsync` 與非同步資料流。
- `PreviewHostView`：文件資料夾相同時不再重設 `doc.local` 虛擬主機映射。
- `FileService`：SHA256 改用 `IncrementalHash`，避免存檔時複製整份內容。
- `FileWatcherService`：改為每個目錄共用一個 `FileSystemWatcher`，仍保留每個檔案 500ms debounce。
- `FileLoggerProvider`：改用 bounded `Channel` 與背景 writer，避免日誌 I/O 阻塞呼叫端。
- `MdEditor.csproj`：`RuntimeIdentifier` 依平台條件化；Release 啟用 `PublishReadyToRun`。
- 版號更新為 0.8.0。

### 問題與解法
- 預覽改為延遲初始化後，PDF 匯出若在 Edit 模式直接列印會沒有內容 → 匯出流程先切到 Preview、明確初始化並等待 `RenderAsync` 完成再列印。
- 自動暫存若只比對檔案時間無法可靠判斷內容是否變更 → 以文件 `Revision` 記錄，manifest 一併保存 revision，重啟後仍可避免重寫。
- 目錄共用 watcher 的 `Renamed` 事件路徑是舊路徑 → 以 `OldFullPath` 對應既有註冊，維持原本外部變更偵測行為。

### 待辦／已知限制
- 需實機確認 Edit 模式啟動時完全不建立預覽 WebView，以及無 Mermaid 文件不載入圖表引擎。
- Release ReadyToRun 尚未實際發佈驗證。
- 尚未在 Windows 10 1809 與 ARM64 實機測試。

## 2026-11-04 - v0.7.0

### 目標
在預覽與 PDF 支援數學公式與流程圖，維持離線運作與現有 CSP。

### 變更
- `web/package.json`：新增 `katex`、`mermaid`。
- `web/build.mjs`：複製 `mermaid.min.js`、`katex.min.css` 與 KaTeX 字型到 `src/MdEditor/Assets/Preview`。
- `Assets/Preview/preview.html`：載入 KaTeX CSS 與 Mermaid bundle。
- `web/preview/src/preview.js`：
  - KaTeX 排版 Markdig 產生的 `.math` 節點，並去除 Markdig 加入的 `\(`／`\[` 分隔符。
  - Mermaid 將 `<pre class="mermaid">` 轉成 SVG，支援主題切換重繪、錯誤提示與渲染競態保護。
  - 圖表與公式完成後才送出 `rendered`，確保 PDF 匯出完整。
- `Assets/Preview/preview.css`：新增公式、圖表與錯誤樣式及列印規則。
- `DocxExportService`：Word 匯出將數學公式保留為 LaTeX 原文（行內 `$...$`、區塊 `$$...$$`），不再輸出型別名稱或遺漏區塊。
- `Views/PreviewHostView.xaml.cs`：渲染等待由 15 秒放寬為 60 秒。
- `MarkdownRendererTests`：新增數學與 Mermaid 輸出測試；`DocxExportServiceTests`：新增數學原文匯出測試（共 40 個測試）。
- 更新 README、使用指南、技術文件、系統架構、CHANGELOG、RELEASE_NOTES；版號更新為 0.7.0。

### 問題與解法
- Markdig 的 Mathematics 擴充輸出 `<span class="math">\(…\)</span>`／`<div class="math">\[…\]</div>`，直接交給 KaTeX 會把分隔符當成公式內容 → 先去除 `\(`、`\)`、`\[`、`\]` 再排版。
- Mermaid 12 的 ESM 版本含 top-level await 與動態 import，無法直接併入 IIFE bundle → 改用官方預先打包的 `mermaid.min.js` UMD，以一般 script 載入。
- 快速輸入時前一次的非同步圖表渲染可能覆蓋新內容 → 以 generation id 檢查，僅在仍為目前渲染時寫入 DOM。
- Mermaid 可能留下暫存的 `#d{id}` 節點 → 渲染結束後主動移除。

### 待辦／已知限制
- Word 匯出仍以 LaTeX 原文與程式碼區塊呈現數學與圖表，尚未支援 OMML／圖片轉換。
- PDF 中的 Mermaid 圖表沿用預覽當下配色，列印樣式不會重新著色。
- 尚未在 Windows 10 1809 與 ARM64 實機驗證數學與圖表渲染。

## 2026-11-03 - v0.6.0

### 目標
補齊公開專案文件、採用 MIT License、支援 ARM64，並建立 GitHub 公開儲存庫與非 Self-Contained 的 x64／ARM64 Release。

### 變更
- 新增 `docs/使用指南.md`、`docs/系統架構.md`、`docs/技術文件.md`、`docs/系統設計.md`、`docs/建置與發佈.md`。
- 新增 `LICENSE`（MIT）、`CONTRIBUTING.md`、`SECURITY.md`、`.gitattributes`。
- 重寫 `README.md` 與 `RELEASE_NOTES.md`，更新 `CHANGELOG.md`。
- 新增 `scripts/publish-release.ps1`：一次產生 x64／ARM64 非 Self-Contained 發佈與 zip。
- `Directory.Build.props`：`Platforms` 改為 `x64;arm64`，`PlatformTarget` 依平台切換。
- `MdEditor.csproj`：新增 `RuntimeIdentifiers`（`win-x64;win-arm64`）。
- 版號更新為 0.6.0。

### 問題與解法
- PowerShell 5.1 以 ANSI 解讀無 BOM 的 UTF-8 腳本，導致中文亂碼／語法錯誤 → 腳本檔加上 UTF-8 BOM。
- ARM64 發佈需要同時覆寫 `RuntimeIdentifier`、`PlatformTarget` 與 Platform，否則會產出 x64 組件 → 以 `-r win-arm64 -p:Platform=arm64 -p:PlatformTarget=arm64` 發佈。
- 專案預設為 Self-Contained；公開 Release 需覆寫 `--self-contained false -p:WindowsAppSDKSelfContained=false`，並確認輸出無 `coreclr.dll`、`Microsoft.WindowsAppRuntime.dll`。
- 非 Self-Contained 發佈缺少 `MdEditor.pri`，啟動時會出現 `Cannot locate resource from 'ms-appx:///Styles/Themes/Lavender.xaml'` → 將 `EnableMsixTooling` 改為 `true`，讓 PRI 資源納入發佈。

### 待辦／已知限制
- 未在 Windows 10 1809 與 ARM64 實機完整驗證。
- 未進行程式碼簽章，SmartScreen 可能顯示警告。
- `dotnet build`：0 警告 0 錯誤；`dotnet test`：37/37 通過；`dotnet format --verify-no-changes`：通過。
- 已建立 x64／ARM64 非 Self-Contained 產物並解壓縮啟動驗證 x64。

## 2026-11-03 - v0.5.1

### 目標
記住視窗最大化狀態，下次啟動自動還原；避免最大化尺寸覆寫一般視窗大小。

### 變更
- `AppSettings`：新增 `WindowState`（`Normal`／`Maximized`），`SettingsService.Normalize` 將非法值回退為 `Normal`。
- `MainViewModel`：新增 `GetSavedWindowState`；`SaveWindowSettingsAsync` 改為寫入一般尺寸與視窗狀態。
- `MainWindow`：以 `_lastNormalSize` 記錄一般尺寸（啟動套用值與 `AppWindow.Changed` 還原事件）；啟動時依設定呼叫 `OverlappedPresenter.Maximize()`；關閉時儲存狀態，最大化尺寸不覆寫一般尺寸。
- `SettingsAndRecentFilesTests`：新增最大化狀態 round-trip 與非法值回退測試（共 37 個測試）。
- 版號更新為 0.5.1；README、CHANGELOG、RELEASE_NOTES 同步更新。

### 問題與解法
- 原本關閉時以 `AppWindow.Size` 儲存尺寸，最大化時會把螢幕尺寸存成一般尺寸 → 改存 `_lastNormalSize`。
- `AppWindow.Changed` 只在 `OverlappedPresenter.State == Restored` 時更新一般尺寸，避免最大化事件覆寫。

### 待辦／已知限制
- 不記住視窗位置與所在螢幕，啟動仍置中主要螢幕。
- 未在 Windows 10 1809 實測。
- `dotnet build`：0 警告 0 錯誤；`dotnet test`：37/37 通過；`dotnet format --verify-no-changes`：通過。
- UI 自動化實測：設定 `WindowState=Maximized` 啟動為最大化；執行中最大化後關閉會存成 `Maximized`，且寬高維持一般尺寸而非最大化尺寸。

## 2026-11-03 - v0.5.0

### 目標
新增 `.md`、`.markdown` 檔案關聯登錄，讓使用者可從檔案總管以本 App 開啟，並在 Windows「預設應用程式」中選擇。

### 變更
- `Core/Helpers/FileAssociationRegistration`、`Core/Models/RegistryEntry`、`RegistryValueReference`：以純邏輯產生登錄與移除計畫（ProgId、Capabilities、OpenWithProgids、Applications、開啟命令）。
- `Services/IFileAssociationService`、`FileAssociationService`：以 HKCU 套用計畫，提供 `IsRegistered`、`Register`、`Unregister`、`OpenDefaultAppsSettings`。
- `App.xaml.cs`：註冊 `IFileAssociationService` singleton。
- `PreferencesDialog`：新增檔案關聯開關、說明、登錄並開啟設定按鈕與錯誤 InfoBar。
- `MainWindow`：偏好設定儲存時比對狀態並登錄／解除；登錄後開啟 Windows 預設應用程式設定。
- `tests/FileAssociationTests`：涵蓋命令引號、副檔名、Capabilities 與移除計畫一致性（新增 6 個測試）。
- 版號更新為 0.5.0；README、CHANGELOG、RELEASE_NOTES 同步更新。

### 問題與解法
- ComImport coclass 直接轉型介面會產生 CS0030 → 改用 `Type.GetTypeFromCLSID` + `Activator.CreateInstance` 取得 `IApplicationAssociationRegistrationUI`。
- `Type.GetTypeFromCLSID` 回傳可為 null，`TreatWarningsAsErrors` 下觸發 CS8604 → 加上 null 檢查與例外。
- Windows 10/11 不允許程式靜默設定預設（UserChoice 保護）→ 僅登錄候選並開啟系統設定頁，不覆寫預設值。
- 用 PowerShell `Set-Content` 改 `Directory.Build.props` 造成中文亂碼 → 改用 Write 工具重寫，避免再以 shell 編輯含中文檔案。

### 待辦／已知限制
- 程式搬移位置後登錄會失效，尚未自動偵測或提示重新登錄。
- 尚未以實機完成「登錄 → 在設定頁設為預設 → 雙擊 .md 開啟」的完整流程驗證。
- 未手動在 Windows 10 1809 驗證檔案關聯流程。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤；`dotnet test`：35/35 通過；`dotnet format --verify-no-changes`：通過。
- UI 自動化煙霧測試：可從「設定 → 偏好設定...」開啟對話框，並找到檔案關聯開關與「登錄並開啟預設應用程式設定」按鈕。

## 2026-11-03 - v0.4.0

### 目標
新增自動暫存與工作階段還原、從 App 移除文件時刪除暫存、兩個淺色主題、較小且置中的預設視窗，並修正設定儲存後的 Ctrl+N tooltip。

### 變更
- `SessionDraftService`、`DraftSession`、`DraftDocumentSnapshot`、`DraftManifest`：以 `%LocalAppData%\MdEditor\drafts\` 儲存工作階段與各文件暫存，採原子寫入。
- `MainViewModel`：編輯內容 debounce 1.5 秒暫存、切換分頁／儲存／關閉前保存、啟動還原、關閉文件時刪除暫存；修正檢視模式持久化。
- `DocumentViewModel.Id` 改為可指定，讓暫存文件識別碼可跨啟動保留。
- `MainWindow`：關閉前呼叫 `PersistSessionAsync`；預設視窗尺寸改為 1120×720 並置中。
- 新增晨光（Morning）、薰衣草（Lavender）主題與對應資源字典、編輯器／預覽 CSS。
- 第一層選單標題改為「檔案 (F)」「設定 (S)」。
- `RootGrid` 與格式化按鈕設定 `KeyboardAcceleratorPlacementMode="Hidden"`，修正 WinUI 自動顯示 Ctrl+N tooltip。
- 測試新增工作階段暫存 round-trip、刪除、孤兒清理、損壞暫存略過與預設值，共 29 個測試。

### 問題與解法
- WinUI 會自動在具有 `KeyboardAccelerator` 的元素上顯示快捷鍵 tooltip → 設定 `KeyboardAcceleratorPlacementMode="Hidden"` 關閉。
- 關閉文件與關閉程式都需要不同暫存策略 → 關閉文件刪除該文件暫存；關閉程式保存全部暫存。
- 還原已儲存檔案時需處理外部變更 → clean 文件從磁碟重載，dirty 文件使用暫存並比對雜湊顯示外部變更提示。

### 驗證
- `npm --prefix web run build` 成功。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤。
- `dotnet test`：29/29 通過。
- `dotnet format --verify-no-changes`：通過。
- 以預先建立的暫存檔啟動，成功還原文件、視窗 1120×720 置中與晨光主題配色。
- 以 Ctrl+W 關閉文件後確認該文件暫存檔已刪除，其他暫存保留。
- 以自動輸入驗證編輯內容在 1.5 秒後寫入暫存檔。

### 待辦／已知限制
- 分隔線游標與 tooltip 顯示仍需在實際互動桌面做最後人工確認。
- 暫存只保留目前工作階段，尚未提供版本歷史。
- 尚未實測 Windows 10 1809。

## 2026-11-03 - v0.3.0

### 目標
新增多組主題（護眼、北歐藍、森林綠、高對比），將「跟隨系統」移到第一項並維持預設；改善選單快捷鍵顯示、預覽程式碼樣式與複製功能、分隔線游標、預覽 tooltip 與 App 圖示對比。

### 變更
- `Models/AppThemeCatalog`：集中管理主題 ID、顯示名稱、基底明暗、Web 主題與資源字典。
- `Styles/Themes/`：新增 `Sepia.xaml`、`Nord.xaml`、`Forest.xaml`、`HighContrast.xaml`，覆寫主要 WinUI 筆刷。
- `ThemeResourceManager`：切換主題時動態加入／移除應用程式層級資源字典。
- `MainViewModel`／`MainWindow`：`ThemeChanged` 改傳遞主題定義，同步更新 WinUI 外殼、編輯器與預覽。
- `PreferencesDialog`：主題順序改為跟隨系統、淺色、深色、護眼、北歐藍、森林綠、高對比；預設選取第一項。
- `MainWindow.xaml`：第一層選單標題改為「檔案 (Alt+F)」「設定 (Alt+S)」。
- `Assets/Editor/editor.css`、`web/editor/src/editor.js`：新增自訂主題變數與深色語法高亮切換。
- `Assets/Preview/preview.css`、`web/preview/src/preview.js`：新增主題變數、行內程式碼加深、程式碼區塊複製按鈕、移除預覽 `title` tooltip。
- `Controls/SplitterHandle`：以 `UserControl` 加上背景 `Grid`，自行處理 Pointer 拖曳，並在 `UserControl` 設定 `InputSystemCursorShape.SizeWestEast` 游標。
- 重新產生高對比 App 圖示。
- 測試新增新主題序列化與無效主題回退，共 22 個測試。

### 問題與解法
- WinUI 3 的 `Thumb` 是密封類別，無法直接繼承設定 `ProtectedCursor` → 改以 `UserControl` 搭配背景 `Grid` 自行處理 Pointer 拖曳，並在 `UserControl` 設定游標。
- `Windows.System.VirtualKey` 沒有 `OemComma` → 沿用數值 `(VirtualKey)188` 建立 `Ctrl+,`。
- 預覽中的 `Ctrl+N` 提示來自內容 `title` 屬性 → 渲染後統一移除所有 `title`，避免任何原生 tooltip。
- 多主題需要同時處理 WinUI 與 WebView2 → WinUI 以應用程式資源字典覆寫筆刷，編輯器／預覽以 CSS 變數與 `data-theme` 切換。

### 驗證
- `npm --prefix web run build` 成功。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤。
- `dotnet test`：22/22 通過。
- `dotnet format --verify-no-changes`：通過。
- 以 Sepia／Nord／Forest／HighContrast 設定啟動程式，皆可正常執行。
- 啟動煙霧測試正常。

### 待辦／已知限制
- 自訂主題的控制項互動狀態色仍需人工逐項確認，必要時補齊筆刷覆寫。
- 分隔線游標以 `ProtectedCursor` 實作，仍需在實際互動桌面確認顯示結果。
- 「跟隨系統」只在啟動時解析，執行期間系統主題變更不會自動跟隨。
- 預覽移除所有 `title` 屬性，因此不顯示連結／縮寫說明。
- 尚未實測 Windows 10 1809。

## 2026-11-03 - v0.2.0

### 目標
加入三層介面（系統選單／工具列／頁籤）、雙層系統選單與快捷鍵、偏好設定（主題、同步捲動）、檢視循環按鈕、關閉文件／結束程式，以及 App 圖示。

### 變更
- `MainWindow.xaml`：新增 `MenuBar`（檔案、設定）、雙層「最近開啟」與「匯出」子選單、`Ctrl+W`／`Ctrl+,` 等快捷鍵；頁籤移到工具列下方；工具列只保留 Markdown 格式化與單一檢視循環按鈕。
- `MainWindow.xaml.cs`：選單快顯、最近檔案子選單重建、偏好設定／關於對話框、結束程式流程、全域 `KeyboardAccelerator`、視窗圖示。
- `MainViewModel`：新增 `CycleViewModeCommand`、`CloseActiveDocumentCommand`、`ApplyPreferencesAsync`、檢視按鈕文字／提示，以及同步捲動開關判斷。
- 新增 `Views/PreferencesDialog`（主題、同步捲動）與 `Views/AboutDialog`（版本、圖示、使用元件）。
- `AppSettings`：新增 `SyncScrolling`，預設 `true`；舊設定檔缺少欄位時仍預設啟用。
- `web/editor/src/editor.js`：新增 `Ctrl+W` 與 `Ctrl+,` 轉發；重新執行 npm 建置。
- `Assets/AppIcon.ico`、`AppIcon.png`：新增多尺寸 App 圖示，並設定 `ApplicationIcon` 與 `AppWindow.SetIcon`。
- 測試新增同步捲動來回序列化與舊版設定檔預設值，共 17 個測試。

### 問題與解法
- `Windows.System.VirtualKey` 沒有 `OemComma` 成員 → 以數值 `(VirtualKey)188` 建立 `Ctrl+,` 加速鍵。
- 以 PowerShell 直接輸出 ICO 時函式回傳的 byte[] 被展開，產生損壞圖示 → 改用內嵌 C# 產生多尺寸 PNG 影格並手動寫入 ICO 容器。
- 選單中的 `MenuFlyoutSubItem` 沒有 `Opening` 事件 → 改監聽 `RecentFiles.CollectionChanged` 重建最近檔案子選單。

### 驗證
- `npm --prefix web install`、`npm --prefix web run build` 成功。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤。
- `dotnet test`：17/17 通過。
- `dotnet format --verify-no-changes`：通過。

### 待辦／已知限制
- 同步捲動仍為比例式，且只有編輯器 → 預覽方向。
- 快捷鍵在預覽 WebView2 取得焦點時不會觸發 WinUI 加速鍵。
- 尚未實測 Windows 10 1809。

## 2026-11-03 - v0.1.0

### 目標
建立 Windows Markdown 編輯器第一版：編輯／預覽／並排、工具列、多分頁、最近檔案、外部變更重新載入、PDF／Word 匯出、單一實例與主題。

### 變更
- 建立 `MdEditor.sln`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`。
- `src/MdEditor.Core`：Markdown 渲染、檔案服務（編碼／BOM／換行／原子寫入）、檔案監看、設定與最近檔案、OpenXML Word 匯出、啟動參數解析。
- `src/MdEditor`：WinUI 3 外殼、MainWindow、多分頁、工具列、狀態列、設定與最近檔案、單一實例、DI／Hosting、檔案記錄。
- `src/MdEditor/Views/EditorHostView`：WebView2 + CodeMirror 6 橋接（內容、選取、捲動、快捷鍵）。
- `src/MdEditor/Views/PreviewHostView`：WebView2 預覽、Markdig HTML、主題、PDF 列印、連結處理。
- `web/`：CodeMirror 6、highlight.js、esbuild 打包設定；產出 `Assets/Editor` 與 `Assets/Preview`。
- `tests/MdEditor.Tests`：Markdown、檔案、監看、啟動參數、設定／最近檔案、Word 驗證共 16 個測試。
- 文件：`README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/DEV_LOG.md`、`.gitignore`。

### 問題與解法
- WinUI 3 沒有內建 `GridSplitter` → 改用 `Thumb` 的 `DragDelta` 調整欄寬。
- CommunityToolkit.Mvvm 8.4 在 WinUI 3 要求 `[ObservableProperty]` 使用 partial property，否則觸發 MVVMTK0045 → 全面改為 `public partial T Property { get; set; }` 並在建構子設定預設值。
- Word 表格驗證失敗（缺少 `w:tblGrid`）→ 依欄數加入 `TableGrid` 與 `GridColumn`，再以 `OpenXmlValidator` 驗證。
- 第二次啟動曾把 `MdEditor.exe` 當成 Markdown 開啟 → 啟動參數改為只接受 `.md`／`.markdown`／`.mdown`／`.mkd`／`.txt`，並同步過濾最近檔案。
- UIAutomation 的 Control View 看不到 `InfoBar` → 改用 Raw View 驗證；`InfoBar.IsOpen`、高度與重新載入按鈕皆正常。
- WebView2 載入本機資產需要虛擬主機對應，且 CSP 需允許 `app.local` 與 `doc.local` → 以 `SetVirtualHostNameToFolderMapping` 對應 `Assets` 與目前文件資料夾。
- CodeMirror 6 無 UMD 建置 → 以 esbuild 打包為 IIFE，輸出至 `Assets/Editor/editor.bundle.js`。

### 驗證
- `dotnet build` 0 警告 0 錯誤。
- `dotnet test` 16/16 通過。
- `dotnet format --verify-no-changes` 通過。
- 以 CDP 檢查編輯器／預覽內容、主題（light/dark）與並排／預覽版面。
- 以 UI Automation 驗證工具列、儲存、多分頁、外部變更提示與「重新載入」流程。
- 驗證首次啟動（無檔案）會建立「未命名 1」分頁。
- Word 匯出以 `OpenXmlValidator` 驗證通過。
- 已確認 PDF 選單命令可執行並開啟系統檔案挑選器；自動化環境會在切換視窗時取消挑選器，因此未完成端到端 PDF 存檔驗證。

### 待辦／已知限制
- PDF 匯出使用預覽 WebView2 的 `PrintToPdfAsync`，固定 A4；檔案挑選器未能在此自動化環境完成存檔驗證。
- 工具列在 1280 寬視窗下會將部分格式化按鈕收進水平捲動區；檢視、匯出、主題已移到上方固定區。
- 尚未實作自動儲存、當機復原、工作階段還原。
- 尚未提供自訂尋找／取代 UI（目前用 CodeMirror 內建 `Ctrl+F`）。
- 同步捲動為比例式，未做到行號精確對齊。
- 預覽預設封鎖遠端圖片與原始 HTML。
- Word 匯出略過遠端圖片，只嵌入本機相對路徑圖片。
- 「跟隨系統」主題只在啟動時解析，執行期間系統主題變更不會自動跟隨。
- 尚未實測 Windows 10 1809。
