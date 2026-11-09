# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-09 - v1.1.2

### 目標
雙擊分頁列空白處可新增文件；Undo 回到儲存前內容時，分頁的未儲存標記應消失；啟動與儲存時妥善處理外部變更衝突。

### 變更
- `src/MdEditor/MainWindow.xaml.cs`：分頁 `ListView` 改以 `DoubleTappedEvent` + `AddHandler`（含已處理事件）攔截雙擊，確認來源不在分頁項目或捲軸時執行 `NewDocumentCommand`。
- `src/MdEditor/Models/DocumentViewModel.cs`：新增 `SavedText` 基準與 `MarkSaved()`／`SetSavedText()`／`RefreshDirtyState()`，以內容字串比較維護 `IsDirty`；新增 `PendingConflict` 與 `ApplyDiskVersion()`。
- `src/MdEditor/ViewModels/MainViewModel.cs`：載入、儲存、編輯器內容變更與草稿還原改用新的基準機制；還原時建立 `DocumentConflict`，作用中分頁優先顯示解析對話框，其餘分頁切換時提示；儲存前重新比對磁碟雜湊並提供覆蓋／另存／取消。
- `src/MdEditor/Views/ConflictResolutionDialog.xaml`、`ConflictCompareDialog.xaml`：新增衝突解析與並排比較對話框。
- `src/MdEditor.Core/Services/LineDiffService.cs`：新增行級 LCS 差異比對，過大輸入自動降級。
- `src/MdEditor/Services/DialogService.cs`：新增 `SaveConflictDecision` 與 `ConfirmSaveConflictAsync()`。
- `src/MdEditor/App.xaml`：註冊 `ILineDiffService`，並為淺色／深色／系統主題加入差異標示底色資源。

### 問題與解法
- 分頁 `ListView` 會處理指標事件，一般事件處理常式收不到空白處點擊 → 使用 `AddHandler(..., handledEventsToo: true)` 並沿視覺樹排除 `ListViewItem` 與 `ScrollBar`；需求改為雙擊後改用 `DoubleTappedEvent`。
- 檔案雜湊包含編碼、BOM 與行尾，不能直接與記憶體中的文字比較 → 改以正規化後的 `SavedText` 字串作為基準。
- ContentDialog 僅有三種標準按鈕，無法同時表達保留／載入／比較 → 使用自訂按鈕並在 `Choice` 屬性回傳結果。
- 淺色／深色／系統主題沒有自訂資源字典，差異底色會找不到 → 在 `App.xaml` 的 `ThemeDictionaries` 補上預設資源。

### 待辦／已知限制
- 比較畫面為唯讀，不提供逐行合併；超過行數門檻的大檔會降級為無差異標示的並排檢視。
- 儲存前檢查與實際寫檔之間仍有極小時間差，無法完全避免其他程式瞬間修改。
- 尚未在 Windows 10 1809 與 ARM64 實機驗證衝突對話框、雙擊與 Undo 行為。

## 2026-11-06 - v1.1.1

### 目標
將工具列全部功能改為風格一致的自繪圖示，減少中英文文字按鈕造成的寬度差異並提升辨識一致性。

### 變更
- `src/MdEditor/Styles/ToolbarIcons.xaml`：新增 19 個 20×20 單色向量線稿，統一線寬、圓角、尺寸與主題前景色。
- `src/MdEditor/App.xaml`：載入工具列圖示資源字典。
- `src/MdEditor/MainWindow.xaml`：所有工具列文字與系統圖示改用自繪向量圖示，按鈕統一為 32×32；保留原有分組、快捷鍵、提示與無障礙名稱。
- `Directory.Build.props`、`README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/使用指南.md`、`docs/技術文件.md`：版號更新為 1.1.1 並同步文件。

### 問題與解法
- WinUI XAML 編譯器無法將簡寫路徑直接指定給資源中的 `PathGeometry.Figures` → 改以繼承共用線條設定的 `Path` 樣式，在 `Data` setter 內轉換路徑資料。

### 待辦／已知限制
- 尚未在 Windows 10 1809 與 ARM64 實機確認圖示顯示。

## 2026-11-05 - v1.1.0

### 目標
新增 HTML 匯出（單一檔案、跟隨 App 主題、KaTeX 字型使用 CDN），並在工具列檢視按鈕左側加入復原／重做圖示按鈕；取消底線工具規劃。

