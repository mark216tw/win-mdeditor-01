# 貢獻指南

感謝你願意為 Markdown 編輯器（MdEditor）貢獻。本文件說明開發環境、建置流程與提交規範。

## 開發環境

| 工具 | 版本 |
| --- | --- |
| Windows | 10 1809（build 17763）以上或 Windows 11 |
| .NET SDK | 10.0.403（以 `global.json` 為準） |
| Node.js | 20 以上（僅修改 `web/` 時需要） |
| Git | 任意近期版本 |

另需安裝 Microsoft Edge WebView2 Evergreen Runtime。

## 取得原始碼與建置

```powershell
git clone https://github.com/mark216tw/win-mdeditor-01.git
cd win-mdeditor-01
npm --prefix web install
npm --prefix web run build
dotnet restore MdEditor.sln
dotnet build MdEditor.sln -c Debug
```

`src/MdEditor/Assets` 已包含預先建置的編輯器與預覽 bundle；只有修改 `web/` 下的原始碼時才需要重新執行 npm 指令。

## 測試與格式

提交前請確認以下指令通過：

```powershell
dotnet test tests\MdEditor.Tests\MdEditor.Tests.csproj -c Debug
dotnet format MdEditor.sln --verify-no-changes
```

- 新增或修改行為時，請同步新增／更新 `tests/MdEditor.Tests` 的單元測試。
- 不要降低警告等級，也不要使用 `#pragma warning disable` 掩蓋問題。

## 程式碼規範

- 使用 4 個空格縮排；類別／方法為 PascalCase、區域變數／參數為 camelCase、私有欄位為 `_camelCase`。
- 使用 file-scoped namespace 與 nullable reference types。
- UI 使用 WinUI 3 原生控制項；MVVM 使用 `CommunityToolkit.Mvvm`。
- 平台無關邏輯放在 `src/MdEditor.Core`；Windows／WinUI 專屬邏輯放在 `src/MdEditor`。
- 非同步方法以 `Async` 結尾；函式庫層使用 `ConfigureAwait(false)`，UI 層不加。
- 使用者可見文案以繁體中文為主；不要留 `TODO`、測試假資料或除錯輸出。
- 不要提交 `bin/`、`obj/`、`.vs/`、`artifacts/`、`node_modules/` 或憑證檔。

## 分支與提交

1. 從 `main` 建立功能分支，例如 `feature/file-preview-scroll`。
2. 一次提交聚焦一件事，訊息使用簡潔的祈使句，例如：
   - `新增檔案關聯登錄`
   - `修正最大化狀態未儲存`
   - `更新使用指南`
3. 開 Pull Request 時請說明：
   - 變更目的與範圍
   - 測試方式與結果
   - 已知限制或後續工作

## 回報問題

請使用 GitHub Issues，並盡量提供：

- 作業系統版本與架構（x64／ARM64）
- App 版本（「設定 → 關於」）
- 重現步驟與預期結果
- 記錄檔：`%LocalAppData%\MdEditor\logs\log-yyyyMMdd.txt`

安全性問題請勿開公開 Issue，請依 [安全性政策](SECURITY.md) 回報。
