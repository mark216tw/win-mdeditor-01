# Markdown 編輯器 v1.1.0

Windows 10／11 的輕量原生 Markdown 桌面編輯器（支援 x64 與 ARM64），結合 WinUI 3 外殼、CodeMirror 6 編輯器與離線即時預覽。

## v1.1.0 重點

- **HTML 匯出**：新增「檔案 → 匯出 → 匯出 HTML...」，輸出單一 HTML 檔並跟隨匯出當下的 App 主題；保留 KaTeX、Mermaid 與程式碼高亮，本機圖片內嵌，遠端圖片保留網址。
- **KaTeX 字型使用 CDN**：匯出檔的 KaTeX 字型改由 jsDelivr 載入，減少檔案大小；離線開啟時公式字型可能退化。
- **復原／重做圖示**：工具列檢視按鈕左側新增復原與重做圖示，對應 `Ctrl+Z`／`Ctrl+Y`。

## v1.0.0 正式版重點

- **多文件分頁與自動工作階段還原**：切換分頁完整保留各自獨立的 `Ctrl+Z`／`Ctrl+Y` 復原歷史、游標選取與捲動位置；關閉視窗自動保留開啟分頁與未儲存草稿，下次啟動即刻還原，亦可用 `Ctrl+Shift+T` 還原剛關閉的分頁。
- **即時預覽、公式與圖表離線渲染**：支援 GitHub Flavored Markdown（表格、腳註、工作清單點擊回寫）、程式碼語法高亮、KaTeX 數學公式（`$...$`、`$$...$$`）與 Mermaid 流程圖／時序圖／類別圖等，全程本機離線運作。
- **行號錨點同步捲動與彈性版面**：提供編輯、預覽、並排三種模式，可拖曳調整分割比例（雙擊分割線置中），捲動編輯器時依區塊行號即時對齊預覽位置。
- **剪貼簿智慧貼上**：支援 `Ctrl+V` 直接貼上截圖／圖片（自動存至文件同層 `images/` 資料夾並插入語法），以及選取文字後貼上網址自動包成 `[文字](URL)`。
- **匯出與編碼保留**：支援匯出 PDF（A4）與 Word（`.docx`，無須安裝 Microsoft Word）；讀寫保留原始編碼（UTF-8／UTF-16／UTF-32／系統 ANSI）、BOM 與換行格式（LF／CRLF），並採原子寫入與外部變更偵測。
- **九款主題與字型縮放**：內建跟隨系統、淺色、深色、護眼、晨光、薰衣草、北歐藍、森林綠、高對比主題；支援中英文字型選擇與 `Ctrl+=`／`Ctrl+-`／`Ctrl+0`／`Ctrl+滑鼠滾輪` 同步縮放。
- **穩定性與安全強化**：防範對話框重入與視窗關閉卡死、限制超大檔案與跨目錄圖片路徑存取、補齊 `.md`／`.markdown`／`.mdown`／`.mkd` 檔案關聯登錄，並隨附完整開源授權聲明。

## 下載與安裝

| 檔案 | 架構 |
| --- | --- |
| `MdEditor-1.1.0-win-x64.zip` | x64（一般 Intel／AMD 電腦） |
| `MdEditor-1.1.0-win-arm64.zip` | ARM64（Windows on Arm 裝置） |

本發行版為**非 Self-Contained**（framework-dependent）套件，執行前請先安裝與 App 相同架構的執行環境：

1. [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（`Microsoft.NETCore.App` 10.0）
2. [Windows App Runtime 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)（Windows App SDK）
3. [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 11 通常已內建）

下載壓縮檔後解壓縮至任意資料夾，直接執行 `MdEditor.exe` 即可，不需要系統管理員權限。

> 註：執行檔目前未進行商業憑證程式碼簽章，首次執行若出現 Windows SmartScreen 提示，請確認壓縮檔下載自本專案 GitHub Releases 後選擇繼續執行。

## 常用快捷鍵

- `Ctrl+N` 新增、`Ctrl+O` 開啟、`Ctrl+S` 儲存、`Ctrl+Shift+S` 另存新檔、`Ctrl+W` 關閉文件、`Ctrl+Shift+T` 重新開啟關閉的文件。
- `Ctrl+Tab`／`Ctrl+Shift+Tab` 切換下一個／上一個分頁。
- `Ctrl+1` 編輯、`Ctrl+2` 預覽、`Ctrl+3` 並排、`Ctrl+,` 偏好設定。
- `Ctrl+B` 粗體、`Ctrl+I` 斜體、`Ctrl+K` 插入連結、`Ctrl+F` 尋找、`Ctrl+H` 取代。
- `Ctrl+=`／`Ctrl+-`／`Ctrl+0` 或 `Ctrl+滑鼠滾輪` 放大、縮小、重設字型大小。
- `Alt+F` 檔案選單、`Alt+E` 編輯選單、`Alt+V` 檢視選單、`Alt+W` 視窗選單。

## 系統需求

- Windows 10 1809（build 17763）以上或 Windows 11。
- .NET 10 Runtime、Windows App Runtime 1.8、Microsoft Edge WebView2 Evergreen Runtime。

## 已知限制

- 檔案關聯註冊於目前使用者（HKCU）；依 Windows 安全限制需在「預設應用程式」設定頁手動選擇一次，程式搬移資料夾後請重新登錄。
- 單一檔案載入上限為 50 MB（超過 10 MB 開啟前會先詢問確認）；剪貼簿貼上圖片上限為 20 MB。
- 預覽與 Word 匯出基於安全考量預設封鎖遠端圖片、原始 HTML 與跨越文件資料夾上層的本機路徑。
- PDF 頁面設定目前固定為 A4；Word 匯出尚不排版數學公式與 Mermaid 圖表（分別以 LaTeX 原文與程式碼區塊呈現）。
- HTML 匯出依賴 WebView2；遠端圖片保留原網址，KaTeX 字型由 jsDelivr CDN 載入，離線開啟時公式字型可能退化。
- 含 Mermaid 圖表的文件首次預覽會稍慢（離線圖表引擎於首次遇到圖表時載入）；貨幣符號（如 `$5`）若與數學公式衝突請以 `\$` 跳脫。
- 同步捲動為單向（編輯器捲動帶動預覽對齊）。
- 已存檔且無未儲存變更的文件不重複保存全文暫存；若原檔在程式關閉期間被外部刪除，將無法從暫存還原內容。

## 授權與隱私

本專案採用 [MIT License](https://github.com/mark216tw/win-mdeditor-01/blob/main/LICENSE) 開源授權，第三方元件授權聲明請見壓縮包內或儲存庫根目錄的 `THIRD-PARTY-NOTICES.txt`。編輯、預覽與 PDF／Word 匯出皆在本機離線完成，不會上傳任何文件內容或遙測資料；HTML 匯出檔若含遠端圖片或開啟 KaTeX CDN 字型，瀏覽器會連線至對應來源。
