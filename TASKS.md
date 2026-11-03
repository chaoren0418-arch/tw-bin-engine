# TASKS — tw-bin-engine

格式：`ID | 狀態 | 負責 | 範圍 | 驗收條件`。狀態：todo / doing / review / done / blocked。
Cursor 接任務時只改「範圍」列出的路徑；PR 標題用 `ID: 摘要`。

## M0 可重現建置

### T001 | done | Claude | `.github/workflows/build.yml`, `.github/nuget.ci.config`
GitHub Actions 在 windows-latest 上 `dotnet build engine/source/OfflineClient.csproj -c Release` 成功。
驗收：main 分支 CI 綠燈。

### T002 | todo | Cursor | `engine/_integration/r35/`（新建）, `engine/build-product.ps1`, `engine/README.md`, `project-manifest.json`
建立 r35 canonical lock：以目前 `engine/source`+`audio`+`web` 的 181 檔計算 SHA-256 清單，寫成 `engine/_integration/r35/rebuild-lock.json`（格式沿用 r34）；複製 r34 的 `rebuild.py`／`product-build.ps1` 為 r35 版並指向新 lock；`build-product.ps1` 改呼叫 r35；README 的「177 來源」改為 181 並說明 P36 新增檔；`project-manifest.json` 加 `canonical_revision: r35`（正式入口仍 r33，不改 `entry`）。
驗收：`python -B -X utf8 engine/_integration/r35/rebuild.py --verify` 在本機 exit 0；r33/r34 目錄不動。

### T003 | todo | Cursor | `engine/_integration/r35/*.py`, `engine/_integration/r35/*.ps1`, `engine/build-product.ps1`, `啟動引擎.cmd`
去除硬編路徑：所有 `D:\lineage開發\TW1608262200研究` 改為 `$PSScriptRoot` 推導或環境變數 `TW_ENGINE_ROOT`（未設時預設為腳本所在 repo 根）；資源根改為 `TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`）。
驗收：把 repo clone 到任意路徑（例如 `D:\tmp\twtest`）執行 `engine\build-product.ps1` 能建置；`啟動引擎.cmd --check-only` exit 0。

### T004 | todo | Cursor | `docs/BUILD.md`（新建）
寫「乾淨機器從零建置」文件：需要的 SDK（.NET 10.0.300、Python 3.12、WebView2 runtime）、資源目錄、三條指令（restore／build／check-only）、常見錯誤。
驗收：使用者照文件在另一台機器或乾淨目錄重現成功；Claude 審閱。

### T005 | todo | Claude | `CLAUDE.md`, `AGENTS.md`, `.cursor/rules/project.mdc`
依 M0 結果更新狀態段，三份同步。

## M1 真機基線（使用者操作，Claude 準備）

### T010 | todo | Claude | `docs/native-baseline/CHECKLIST.md`（新建）
N01 流程逐步核對表：每步要截圖什麼、看哪個 server 回包、預期值；附失敗時要收集的 log 路徑。
驗收：使用者能照表獨立操作。

### T011 | blocked(T002,T003,T010) | 使用者 | `docs/native-baseline/r35.md`
用 r35 建置跑完 N01：登入→選角→移動→攻擊→技能 1/3/4/8→道具／裝備→能力點→存檔→重登。
驗收：每步有截圖與結果；失敗項各開一個 T1xx bug 任務。

### T012 | blocked(T011) | Cursor | `work/P25-architecture-baseline-r1/completion-matrix.json`, `work/P25-architecture-baseline-r1/finite-backlog.json`
把 T011 結果回寫：通過的列加 `nativeR35Accepted: true`；I01、I02 改 done（P26/P27 已完成對應工作）。
驗收：JSON 可解析；變更列數與 r35.md 一致。

## M2 完整遊戲循環（待 M1 結果排序）

### T020 | todo | — | M07 NpcServices/NpcInteraction/Trade/PrivateShop
NPC 商店買賣、技能學習、倉庫、直接交易、私商 真機序列（N03）。先由 Claude 讀 server 端協定寫出每項的封包序列與前置條件，再拆成 Cursor 任務。

### T021 | todo | — | M02 WorldEntry/OriginalMapCache/Teleport/ShipTravel
換圖、旅館、船運 連續性（N04）。

### T022 | todo | — | M09 Effect6016Controller/Host, AudioCore
正式世界 6016 音訊 dispatch／重疊／退役（N05）。

### T023 | todo | — | M10 WebPanel, PlayerHud*, PlayerPointer*
原生畫面／滑鼠／HUD／聊天焦點，執行 P27 已備好的 12 組 native 計畫（N02）。

## M3 內容擴充與交付

### T030 | todo | — | M04 Magic*, MagicSpells
技能由 1/3/4/8 擴充到各職業基本技能（S03，需 skill ID 與 gfx 來源）。

### T031 | todo | — | M06 Ground*, PickupPresentation
Slow 速度拾取／丟棄（目前不支援）。

### T032 | todo | — | M08 MapEffects, SceneOpacity
MAP-F4 map-local effect group fade（S01，source-blocked，需真實非零關聯樣本）。

### T033 | todo | — | `docs/ARCHITECTURE.md`, `packaging/`
交作業版架構文件 + 含 runtime 的可攜安裝包。

## 已知矛盾（T002/T003 要一起解掉）
- `engine/_integration/HANDOFF.md` 與 `result.json` 仍是 r28 內容。
- `docs/project-consolidation/progress.md` 以 r28 為驗收基準。
- completion-matrix 部分列說 6016 待整合，同檔 r28Current 已整合（P25 標 stale）。
