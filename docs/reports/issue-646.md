# Issue #646（code，T271）：rebuild-lock 改成合併後才重產，PR 不再修改 lock

分支：`task/646`。依據 #644（主控裁定採用建議 1）。

## 做了什麼
1. `engine/_integration/dev/rebuild.py`
   - `--build`（不帶 `--strict`）在 lock 過時、缺檔或損壞時，印 `WARN: rebuild-lock.json is stale…`，改用記憶體中重掃的**暫時 lock**繼續建置，**不寫回 `rebuild-lock.json`**。stdout 狀態為 `dev-inputs-temporary-lock`；`result.json` 加 `lock: committed|temporary` 與 `lockDrift`（reason／added／removed／changed）。暫時 lock 時略過 `receipt.json` 比對（來源變了，DLL 本來就會不同）。
   - 新增 `--build --strict`：舊行為，lock 不一致就拒絕。不帶參數的模式與 `--verify` 維持嚴格。
   - 新增 `--lock-out PATH`：另外把目前輸入重掃成 lock 寫到 PATH，內容與 `--update-lock` 產出逐 byte 相同；不能指向已 commit 的 lock，也不能和 `--update-lock` 一起用。
   - 缺少必要輸入、路徑逃出 `engine/` 時，任何模式照樣拒絕。重構：`scan_lock()`／`write_lock()` 由 `update_lock()` 共用，lock 輸出格式沒變。
2. `.github/workflows/build.yml`（`dotnet` job）
   - 新增步驟，執行 `test_rebuild_paths` 與 `rebuild_tests`。
   - PR 與 main 的 dev 建置都改用 `--build … --lock-out engine/_integration/evidence/ci-dev/candidate-lock/rebuild-lock.json`。
   - 只有 push 到 main 時跑嚴格核驗（不帶參數的 `rebuild.py`）。lock 過時時這一步失敗，並上傳 artifact `rebuild-lock-candidate`，**不自動 commit**。
3. 文件：`docs/design/m6-contract.md` 第 3、4 條改成「PR 不改 lock，同步時 lock 取 main 版本，合併後由調度器重產」；新增 `engine/_integration/dev/README.md` 說明各模式。
4. 新測試 `engine/_integration/dev/rebuild_tests.py`（6 個），沿用 `test_rebuild_paths` 的暫存 repo fixture（放在 `scratch/`），不跑 dotnet。

`rebuild-lock.json`、r33／r34／r35 封存 lock 與 releases 都沒動。

## 怎麼驗證（本機 Windows，Python 3.12.10，SDK 10.0.300）
| 群組 | 改動前 | 改動後 | 結果 |
|---|---|---|---|
| rebuild.py Python 單元測試（`test_rebuild_paths` + `rebuild_tests`） | 7 | 13 | 全綠 |
| C# 各測試專案（demo-tests、mail、sound、weapon-repair、combat-sfx、player-chat、web-resource、packet-trace、module-tests…） | 沒改 C# 程式，數量不變 | 同左 | 由 CI 執行，見 PR 的 CI 結果 |

- `rebuild.py --build --strict --configfile .github/nuget.ci.config`（乾淨樹）：PASS，product DLL `f9409cae…`。
- 模擬 PR 新增來源、沒改 lock（暫時加 `engine/source/Zz646Probe.cs`）：
  - `--build`：印 WARN，`added: ["source/Zz646Probe.cs"]`，建置 PASS，`result.json` 為 `lock: temporary`；
  - 不帶參數與 `--build --strict`：都 `REFUSED: input inventory drift`（exit 2）；
  - `--lock-out` 產出的 lock 與 `--update-lock` 的 SHA-256 相同；
  - 測完把 probe 檔刪掉，`git checkout` 還原 lock，SHA 與原本相同；`rebuild.py` 嚴格核驗通過（311／320 檔）。
- `check-no-game-assets.py`：pass（errors 0）。

## 未驗證
- main push 上的嚴格步驟，以及 `rebuild-lock-candidate` artifact 的上傳：要等合併進 main 才會跑，PR 的 CI 跳過這兩步。
- 調度器的閘門實際用哪條指令：若用 `rebuild.py --build`，不必改；若用不帶參數或 `--verify` 的嚴格模式，PR 會被擋，需由 #644 改。

## 產出路徑
`engine/_integration/dev/rebuild.py`、`engine/_integration/dev/rebuild_tests.py`、`engine/_integration/dev/README.md`、`.github/workflows/build.yml`、`docs/design/m6-contract.md`、本報告。

## 給下一棒的提示
- 調度器端（#644）：合併後在 main 跑 `rebuild.py --update-lock` 並 commit lock（可以直接取 CI artifact `rebuild-lock-candidate`，內容逐 byte 相同）。閘門改用 `rebuild.py --build`，不要要求 PR 改 lock。同步輪遇到 lock 衝突時取 main 版本。
- 合併後到重產 lock 之前，main CI 的「Strict dev lock check」會紅，這是設計上的提醒；lock 重產 commit 推上去就會恢復綠燈。
- `docs/BUILD.md` 第 160 行仍寫「lock 與程式一起提交在同一個 PR」，不在本任務的可改範圍，建議另開小任務改成 #646 的政策。CLAUDE.md／AGENTS.md 的建置敘述沒有衝突，不用改。