### 變更
- `src/MdEditor.Core/Services/HtmlExportService.cs`：新增 `IHtmlExportService`／`HtmlExportService`，包裝預覽 HTML、內嵌 `preview.css`、程式碼高亮 CSS、CSP、本機圖片 data URI，並把 KaTeX 字型 URL 改寫為 jsDelivr CDN。
- `web/build.mjs`：建置時輸出 `Assets/Preview/katex-version.txt`，讓匯出的 CDN 版本與目前 KaTeX 一致。
- `web/preview/src/preview.js`：新增 `exportHtml` 訊息，將目前預覽 DOM 轉為靜態 HTML（移除複製按鈕、停用工作清單互動、移除 `data-line`）。
- `src/MdEditor/Services/IPreviewHost.cs`、`Views/PreviewHostView.xaml.cs`：新增 `GetRenderedHtmlAsync()`，透過 WebView2 取得渲染後內容。
- `src/MdEditor/ViewModels/MainViewModel.cs`：新增 `ExportHtmlAsync` 命令、注入 `IHtmlExportService`，並在 HTML 匯出期間避免切換檢視與處理 WebView 快捷鍵。
- `src/MdEditor/MainWindow.xaml`：匯出選單新增「匯出 HTML...」；工具列在檢視按鈕左側新增復原／重做圖示按鈕。
- `src/MdEditor/App.xaml.cs`：註冊 `IHtmlExportService`。
- `tests/MdEditor.Tests/HtmlExportServiceTests.cs`：新增主題、KaTeX CDN URL、本機／遠端圖片與 CSP 測試。
- `Directory.Build.props`、`CHANGELOG.md`、`README.md`、`RELEASE_NOTES.md`、`docs/使用指南.md`、`docs/技術文件.md`：版號更新為 1.1.0 並同步文件。

### 問題與解法
- 需要在匯出檔重現預覽中的 KaTeX、Mermaid 與程式碼高亮，而不額外打包 JavaScript → 直接從預覽 WebView2 擷取已渲染的靜態 DOM。
- 匯出檔可能包含 `https://doc.local/...` 圖片（僅在 App 內的虛擬主機有效）→ 匯出時以 `PathSafety` 驗證並轉為 base64 data URI；遠端圖片保留網址。
- KaTeX 字型佔用較大且版本需與 CSS 一致 → 保留本機 CSS，字型 URL 依 `katex-version.txt` 改寫為 jsDelivr CDN。
- 復原／重做已在「編輯」選單有全域快捷鍵，工具列按鈕不重複註冊 `KeyboardAccelerator`，避免同一個快捷鍵觸發兩次。

### 待辦／已知限制
- HTML 匯出依賴 WebView2；WebView2 初始化失敗時無法匯出。
- 遠端圖片保留網址，不是完全離線檔案；KaTeX 字型使用 CDN，離線時公式可能改用替代字型。
- 尚未在 Windows 10 1809 與 ARM64 實機驗證 HTML 匯出顯示結果。

## 2026-11-05 - v1.0.0

### 目標
完成 GitHub Releases v1.0.0 上架前全面巡查、穩定性與安全性強化、開源授權合規補齊及文件同步校對。

### 變更
- `src/MdEditor.Core/Helpers/PathSafety.cs`、`MarkdownRenderer.cs`、`DocxExportService.cs`、`MainViewModel.cs`：統一驗證相對圖片與連結路徑，阻擋 `..` 上層目錄穿越與絕對路徑存取。
- `FileService.cs`、`web/editor/src/editor.js`、`EditorHostView.xaml.cs`、`MainViewModel.cs`：新增單檔 50 MB 上限（超過 10 MB 先詢問確認）與剪貼簿貼上圖片 20 MB 上限，並重新建置前端 bundle。
- `DialogService.cs`、`MainWindow.xaml.cs`：加入單一 `ContentDialog` 互斥鎖與主題同步，並於對話框開啟期間攔截背景全域快捷鍵；修正視窗關閉流程（5 秒逾時與強制放行關閉），`EditorHostView.SetReadOnlyAsync` 不再等待失敗的初始化工作。
- `MainViewModel.cs`：另存新檔指向另一個已開啟分頁時提示確認並清理重複分頁；並排檢視下匯出 PDF 不再強制切換版面。
- `FileAssociationRegistration.cs`、`PreferencesDialog.xaml`、`AboutDialog.xaml(.cs)`、`MainWindow.xaml`、`LICENSE`：補齊 `.mdown`／`.mkd` 檔案關聯、關於對話框顯示組件與檔案版本／授權隱私說明、修正快捷鍵與圖示字型資源。
- `Directory.Build.props`、`THIRD-PARTY-NOTICES.txt`、`scripts/publish-release.ps1`、`README.md`、`RELEASE_NOTES.md`、`CHANGELOG.md`、`CONTRIBUTING.md`、`docs/*.md`、`AGENTS.md`、`tests/MdEditor.Tests/*`：升級版號為 1.0.0、補齊第三方授權聲明、強化發佈腳本（自動讀版號、去除 `.pdb`、隨附授權檔、UTF-8 BOM）並新增對應單元測試。

### 問題與解法
- WinUI 3 同一個 `XamlRoot` 同時呼叫兩次 `ContentDialog.ShowAsync()` 會拋出 `COMException` → 於 `DialogService` 集中透過 `SemaphoreSlim` 串行化並提供 `IsDialogOpen` 防重入。
- WebView2 初始化失敗或逾時時，`OnAppWindowClosing` 呼叫 `BeginClosingAsync`／`CancelClosingAsync` 會重複拋出例外導致視窗永遠無法關閉 → `SetReadOnlyAsync` 改為僅在 `IsReady` 時傳訊，關閉保存加上 5 秒逾時後必定放行 `Close()`。
- Windows PowerShell 5.1 讀取無 BOM 的 UTF-8 `.ps1` 腳本會誤用系統 ANSI 編碼導致中文字串亂碼 → `scripts/publish-release.ps1` 確保以 UTF-8 with BOM 儲存並通過語法樹解析驗證。

