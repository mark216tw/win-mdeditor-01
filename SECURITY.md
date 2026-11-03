# 安全性政策

## 支援版本

安全性修正會套用在最新發行版本。請盡可能使用最新版本。

## 回報安全性問題

請**不要**透過公開 GitHub Issue 回報安全性問題。

請改用以下方式私下聯絡：

- GitHub Security Advisories：<https://github.com/mark216tw/win-mdeditor-01/security/advisories/new>
- 電子郵件：<mark216tw@gmail.com>

回報時請提供：

- 問題描述與影響範圍
- 重現步驟或概念驗證
- 受影響版本與作業系統架構
- 你希望公開揭露的時間（若適用）

我們會在合理時間內確認並回覆，並在修正推出後於版本紀錄中說明。

## 安全設計摘要

- 預覽以 Markdig 產生 HTML，停用原始 HTML 並封鎖 `javascript:`／`vbscript:` 等危險 URL。
- 遠端圖片預設不在預覽中載入。
- WebView2 僅允許導覽至虛擬主機 `https://app.local/`；文件資料夾以 `https://doc.local/` 唯讀對應。
- PDF 使用 WebView2 列印；Word 匯出使用 OpenXML，不需安裝 Microsoft Word。
- 檔案關聯只寫入目前使用者（HKCU），不需系統管理員權限，並可完整移除。
- 記錄檔不含 API Key、密碼或其他機密資訊。
