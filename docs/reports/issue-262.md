# Issue #262／T128：血盟（部分實作，待範圍裁定）

分支：`task/262`。工作目錄：`scratch/wt/task-262`。T128 尚未完成。2026-11-07 第二次執行核對 PR #282 為 open、draft=false，已修正前次報告的草稿狀態描述。

## 第二次執行結果

**阻擋，待主控裁定掛接範圍。** 已重新讀取 Issue #262 本文與六則留言，仍只授權 WorldSession.cs 的 S72／S118／S200 三個 case。前次審查要求先裁定 Trade.cs、S167／S170／S250 的必要範圍，並明示未授權越界。本次已提出範圍確認，尚未收到答覆，因此僅更新本報告與 `docs/protocol/clan.md`。

### 本次做了什麼

- 唯讀核對實際 d287 JAR，確認入盟邀請為 `S219 H(0) D(0) H(97) S(申請者)`。C121 的 97 分支依目前 `CLAN_JOIN` 邀請回覆，D 欄位由伺服器讀取後捨棄。後續實作應使用這條既有建構子路徑。
- 核實 C68 的 S167、S170、S250/171 三種回包均存在，並記錄 S250/171 每筆姓名字串後的額外 C(0)。
- 核實 C72 仍比對 emblem ID。JAR class 符號掃描只在 `L1Clan.class` 找到 `setEmblemId`／`_emblemId`，初始化與其他血盟的映射來源仍待釐清，不能假定 clan ID 等於 emblem ID。
- 詳細 wire 格式、JAR SHA-256、方法與最小掛接清單已附加在 `docs/protocol/clan.md`。

### 本次怎麼驗證

- `rebuild.py` 唯讀核驗通過，176 個來源、185 個鎖定輸入。
- `rebuild.py --verify` 通過，既有 product／audio／launcher 與 receipt 相符。product DLL SHA-256 仍為下列前次建置值。
- 以 `dotnet run --no-build --no-restore` 重跑已建置的既有離線測試，clan 56 項與 map-continuity 184 項通過。輸出為 `scratch/clan-checks-attempt2.json`、`scratch/clan-map-checks-attempt2.json`。這些計數涵蓋前次已實作範圍。
- `engine/tools/check-no-game-assets.py` 通過，trackedFiles=3259、errors=0。
- 本次只修改文件，未執行 `--update-lock` 或重建產品。最新文件提交的 CI 結果另列最後 RESULT。

### 尚待完成與給下一棒的提示

三項 review 缺口全部仍待完成。請調度器將本任務交主控／Claude 裁定掛接範圍及盟徽初始化責任，取得裁定後由本機執行器實作、補正式入口離線測試、更新 dev lock、重建並重送審查。維持原範圍重派同一 code 任務仍會遇到同一阻擋。

本次 `gh issue/pr view` 在沙箱內回傳 HTTP 401，先由 GitHub 連接器核對狀態，再經核准的沙箱外 `gh pr view` 成功讀取，確認前次 head `bce8d2c` 的三項 CI 皆 SUCCESS。Git 沙箱內連線出現 Schannel 憑證錯誤，經核准的沙箱外 `git ls-remote` 已成功核對同一遠端 head。提交、推送與 GitHub 留言結果另列最後 RESULT。

以下保留前次已完成的產品與測試範圍，方便接手者對照。

## 做了什麼

- 新增 `Clan.cs`：C222／C194／C61／C69／C72／C68 編碼、既有 sender gate 傳送、S72／S118／S200 解析、血盟名稱與記憶體盟徽快取。拒絕重複傳送、失效世界／連線、失去面對面目標的入盟申請；不預先假造伺服器成功結果。
- 新增 `ClanUi.cs`：正式 HUD「服務 → 血盟」管理入口，建立、申請加入、退出／解散、驅逐與送出成員查詢；退出／驅逐有操作確認。名稱旁繪製伺服器送來且識別碼可對應的 BMP 盟徽，沒有讀取外部圖檔。
- `WorldSession.cs` 只增加 S72、S118、S200 各一個 case；`ObjectRendering.cs` 只增加一個繪製呼叫。未修改 Trade.cs、LoginConnection.cs 或其他產品模組。
- 提交模組測試並接到既有 `DemoChecks`／Windows CI `map-continuity` 入口；以工具更新 dev lock。