### 待辦／已知限制
- 尚未在 Windows 10 1809 與 ARM64 實機完整驗證；公開發行檔尚未進行商業憑證程式碼簽章。

## 2026-11-05 - v0.13.2

### 目標
修正頁籤切換時 App 崩潰、頁籤無法操作的問題。

### 變更
- `MainWindow.xaml`：移除 `DataTemplate` 內的 `VisualStateManager`／`StateTrigger`，改以兩個背景 `Border`、兩個標題 `TextBlock` 與底部指示條的 `Visibility` 綁定呈現作用中狀態。
- `DocumentViewModel`：以 `ActiveVisibility`／`InactiveVisibility` 取代 `IsInactive`。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版號更新為 0.13.2。

### 問題與解法
- log 顯示 `XamlBindingSetters.Set_Microsoft_UI_Xaml_StateTrigger_IsActive` 發生 `NullReferenceException`。`x:Bind` 綁定 `DataTemplate` 內的 `StateTrigger.IsActive` 時，產生的 setter 取用到 null 的 trigger，切換頁籤即崩潰；改以穩定的 `Visibility` 綁定避開此問題。

### 待辦／已知限制
- 無。

## 2026-11-05 - v0.13.1

### 目標
讓目前瀏覽的文件頁籤與其他頁籤有更明顯的視覺區別。

### 變更
- `DocumentViewModel`：新增 `IsActive`／`IsInactive`。
- `MainWindow.xaml.cs`：`SyncTabSelection` 與頁籤切換時同步 `IsActive`，讓視覺立即回饋。
- `MainWindow.xaml`：頁籤改用 `VisualStateManager` 切換作用中樣式（卡片背景、底部強調線、主要文字色、半粗體）；非作用中頁籤淡化；停用 `ListViewItem` 預設左側選取指示條。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版號更新為 0.13.1。

### 問題與解法
- 原本頁籤外觀由 `DataTemplate` 的 `Border` 自行繪製，會蓋住 `ListViewItem` 的選取視覺；改以 `IsActive` 驅動自訂視覺狀態。

### 待辦／已知限制
- 無。

## 2026-11-05 - v0.13.0

### 目標
為預覽視窗加入實用的右鍵選單。

### 變更
- `PreviewHostView`：啟用 WebView2 右鍵選單事件，清空預設項目後建立「複製、全選、複製連結網址、開啟連結、複製圖片網址」。
- `web/preview/src/preview.js`：在 `contextmenu` 事件記錄原始 `href`／`src` 與選取文字，供 C# 建立選單與複製使用。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版號更新為 0.13.0。

### 問題與解法
- 直接使用 WebView2 預設選單會出現「重新載入」等瀏覽器項目，可能讓預覽內容消失；改為清空後自訂項目。
- 相對連結與圖片若使用 `ContextMenuTarget` 會取得解析後的 `app.local`／`doc.local` 網址，因此改由前端記錄原始 `href`／`src`。

### 待辦／已知限制
- 目前未提供「另存圖片」與「複製圖片」二進位內容。

## 2026-11-05 - v0.12.2

### 目標
修正 App 啟動後 WebView2 無法載入、畫面持續轉圈的問題，並加入防再發處理。

### 變更
- `EditorHostView`、`PreviewHostView`：`WebViewEnvironmentProvider.GetAsync()` 與 `EnsureCoreWebView2Async()` 加上 20 秒逾時；逾時顯示「使用者資料夾可能被其他程序鎖定」。
- `MainWindow`：關閉視窗時呼叫 `EditorHost.Close()` 與 `PreviewHost.Close()` 釋放 WebView2。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`AGENTS.md`：版號更新為 0.12.2，並記錄重建時應終止整個程序樹。

### 問題與解法
- 原因是先前的 `MdEditor.exe` 被強制終止後，`msedgewebview2.exe` 子程序仍持有 `%LocalAppData%\MdEditor\WebView2\EBWebView\lockfile`，導致新實例卡在 `EnsureCoreWebView2Async`。已終止殘留程序並確認 lockfile 解除。
- 日後重建改用 `taskkill /F /T /IM MdEditor.exe` 終止整個程序樹，避免留下 WebView2 orphan。

### 待辦／已知限制
- 逾時時間固定 20 秒；若首次啟動較慢可能需要調整。

## 2026-11-05 - v0.12.1

### 目標
依使用者回饋將編輯／預覽區與狀態列間距歸零，並移除列印功能。

