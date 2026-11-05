# Markdown 編輯器（MdEditor）

[![GitHub Release](https://img.shields.io/github/v/release/mark216tw/win-mdeditor-01)](https://github.com/mark216tw/win-mdeditor-01/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Windows 10／11 的 Markdown 桌面編輯器。介面使用 WinUI 3，編輯器為 WebView2 內的 CodeMirror 6，預覽由 Markdig 產生 HTML，Word 匯出使用 OpenXML，PDF 使用 WebView2 列印。

- 支援架構：x64、ARM64
- 支援系統：Windows 10 1809（build 17763）以上、Windows 11
- 授權：MIT License

## 功能特色（v0.10.13）

- 三層介面：系統選單、工具列、文件頁籤。
- 系統選單：檔案（F）、編輯（E）、檢視（V）與設定（S），提供子選單與快捷鍵。
- 編輯、預覽、並排三種檢視，可拖曳調整分割比例。
- Markdown 工具列依「行內格式／標題／清單與區塊／插入／檢視切換」分組，固定檢視圖示可循環切換模式。
- 編輯器與預覽共用字型大小設定，可用 `Ctrl+=`／`Ctrl+-`／`Ctrl+0` 或 `Ctrl+滑鼠滾輪` 調整。
- 開啟、儲存、另存新檔、儲存全部；支援拖放檔案、命令列與檔案關聯開啟。
- 多文件分頁，切換分頁保留各自獨立的復原歷史、游標選取與捲動位置；頁籤支援 `Ctrl+Tab` 切換、右鍵選單與中鍵關閉。
- 支援直接 `Ctrl+V` 貼上剪貼簿圖片（自動存至文件 `images/` 資料夾），以及選取單行文字貼上網址自動轉為 `[文字](URL)`。
- 預覽中的工作清單（`- [ ]`）可直接點擊勾選或取消勾選，自動回寫原始碼。
- 自動暫存工作階段：關閉程式後再次開啟會還原開啟的文件、未儲存內容、使用中分頁與檢視模式。
- 啟動時立即套用視窗大小與位置並顯示載入狀態；編輯器與預覽同時載入，其他工作階段分頁在背景完成。
- 關閉視窗不會詢問是否儲存；未儲存內容仍保留於工作階段暫存。
- 最近開啟檔案清單（MRU），可從子選單釘選／移除目前檔案與清除清單。
- 外部檔案變更偵測：顯示提示並可重新載入；重新載入前若有未儲存變更會先確認。
- 匯出 PDF（A4）與 Word（.docx）。
- 預覽支援 KaTeX 數學公式（`$...$`、`$$...$$`）與 Mermaid 流程圖／時序圖／類別圖等圖表，完全離線渲染。
- 九種主題：跟隨系統、淺色、深色、護眼、晨光、薰衣草、北歐藍、森林綠、高對比；「跟隨系統」會在執行期跟隨系統變化。
- 編輯與預覽同步捲動（可停用）。
- 檔案關聯：可在偏好設定登錄／解除 `.md`、`.markdown`，並開啟 Windows「預設應用程式」設定。
- 記住視窗大小、位置與最大化狀態。
- 可從「視窗」選單將視窗寬度切換為目前螢幕工作區的 60%、80%，或將視窗最大化；比例模式會保留原有高度。
- 單一實例：再次啟動會把檔案帶到既有視窗，並將視窗還原到最前景。
- 保留檔案編碼（UTF-8／UTF-16／UTF-32／系統 ANSI）與 BOM，儲存時保留主要換行格式（LF／CRLF），採原子寫入。
- 狀態列：游標位置、字數、編碼、換行格式、儲存狀態，以及快速開啟檔案所在資料夾。

## 下載與安裝

請至 [Releases](https://github.com/mark216tw/win-mdeditor-01/releases) 下載對應版本的壓縮檔：

| 檔案 | 架構 | 說明 |
| --- | --- | --- |
| `MdEditor-<版本>-win-x64.zip` | x64 | 一般 Intel／AMD 電腦 |
| `MdEditor-<版本>-win-arm64.zip` | ARM64 | Windows on Arm 裝置 |

本專案只提供**非 Self-Contained**（framework-dependent）版本。執行前請先安裝：

1. [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（`Microsoft.NETCore.App` 10.0，需與 App 相同架構）。
2. [Windows App Runtime 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)（Windows App SDK，需與 App 相同架構）。
3. [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 11 通常已內建）。

下載 zip 後解壓縮到任意資料夾，執行 `MdEditor.exe` 即可；不需要系統管理員權限。

> 若要改為 Self-Contained 發佈，請參考 [建置與發佈](docs/建置與發佈.md)。

## 快速開始

1. 啟動 `MdEditor.exe`，按 `Ctrl+O` 開啟 Markdown 檔案，或直接將檔案拖進視窗。
2. 按 `Ctrl+1`／`Ctrl+2`／`Ctrl+3` 切換編輯、預覽、並排檢視。
3. 按 `Ctrl+S` 儲存、`Ctrl+Shift+S` 另存新檔。
4. 從「設定 → 偏好設定」調整主題、同步捲動與檔案關聯。
5. 關閉程式時不會詢問是否儲存，未儲存的內容會自動保留，下次開啟會自動還原。

完整說明請見 [使用指南](docs/使用指南.md)。

## 快捷鍵

| 快捷鍵 | 功能 |
| --- | --- |
| `Ctrl+N` | 新增文件 |
| `Ctrl+O` | 開啟檔案 |
| `Ctrl+S` | 儲存 |
| `Ctrl+Shift+S` | 另存新檔 |
| `Ctrl+W` | 關閉文件 |
| `Ctrl+Tab`／`Ctrl+Shift+Tab` | 下一個／上一個分頁 |
| `Ctrl+,` | 偏好設定 |
| `Ctrl+B` | 粗體（再按一次取消） |
| `Ctrl+I` | 斜體（再按一次取消） |
| `Ctrl+K` | 插入連結 |
| `Ctrl+1`／`Ctrl+2`／`Ctrl+3` | 編輯／預覽／並排 |
| `Ctrl+=`／`Ctrl+-`／`Ctrl+0` | 放大／縮小／重設字型（編輯器與預覽） |
| `Ctrl+滑鼠滾輪` | 放大／縮小字型（編輯器或預覽皆可） |
| `Ctrl+F`／`Ctrl+H` | 編輯器內尋找／取代 |
| `Alt+F`／`Alt+E`／`Alt+V`／`Alt+S`／`Alt+W` | 檔案／編輯／檢視／設定／視窗選單 |

## 資料位置

| 內容 | 路徑 |
| --- | --- |
| 使用者設定與最近檔案 | `%LocalAppData%\MdEditor\settings.json` |
| 工作階段暫存 | `%LocalAppData%\MdEditor\drafts\` |
| 記錄檔 | `%LocalAppData%\MdEditor\logs\log-yyyyMMdd.txt` |

## 專案結構

```text
src/MdEditor/             WinUI 3 應用程式（Views、ViewModels、Services、Assets）
src/MdEditor.Core/        平台無關核心（Markdown、檔案、監看、設定、工作階段、Word 匯出）
tests/MdEditor.Tests/     xUnit 單元測試
web/editor/               CodeMirror 6 編輯器原始碼與打包設定
web/preview/              預覽頁面（Markdig HTML + highlight.js + KaTeX + Mermaid）
docs/                     使用、架構、技術、設計與建置文件
scripts/                  發佈輔助腳本
```

## 開發與建置

需求：.NET 10 SDK（`global.json` 指向 10.0.403）、Node.js 20 以上。

```powershell
npm --prefix web install
npm --prefix web run build
dotnet restore MdEditor.sln
dotnet build MdEditor.sln -c Debug
dotnet test tests\MdEditor.Tests\MdEditor.Tests.csproj -c Debug
dotnet format MdEditor.sln --verify-no-changes
```

發佈（非 Self-Contained，x64 與 ARM64）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1 -Version 0.10.13
```

詳細流程請見 [建置與發佈](docs/建置與發佈.md)。

## 文件索引

- [使用指南](docs/使用指南.md)
- [系統架構](docs/系統架構.md)
- [技術文件](docs/技術文件.md)
- [系統設計文件](docs/系統設計.md)
- [建置與發佈](docs/建置與發佈.md)
- [開發紀錄](docs/DEV_LOG.md)
- [版本異動](CHANGELOG.md)
- [貢獻指南](CONTRIBUTING.md)
- [安全性政策](SECURITY.md)

## 已知限制

- 檔案關聯註冊在目前使用者（HKCU），Windows 不允許程式自行設定預設，需在「預設應用程式」設定頁選擇一次；程式搬移位置後需重新登錄。
- PDF 匯出固定 A4 與邊界，尚未提供頁面設定。
- 同步捲動為單向（編輯器捲動依區塊行號錨點對齊預覽），捲動預覽不會反向帶動編輯器。
- 預覽基於安全考量預設封鎖遠端圖片與原始 HTML。
- 預覽會移除內容中的 `title` 屬性，因此不顯示連結／縮寫的原生說明提示。
- 預覽中的相對連結僅能開啟文件所在資料夾內的檔案。
- Word 匯出只嵌入本機相對路徑圖片，遠端圖片會略過。
- Word 匯出不會排版數學公式或流程圖；數學以 LaTeX 原文、流程圖以程式碼區塊呈現。
- 預覽中的 Mermaid 圖表與 KaTeX 公式完全離線渲染；Mermaid 套件較大，會在第一份含圖表的文件出現時才載入，因此該次預覽會稍慢。
- `$` 同時是數學分隔符，貨幣寫法（例如 `$5`）可能被誤判；可用 `\$` 跳脫。
- 已存檔且無未儲存變更的文件不保留全文暫存；若檔案在程式關閉後被刪除，將無法從暫存還原內容。
- 尚未在 Windows 10 1809 與 ARM64 實機完整驗證。

## 授權

本專案採用 [MIT License](LICENSE)。
