# Issue #267：T131 信件

## 做了什麼

- 新增 `Mail.cs`：C87 三類列表／讀信／刪信、一般／血盟寄信、保管；完整解析 S186 列表、本文、失敗通知與操作確認。
- 新增 `MailUi.cs`：HUD 信件入口、收件匣、讀信、收件人／標題／多行本文編輯、刪除確認、保管與伺服器訊息。送出後保留並鎖定草稿，不自動重送，不以送出冒充成功。
- `WorldSession.cs` 僅增加 S186 一個 case；`PlayerHudView.cs` 僅增加一行掛載。連線方法放在新檔的 partial class，未修改 `LoginConnection.cs`。
- 將 119 項離線檢查提交於 `engine/_integration/dev/mail-tests/`，接入既有 Windows CI；透過 `--update-lock` 更新 dev lock，未手工拼接 hash。
- 本輪依調度留言再次合併 `origin/main`：先同步佈告欄主線 `bbaac76`，保留 S68／S148 與 S186 分派；驗證期間主線再合併 #215，因此續同步 `ccaa3e0`，保留受擊反應抑制與重設邏輯，以腳本重建 dev lock。保留信件、物品轉移、佈告欄與受擊抑制測試入口；信件模組程式不變，`docs/REPORT.md` 與主線一致。

## 怎麼驗證（已驗證）

- .NET SDK `10.0.300`，`rebuild.py --update-lock` → `rebuild.py --build --configfile scratch/issue-267/NuGet.Config --output scratch/issue-267/build` 通過：同步主線後 181 個來源、190 個鎖定輸入，product／audio／launcher 建置成功。本輪使用已安裝的 Blender 隨附 Python 執行腳本，未啟動 Blender；套件快取明確設於 worktree 的 `engine/.nuget`。
- 本機 NuGet 線上還原遇 TLS 認證錯誤；改用 scratch 中的 config，唯讀引用原專案 `engine/vendor`。套件快取與建置輸出均留在此 worktree，未改原專案。
- `powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/mail-tests/run.ps1 -NuGetConfig scratch/issue-267/NuGet.Config`：**PASS 119**，使用實際產品 DLL、合成封包與記憶體傳送閘門；未開遊戲／未連線／未查 DB。
- 針對本輪共用分派點合併，執行 `DemoChecks.csproj` 的 `board` 群組：**PASS 230**；結果在 `scratch/issue-267/board-checks.json`。證實保留的佈告欄分派可通過產品 DLL 離線測試。
- 同步 #215 後另執行 `hit-reaction-suppress` 群組：**PASS 66**，結果在 `scratch/issue-267/hit-reaction-suppress.json`；信件與佈告欄也以最終產品 DLL 重驗通過。
- 涵蓋每個 C／S 子型別、混合編碼、本文容量邊界、名稱奇偶與非對齊加密往返、畸形回包、ACK 才更新、斷線／世界重設、關閉編輯器撤銷排隊寄信及傳送錯誤不重試。
- UI 僅建立未顯示的原生控制項，核對 HUD 入口、本文欄位與收件匣繫結。
- `check-no-game-assets.py` 通過：同步主線後共 3292 個追蹤檔，0 錯誤、4 個核准 EmbeddedResource。`git -c core.whitespace=cr-at-eol diff --cached --check` 通過（既存兩個共用檔保留原 CRLF，避免無關行尾變更）。CI 自動執行 Windows 信件測試與 Linux 完整交叉建置，最終狀態以 [PR #283 checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/283/checks) 為準。
- 編譯僅有既存 `MagicUi.cs` 的兩個 nullable 警告，本模組無新增警告。

同步主線後產品 DLL SHA-256：`dc8e6572168936af54df135bce7560173faf4189cd6a39169f4f07d4d19ae9c4`。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未驗證原生畫面、真實收件、費用扣除、收件人不存在、滿箱與血盟群寄。來源格式與合成測試不代表端到端驗收。

已查來源沒有固定的本文字數上限；UI 依現有 frame 容量計算剩餘 UTF-16 單位，不將客戶端容量宣稱為伺服器規則。寄信成功沒有專用 ACK；空信箱可能不回包；列表包含收件人全部信件且沒有逐筆分類資料。UI 如實呈現，詳見協定文件。

## 產出路徑

- `engine/source/Mail.cs`、`engine/source/MailUi.cs`
- `engine/_integration/dev/mail-tests/`、`.github/workflows/build.yml`
- `docs/protocol/mail.md`：格式、文字規則、伺服器來源 SHA-256 及相容性限制
- `docs/reports/issue-267.md`：本報告
- `scratch/issue-267/build/`：未提交的本機完整建置 log 與 `result.json`

## 給下一棒的提示

Claude 審查 PR；依專案流程由使用者合併。dot 在授權 P24Lab 的後續 smoke，以兩個測試角色核對中英混合名稱寄信、中文／換行／補充平面本文、讀信、保管、刪除、一般／血盟郵資與滿箱失敗。特別核對沒有 8-byte 補零的 C87 寄信與實際 UTF-16LE 呈現。

如要求三個信匣各自只顯示所屬信件，須另派伺服器工作，因目前 S186 無法提供逐筆類別；本任務沒有改伺服器或推測分類。地圖／角色／連線切換會關閉信件視窗並清除快取，回到穩定世界後可重新開啟。