### 變更
- `MainWindow.xaml`：編輯／預覽區底部間距改為 0；移除「列印...」選單項目與 `Ctrl+P` 快捷鍵。
- `MainWindow.xaml.cs`：移除列印點擊與快捷鍵處理常式。
- `MainViewModel`、`IPreviewHost`、`PreviewHostView`：移除列印命令與 `PrintAsync`；`IsPreviewVisible` 一併移除。
- `web/editor/src/editor.js`、`web/preview/src/preview.js`：移除 `Ctrl+P` 轉送 `print` 的快捷鍵對應，並重新建置 bundle。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版號更新為 0.12.1。

### 問題與解法
- 保留 `PrepareForPrintAsync`、`preparePrint` 與 `printReady`，因為「匯出 PDF」仍透過 `PrintToPdfAsync` 使用列印準備流程。

### 待辦／已知限制
- 無。

## 2026-11-05 - v0.12.0

### 目標
依使用者回饋調整列印可用性與對話框呈現、分割線操作、字型選單與整體間距。

### 變更
- `MainWindow.xaml`：縮小編輯／預覽區底部間距（8 → 4）；移除「檔案」選單的「關閉文件」；列印項目依 `IsPreviewVisible` 顯示。
- `SplitterHandle`、`MainWindow`：新增雙擊偵測與 `ResetRequested`，雙擊分割線時將比例重設為 0.5。
- `PreviewHostView`：`ShowPrintUI` 改用 `CoreWebView2PrintDialogKind.System`，列印對話框以獨立視窗呈現。
- `MainViewModel`：新增 `IsPreviewVisible`；`PrintAsync` 僅在預覽／並排模式執行，移除暫時切換檢視模式的邏輯。
- `PreferencesDialog`、`EditorFontCatalog`：字型下拉改為不可編輯；新增中文字型（新細明體、標楷體、微軟正黑體）並置頂。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版號更新為 0.12.0。

### 問題與解法
- 字型下拉改為不可編輯後，舊版自訂字型可能不在清單中；載入時比對不到會動態補上該項目並選取，避免顯示空白。
- 雙擊分割線需與拖曳區分；以 `PointerPoint.Timestamp` 與位移門檻判定，避免 `PointerPressed` 設為 handled 後 `DoubleTapped` 不觸發。

### 待辦／已知限制
- 系統列印對話框不提供 Edge 的即時列印預覽。

## 2026-11-05 - v0.11.0

### 目標
依使用者選擇實作高效益且改動小的體驗優化：記住分割比例、列印、編輯器字型、重開關閉分頁、選單整理與效能微調。

### 變更
- `AppSettings`、`SettingsService`：新增 `EditorSplitRatio`（0.25–0.75）並正規化；空白字型回退 `Cascadia Mono`。
- `MainWindow`：以星號比例套用並記住編輯／預覽分割；關閉時連同視窗尺寸儲存；最近檔案選單加入簽章快取。
- `IEditorHost`、`EditorHostView`、`web/editor/src/editor.js`、`PreferencesDialog`、`MainViewModel`：接上既有 `EditorFontFamily` 設定，新增可編輯字型下拉與即時套用。
- `MainViewModel`、`MainWindow`：新增最近關閉文件快照堆疊（上限 10）與 `Ctrl+Shift+T` 重開；乾淨檔案直接重讀，未儲存內容以新分頁還原。
- `IPreviewHost`、`PreviewHostView`、`MainViewModel`、`MainWindow`、`editor.js`、`preview.js`：新增 `Ctrl+P` 列印，純編輯模式會先強制渲染預覽再呼叫 `ShowPrintUI`。
- `MainWindow.xaml`：移除「設定 (S)」選單，將「偏好設定...」移到「檔案」選單；新增「列印...」與「重新開啟關閉的文件」。
- `MainViewModel`：同步捲動訊息去重並加上例外保護；切換文件時重設快取。
- `SettingsAndRecentFilesTests`：新增分割比例與字型正規化測試。
- 版號更新為 0.11.0。

### 問題與解法
- `RenderPreviewNowAsync` 原本在純編輯模式直接返回，列印無法取得最新預覽；改為可傳入 `force` 並回傳是否成功渲染。
- 關閉分頁會刪除 draft，重開時不重用原文件識別碼，避免 WebView2 內已釋放的分頁狀態衝突。

### 待辦／已知限制
- 重開分頁堆疊僅存在本次執行期間，跨程式重啟仍由 draft 還原。
- 大文件預覽／編輯內容的背景序列化尚未實作，待實測有明顯 UI 卡頓再處理。

## 2026-11-05 - v0.10.14

### 目標
依使用者回饋調整系統選單配置，將「關於」移至視窗選單最下方。

### 變更
- `MainWindow.xaml`：從「設定 (S)」選單移除「關於」，改置於「視窗 (W)」選單最下方並以 `MenuFlyoutSeparator` 隔開。
- `MainWindow.xaml`：移除「舒適寬度 (80%)」與「最大化」之間的分隔線。
- `Directory.Build.props`、`CHANGELOG.md`：版號更新為 0.10.14。

