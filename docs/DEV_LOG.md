# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

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
