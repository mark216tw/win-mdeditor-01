# Markdown 編輯器 v0.6.0

Windows 10／11 的 Markdown 桌面編輯器，支援 x64 與 ARM64。本版本為首個公開 Release，提供完整專案文件與 MIT License。

## 下載

| 檔案 | 架構 |
| --- | --- |
| `MdEditor-0.6.0-win-x64.zip` | x64（Intel／AMD） |
| `MdEditor-0.6.0-win-arm64.zip` | ARM64（Windows on Arm） |

本版本只提供**非 Self-Contained**（framework-dependent）套件，執行前請先安裝與 App 相同架構的：

1. .NET 10 Runtime（`Microsoft.NETCore.App` 10.0）
2. Windows App Runtime 1.8（Windows App SDK）
3. Microsoft Edge WebView2 Evergreen Runtime

解壓縮後直接執行 `MdEditor.exe`，不需要系統管理員權限。

## 你可以做什麼

- 編輯 Markdown 並即時預覽，支援編輯、預覽、並排三種檢視。
- 多文件分頁、自動暫存與工作階段還原：關閉後再開啟會還原文件、未儲存內容、使用中分頁與檢視模式。
- 九種主題：跟隨系統、淺色、深色、護眼、晨光、薰衣草、北歐藍、森林綠、高對比。
- 匯出 PDF（A4）與 Word（`.docx`），Word 匯出不需要安裝 Microsoft Word。
- 外部檔案變更偵測與重新載入。
- 最近開啟清單（MRU），可釘選與清除。
- 檔案關聯：在偏好設定登錄 `.md`、`.markdown`，再於 Windows「預設應用程式」設定中選擇本 App。
- 記住視窗最大化狀態，下次開啟自動還原。
- 單一實例：再次開啟檔案會帶到既有視窗。
- 保留檔案編碼（UTF-8／UTF-16／UTF-32／系統 ANSI）、BOM 與換行格式。

## 快捷鍵

- `Ctrl+N` 新增、`Ctrl+O` 開啟、`Ctrl+S` 儲存、`Ctrl+Shift+S` 另存、`Ctrl+W` 關閉文件。
- `Ctrl+,` 偏好設定。
- `Ctrl+B` 粗體、`Ctrl+I` 斜體。
- `Ctrl+1` 編輯、`Ctrl+2` 預覽、`Ctrl+3` 並排。
- `Ctrl+F` 編輯器內搜尋。
- `Alt+F` 檔案選單、`Alt+S` 設定選單。

## 系統需求

- Windows 10 1809（build 17763）以上或 Windows 11。
- 非 Self-Contained 執行環境：.NET 10 Runtime、Windows App Runtime 1.8、WebView2 Evergreen Runtime。

## 已知限制

- 檔案關聯註冊在目前使用者（HKCU）；Windows 不允許程式自行設定預設，需在「預設應用程式」設定頁選擇一次。程式搬移位置後需重新登錄。
- 視窗只記住最大化狀態，不記住位置與所在螢幕。
- PDF 頁面設定目前固定為 A4。
- 遠端圖片在預覽中預設不載入；預覽停用原始 HTML。
- 預覽不顯示連結與縮寫的原生 `title` 提示。
- Word 匯出只嵌入本機相對路徑圖片。
- 「跟隨系統」主題只在啟動時判斷。
- 同步捲動為比例式，不是依 Markdown 行號精準對齊。
- 尚未在 Windows 10 1809 與 ARM64 實機完整驗證。

## 授權

本專案採用 [MIT License](https://github.com/mark216tw/win-mdeditor-01/blob/main/LICENSE)。