### 問題與解法
- 無。

### 待辦／已知限制
- 無。

## 2026-11-05 - v0.10.13

### 目標
實作第 1～6 項高報酬編輯與同步體驗優化：分頁保留復原歷史與游標／捲動狀態、改善同步捲動流暢度與行號對齊、消除預覽破圖阻塞與純編輯模式冗餘 IPC、支援雙向行內格式／智慧貼上網址／剪貼簿貼圖／預覽勾選待辦清單，並補齊常用快捷鍵。

### 變更
- `web/editor/src/editor.js`、`IEditorHost`、`EditorHostView`、`MainViewModel`：以 `Map` 快取各分頁 `EditorState` 與 `scrollTop`，切換分頁保留 `Ctrl+Z` 歷史、游標與捲動位置；關閉分頁時送 `disposeDocument` 釋放快取。
- `web/editor/src/editor.js`：捲動事件改為 `requestAnimationFrame` 節流並計算頂端行號 `line`；`wrapInline` 支援雙向移除標記（含 `*`／`**`／`***` 邊界判斷）；新增選取單行文字貼上網址轉連結、剪貼簿圖片貼上傳送，以及 `Ctrl+K`、`Ctrl+H`、`Ctrl+Tab`／`Ctrl+Shift+Tab` 快捷鍵與 `toggleTask` 指令。
- `MarkdownPipelineFactory`、`MarkdownRenderer`：預覽專用 Markdig pipeline 透過 `DocumentProcessed` 為區塊與清單項目注入 1-based `data-line` 屬性（跳過會重排到底部的腳註定義，且不覆蓋標題自動產生的 `id`）。
- `web/preview/src/preview.js`、`preview.css`、`IPreviewHost`、`PreviewHostView`：移除常態渲染中阻塞 Mermaid 與高亮的 `waitForImages()`（移至匯出 PDF 的 `preparePrint`）；依 `[data-line]` 元素做線性插值同步捲動並在末端平滑收斂至頁底；解除工作清單 checkbox `disabled` 並回傳 `toggleTask` 同步原始碼。
- `MainViewModel`：移除純編輯模式下每 250ms 觸發全文拉取的 `ScheduleTextSync`，統一由存檔、切換與 1.5 秒自動暫存時同步全文；串接貼圖儲存與待辦清單狀態回寫。
- `FileService`：新增 `SavePastedImageAsync`，將剪貼簿圖片以不重複檔名原子寫入文件同層的 `images/` 資料夾。
- `MainWindow.xaml(.cs)`：新增 `Ctrl+Tab`／`Ctrl+Shift+Tab` 分頁切換加速器、「取代... (`Ctrl+H`)」選單項目與工具列連結按鈕 `Ctrl+K` 加速器。
- `MarkdownRendererTests`、`FileServiceTests`：新增 `data-line` 屬性與貼上圖片寫檔單元測試。
- 版號更新為 0.10.13，同步更新 README、使用指南、技術文件、CHANGELOG、RELEASE_NOTES。

### 問題與解法
- CodeMirror 6 的 `Compartment` 隨 `EditorState` 保存，若在分頁 B 改過主題或換行設定，切回分頁 A 還原快取 `EditorState` 時會殘留舊設定 → 還原 `EditorState` 後立即派送 compartment `reconfigure` 對齊目前全域狀態。
- 若用 Markdig 內建 `UsePragmaLines()` 會改寫區塊 `id="pragma-line-..."`，覆蓋標題自動錨點；且腳註定義區塊會被搬移到 HTML 底部破壞行號單調順序 → 改以自訂 `DocumentProcessed` 寫入 `data-line` 並排除 `FootnoteGroup`／`Footnote`。
- 對 `**粗體**` 內選取文字按 `Ctrl+I` 時，外圍字元雖為 `*` 但屬於粗體標記 → 加入前後 2～3 字元連續星號判斷，確保粗體與斜體可正常疊加成 `***粗斜體***` 及各自獨立解除。

### 驗證
- `npm --prefix web run build`：成功更新 `Assets/Editor/editor.bundle.js` 與 `Assets/Preview/preview.bundle.js`。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤。
- `dotnet test tests\MdEditor.Tests\MdEditor.Tests.csproj -c Debug`：48/48 通過。
- `dotnet format MdEditor.sln --verify-no-changes`：通過。

### 待辦／已知限制
- 同步捲動仍為單向（編輯器帶動預覽），捲動預覽不會反向帶動編輯器。
- 未儲存的新增文件因尚無目標資料夾，貼上剪貼簿圖片時會提示先儲存文件。
- 尚未在 Windows 10 1809 與 ARM64 實機驗證。

## 2026-11-05 - v0.10.12

### 目標
執行先前評估的 1～7 項小改動高效能優化，降低存檔等待、輸入時的重複 UI 更新、自動暫存 I/O 與安裝包體積，並補齊挑檔、無障礙與預覽連結安全。