## 已驗證／怎麼驗證

- `rebuild.py --update-lock`：176 個來源、185 個鎖定輸入通過。
- `rebuild.py --build --configfile scratch/clan-nuget.config --output scratch/clan-build --write-receipt`：product／audio／launcher 成功，0 錯誤；產品有原有 `MagicUi.cs` 兩個 nullable 警告。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-checks.json clan`：56 項通過。
- 同入口 `map-continuity` 群組：184 項通過，包含此次 56 項，證明 CI 使用的入口已實際執行血盟測試。
- 所有六種 C／三種 S 格式都有合成封包測試；另測 Big5／NUL／過長名稱、BMP 六種位深與兩種方向、惡意尺寸／截斷／padding、他人與本人更新、換盟／退出清除識別、快取生命週期、重複送出與排隊失效。
- 本機 NuGet.org HTTPS 發生認證／TLS 錯誤，改從原專案 `engine/vendor` 唯讀還原 WebView2 套件。自訂 config、暫存、套件快取與輸出均在本 worktree；未修改原專案。
- 資源拒收檢查與 GitHub CI 結果見 PR checks 及最後 RESULT；下方來源路徑均提交，scratch 證據不代替測試原始碼。

本輪產品 DLL SHA-256：`8775fabd3b8da21937440b9b45693c4230902cced898cfc33ae4c09830a40f51`。

## 未完成／未驗證什麼

1. **加入確認未接入**：伺服器 S219／message=97 的入盟邀請被既有 Trade 模組消耗。依使用者明列範圍不可修改 Trade.cs，也不可新增 S219 分派。因此只能送出申請，尚不能在本客戶端作為招募者接受／拒絕。
2. **成員列表未顯示**：C68 已能送出，但真正回包是 S167／S170／S250 子碼171，不是 S200。缺少授權接收掛接，沒有把關注血盟名單冒充成員。
3. **盟徽自動查詢未完成**：C72 的編碼／傳送 API 已具備，S87 clan ID 與 C72 emblem ID 不同；S118 的回覆與廣播也使用不同識別語義。沒有可靠映射就不猜 ID。目前僅可繪製已收到、鍵可對應 S87 的廣播盟徽。需要 S167 掛接及伺服器 emblem ID 初始化契約確認。
4. 尚未取得實際既有盟徽 payload。BMP 僅有合成驗證；非 BMP／壓縮 BMP 目前明示無法解析。原生 UI、真實服務器往返與成功建立／加入／退出／驅逐、盟徽視覺位置尚未實測。
5. 沒有啟動遊戲、伺服器、DB，沒有複製遊戲資源或伺服器程式碼，未修改 releases／封存 lock／`docs/REPORT.md`，沒有刪除或清理 worktree。

## 產出路徑

- 產品：`engine/source/Clan.cs`、`ClanUi.cs`；兩個允許的共用掛接。
- 測試：`engine/_integration/dev/clan-tests/ClanChecks.cs`、`README.md`；`demo-tests/DemoChecks.csproj`、`Program.cs`。
- 格式、來源 SHA 與缺口：`docs/protocol/clan.md`。
- 來源鎖：`engine/_integration/dev/rebuild-lock.json`。
- 本機未追蹤證據：`scratch/clan-build/`、`scratch/clan-checks.json`、`scratch/clan-map-checks.json`；建置 DLL 在 `engine/build/client-product-dev/`。

## 給下一棒的提示

由調度器／Claude 裁定是否把 Trade.cs 的 S219 最小轉交、S167／S170／S250 血盟子碼掛接加入此任務範圍；本輪已向使用者提出範圍問題，尚未取得答覆。確認後由本機執行器完成 challenge 接受／拒絕與成員顯示、補測。

請伺服器維護者唯讀核對 emblem ID 初始化／C72 查詢契約：本次 src 搜尋只見 `setEmblemId` 定義、未見有效呼叫，不能假定它等於 clan ID。待有可靠映射後實作自動查詢及去重／快取更新。上述程式完成、審查與 CI 通過後，再由 dot 按授權安排 P24Lab 真機驗收；本輪不請使用者代做開發或測試準備。