### 變更
- `MainViewModel`：存檔後不再等待 `RenderPreviewNowAsync`；以文件、文字與資料夾比對略過重複預覽渲染，並在渲染成功後記錄狀態。
- `MainViewModel`：`RenderPreviewNowAsync` 先擷取文字與資料夾快照，避免背景執行緒讀取可變狀態。
- `DocumentViewModel`：移除未被綁定的 `CharCount`／`LineCount`／`WordCount` 變更通知。
- `MainViewModel`：`OnDocumentPropertyChanged` 只對會影響狀態列的屬性呼叫 `UpdateStatus`，並移除 `OnEditorContentChanged` 中重複的呼叫。
- `MainViewModel`：儲存時若最近檔案已存在且可用，不再重寫 `settings.json`。
- `SessionDraftService`：孤兒暫存清理改為啟動載入與文件數量減少時執行，避免每次自動暫存掃描目錄。
- `web/build.mjs`：KaTeX 只輸出 WOFF2，並移除 CSS 中的 woff／ttf fallback。
- `DialogService`：開啟檔案挑選器改用 `SupportedFileTypes.Extensions`，補齊 `.mdown`／`.mkd`。
- `MainWindow.xaml`：工具列格式按鈕補上 `AutomationProperties.Name`。
- `MainViewModel`：預覽相對連結解析後限制在文件資料夾內。
- 版號更新為 0.10.12，同步更新 README、CHANGELOG、RELEASE_NOTES。

### 問題與解法
- 存檔若同步等待預覽，會讓 `Ctrl+S` 的感覺取決於 Markdown 與 WebView 渲染時間；改為背景渲染並以文字快照比對，避免每次存檔都重建預覽 DOM。
- 一次文字變更原本會因 `Text`、`CharCount`、`LineCount`、`WordCount`、`IsDirty` 等通知重複觸發狀態更新；移除未使用通知並縮小屬性判斷範圍後，常態輸入只保留必要更新。
- 自動暫存每次寫入都掃描 drafts 目錄；改為啟動載入與文件被移除時清理，仍保留 `SavingWithoutDocument_DeletesOrphanDraft` 行為。
- WebView2 支援 WOFF2，因此移除 woff／ttf 不影響公式顯示，安裝包字型由約 1.03 MB 降至 0.25 MB。
- 相對連結以 `Path.GetFullPath` 解析後再比對文件資料夾前綴，避免 `..` 跳出資料夾；根目錄以 `Path.EndsInDirectorySeparator` 處理。

### 驗證
- `npm --prefix web install`：成功。
- `npm --prefix web run build`：成功；`Assets/Preview/fonts` 只剩 20 個 WOFF2，CSS 無 woff／ttf 參照。
- `dotnet build MdEditor.sln -c Debug`：0 警告 0 錯誤。
- `dotnet test tests\MdEditor.Tests\MdEditor.Tests.csproj -c Debug`：46/46 通過。
- `dotnet format MdEditor.sln --verify-no-changes`：通過。

### 待辦／已知限制
- 尚未以實機量測大檔存檔、連續輸入與大量分頁的體感差異。
- 尚未實機驗證螢幕閱讀器朗讀工具列名稱。
- 相對連結跨資料夾的行為已刻意限制在文件資料夾內，若需開啟外部檔案需另提供明確操作。
- 仍須在 Windows 10 1809 與 ARM64 實機驗證。

## 2026-11-04 - v0.10.11

### 目標
精簡編輯器與預覽的載入畫面，只顯示轉圈圖示，不顯示說明文字。

### 變更
- `EditorHostView.xaml`：移除載入畫面中的「正在準備編輯器…」文字與外層 `StackPanel`，只保留置中的 `ProgressRing`。
- `PreviewHostView.xaml`：移除載入畫面中的「正在準備預覽…」文字與外層 `StackPanel`，只保留置中的 `ProgressRing`。
- 保留 `ProgressRing` 的 `AutomationProperties.Name`，維持螢幕閱讀器辨識。
- 更新版號與文件至 0.10.11。

### 問題與解法
- 載入文字會讓畫面顯得較擁擠；改為單一圖示，載入狀態仍可從轉圈動畫辨識。

### 待辦／已知限制
- 錯誤面板仍保留文字說明，僅載入狀態改為純圖示。

## 2026-11-04 - v0.10.10

### 目標
讓編輯器與預覽在啟動時同時開始載入，避免預覽比編輯器晚出現。

### 變更
- `MainViewModel.InitializeAsync`：載入設定後依 `ViewMode` 同步啟動 `PreviewHost.InitializeAsync()`；編輯模式不預熱預覽。
- `MainViewModel.LoadActiveDocumentAsync`：移除 `RenderPreviewAfterEditorReadyAsync` 與 150ms 延遲，載入文件時立即並行啟動 Preview render。
- 保留既有 Editor 預熱、載入畫面與過期 render 防護。
- 更新 `Directory.Build.props`、README、CHANGELOG、RELEASE_NOTES 與版號至 0.10.10。

### 問題與解法
- 先前為了降低啟動競爭，Preview 刻意延後 150ms 且等到 Editor 設定內容後才初始化，造成預覽較晚出現；改為載入設定後即初始化 Preview WebView2，並與 Editor 內容載入同時進行 render。
- 以 `ViewMode == Edit` 作為預覽預熱條件，避免只看編輯模式時浪費 WebView2 資源。

### 待辦／已知限制
- 同時初始化兩個 WebView2 會增加啟動瞬間的 CPU 與記憶體用量，Editor 首次可輸入時間可能略增。
- 尚未以 WPR/WPA 量測同時載入前後的差異。

## 2026-11-04 - v0.10.9

### 目標
改善啟動感知速度：視窗立即以記住的大小與位置出現、初始化期間不顯示空白 WebView，並讓 Editor 更早可用。

### 變更
- `MainWindow`：新增 `ApplyInitialWindowLayout`，在視窗顯示後、ViewModel 初始化前立即套用尺寸、位置與最大化狀態。
- `App`：啟動時預熱 WebView2 環境；視窗顯示後立即啟動 Editor 初始化，不再等待設定與工作階段載入。
- `MainViewModel`：Editor 初始化與工作階段 draft 載入並行；作用中文件優先顯示，Preview 延後至 Editor 內容就緒後才渲染。
- `EditorHostView`／`PreviewHostView`：WebView 上方新增主題化載入畫面，ready 後才隱藏，初始化失敗時改顯示錯誤面板。
- `FileService`：檔案解碼、換行正規化與 SHA256 改在背景執行，避免大檔案阻塞 UI 執行緒。
- `FileLoggerProvider`：舊日誌清理改為背景執行。
- `web/editor`：移除 `@codemirror/language-data` 全量語言，只保留 JavaScript／TypeScript／JSX／TSX、HTML、CSS、JSON、Python、Java、C++、C#、Shell、SQL、XML 與 YAML 常用語言；`editor.bundle.js` 由約 1.62 MB 降至約 0.91 MB。
- 更新 `Directory.Build.props`、README、CHANGELOG、RELEASE_NOTES 與版號至 0.10.9。

### 問題與解法
- 原本視窗先以預設大小顯示，等 Editor 初始化完成後才 `Resize`，造成明顯跳動；改為 `Activate` 後立即套用儲存尺寸並以旗標避免重複套用。
- Editor 初始化原本必須等設定與工作階段載入完成才開始，WebView2 啟動成本完全串行；改為提前預熱並與 draft 載入重疊。
- Preview 在 Editor 剛顯示時立即初始化會搶 UI 與 WebView2 資源；改為延後 150ms，且 Editor 模式不初始化 Preview。
- 大檔案還原時 SHA256 與解碼可能在 UI 執行緒完成；改於 `Task.Run` 內處理，維持 UI 可回應。
- 只保留常用語言可顯著縮小 bundle，但冷門語言（例如 Rust、Go、PHP）的程式碼區塊不再語法高亮。

### 待辦／已知限制
- 尚未以 WPR/WPA 量測冷啟動與暖啟動的前後差異。
- 尚未在 Windows 10 1809、大量分頁與大檔案情境實機驗證載入畫面與背景還原。
- 若需要冷門語言高亮，可再改為動態載入對應語言 chunk。

## 2026-11-04 - v0.10.8

### 目標
縮短視窗顯示後到 Editor 可輸入的等待時間，並修正切到預覽後 `Ctrl+1/2/3` 無法切換檢視的問題。

### 變更
- `MainViewModel`：Editor 載入目前文件後立即解除輸入保護，Preview 改為背景渲染。
- `MainViewModel`：工作階段先還原目前文件，其餘分頁以最多兩個並行工作背景載入；關閉時會等待背景還原完成。
- `MainViewModel`：背景還原期間暫停工作階段寫入，完成後再保存完整 manifest，並避免與手動開檔產生重複文件。
- `MainViewModel`：最近檔案存在性檢查移出啟動關鍵路徑，失敗時只記錄警告。
- `IPreviewHost`／`PreviewHostView`／`preview.js`：新增快捷鍵訊息轉送，預覽模式支援檢視、檔案操作、偏好設定與縮放快捷鍵。
- `EditorHostView`／`editor.js`：`setContent` 新增完成回覆，確認 CodeMirror 已套用內容後才允許輸入。
- `EditorHostView`／`editor.js`：關閉保存期間將 CodeMirror 切為唯讀，關閉失敗時恢復編輯與焦點。
- 重新建置 `src/MdEditor/Assets` WebView 資產，並更新文件與版號至 0.10.8。

### 問題與解法
- `LoadActiveDocumentAsync` 原本在 `_isLoadingDocument` 為 true 時等待完整 Preview render，期間輸入事件會被忽略；改為只等待 Editor 設定內容，Preview 非同步完成。
- Preview WebView2 取得焦點後，WinUI 根層的 `KeyboardAccelerator` 收不到按鍵；改由 `preview.js` 攔截應用程式快捷鍵並透過 WebMessage 交給共用處理器。
- 一次還原所有分頁會延後目前文件可編輯時間；改為目前文件優先，其餘背景限量還原並在加入前排除重複文件。
- 背景還原期間若先保存工作階段，可能把尚未加入的 draft 當成孤立檔案刪除；加入還原閘門，完成前不覆寫 manifest。
- 關閉等待背景還原時仍可能收到最後輸入；關閉流程先切換唯讀，最後再擷取 Editor 內容並保存工作階段。

### 待辦／已知限制
- 啟動改善尚未以 WPR/WPA 量測冷啟動與暖啟動時間。
- 尚未在大量檔案、不可用網路路徑及 Windows 10 1809 實機驗證背景還原。

## 2026-11-04 - v0.10.7

### 目標
重新整理視窗與檢視操作的位置，讓上方選單負責視窗尺寸，工具列只保留固定圖示的檢視切換。

### 變更
- `MainWindow.xaml`：在設定右側新增「視窗 (W)」選單，提供精簡寬度、舒適寬度與最大化。
- `MainWindow.xaml`：移除工具列的視窗按鈕；檢視循環按鈕移至格式按鈕列末端，以分隔線自成群組。
- `MainWindow.xaml`：檢視按鈕改用固定 `View` 圖示，點擊仍依序切換編輯、預覽與並排模式。
- README、使用指南、CHANGELOG、RELEASE_NOTES、建置文件與版號更新為 0.10.7。

### 問題與解法
- 視窗尺寸屬於視窗層級操作，放在格式工具列會混淆用途，因此改為獨立的上方選單。
- 檢視按鈕不再以動態文字占用寬度，改用固定圖示並保留動態 ToolTip 說明目前模式。

### 待辦／已知限制
- 尚未在 Windows 10 1809 與觸控模式實機驗證選單及工具列配置。

## 2026-11-04 - v0.10.6

### 目標
精確還原貼齊螢幕工作區的視窗高度，並讓精簡與舒適模式只調整視窗寬度。

### 變更
- `MainWindow.xaml.cs`：啟動還原尺寸上限改為完整工作區，不再預留 80 像素邊界。
- `MainWindow.xaml.cs`：60% 與 80% 模式只依工作區比例調整寬度，保留原有高度及垂直位置。
- `MainWindow.xaml`：選單與提示文字改為「精簡寬度」與「舒適寬度」。
- README、使用指南、CHANGELOG、RELEASE_NOTES、建置文件與版號更新為 0.10.6。

### 問題與解法
- 視窗高度雖有正確保存，但啟動時上限使用 `workArea.Height - 80`，導致重啟後底部固定留下距離；改用完整工作區高度。
- 從最大化切回比例寬度時，使用最大化前記錄的一般視窗高度，避免高度一併變成工作區高度。

### 待辦／已知限制
- 尚未在不同 DPI、工作列位置及 Windows 10 1809 實機驗證。

## 2026-11-04 - v0.10.5

### 目標
提供工具列快速切換視窗大小，讓使用者能依目前螢幕工作區快速選擇精簡、舒適或最大化版面。

### 變更
- `MainWindow.xaml`：工具列右側新增「視窗」按鈕與精簡（60%）、舒適（80%）、最大化選單。
- `MainWindow.xaml.cs`：依視窗所在螢幕的工作區計算尺寸，切換後置中並沿用 800×500 最小限制。
- README、使用指南、CHANGELOG、RELEASE_NOTES 與版號更新為 0.10.5。

### 問題與解法
- 固定像素尺寸在不同解析度與 DPI 下體驗不一致，因此改用目前螢幕工作區比例計算。
- 最大化狀態無法直接調整一般視窗尺寸，因此切換至精簡或舒適時先還原，再調整尺寸與位置。

### 待辦／已知限制
- 尚未在不同 DPI、工作列位置及 Windows 10 1809 實機驗證。

## 2026-11-04 - v0.10.4

### 目標
從 Windows 檔案總管開啟 Markdown 檔案時，讓既有的 MdEditor 視窗還原並切換到最前景。

### 變更
- `App`：次要執行個體在轉送啟用參數前，將前景視窗權限交給主要執行個體。
- `WindowActivationHelper`：新增最小化視窗還原、WinUI 視窗啟用與 Win32 前景切換。
- 文件與版號更新為 0.10.4。

### 問題與解法
- `RedirectActivationToAsync` 只會轉送啟用資料，既有執行個體不一定有權限切換前景，因此由檔案總管啟動的次要執行個體先呼叫 `AllowSetForegroundWindow`。
- 原先採用 `LibraryImport` 會要求專案啟用 unsafe 編譯；此功能改用 `DllImport`，避免擴大專案編譯權限。

### 待辦／已知限制
- 前景切換仍受 Windows 的使用者焦點政策控制；API 失敗時會記錄警告，但不會阻止檔案開啟。
- 尚未在 Windows 10 1809 與 ARM64 實機驗證。

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
