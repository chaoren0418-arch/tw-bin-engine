# Issue #192／T105：音效與座標特效封包

## 結論

**部分完成，尚未達成線上接線驗收。** 模組程式編譯與離線測試通過；需要確認兩處最小掛接後才能完成整張任務。沒有啟動遊戲、伺服器、DB 或音訊裝置，沒有讀取機密配置、複製遊戲資源或更動原專案／封存。

## 做了什麼

- `SoundPackets.cs`：嚴格解碼 S22 與 S55／S106，含 unsigned ID、repeat、16-byte H(time) 及 padding；S22 排程沿用 `CombatSfxController`，時長取既有核定 WAV，保留音訊核心白名單。
- `MapEffects.cs`：新增座標／附著圖像佇列、原始素材查核、逐幀顯示、目標追隨、缺素材診斷及 WorldEpoch 退役；`MagicSpells.AttachedEffect` 增加 appearance 動作選擇擴充點。
- 既有六種短版 S55 委派原本處理；其他變體進新圖像路徑，gfxId 不當 soundId。各佇列最多 256 筆，不以替代素材冒充支援。
- 只更新 dev lock；未改 `docs/REPORT.md`、封存 lock、音訊核心或其他 M6 模組。

## 怎麼驗證（已驗證）

- Python 3.12／.NET SDK 10.0.300：先 `rebuild.py --update-lock`，再 `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-192/build`，產品及 launcher 建置通過。既有 `MagicUi.cs` 兩項 nullable 警告，沒有新增編譯錯誤。
- `SoundPacketChecks.csproj`：**116 項通過**，執行本次產品 DLL；合成封包涵蓋欄位邊界、所有截斷長度、padding、S55 8／16-byte 變體、time=0、逐幀／期限／跟隨／缺資源、Reset／S149／S206、佇列上限、repeat 排程、不重播、退役與延遲音訊 registration fence。
- 測試 `Feed` 目前直接呼叫模組接收方法，**未驗證共用 WorldSession 接線**；音訊使用記憶體 transport，不是實際聽音。
- `check-no-game-assets.py`：pass，0 errors。新測試／文件加入索引後須以最終提交再核驗。
- CI 狀態以 PR checks 為準；本報告建立時尚未推送、尚未取得 CI 結果。

## 未驗證／未完成

1. 待確認的最小掛接：在 `WorldSession.ReceiveCore` 接收鏈前，S22／S55／S106 各加一個只呼叫模組的 case；在 `ClientCombatSfx.cs` 原 Pump 引數加 `WithPacketSounds(...)`。S55 必須早於既有魔法／背包分支。M6 契約明列共用 switch，但未列 `ClientCombatSfx.cs` 為可改檔案，因此已詢問範圍、未自行越界。
2. repeat 的「額外次數」和 time 的「秒」是明示產品政策，伺服器序列化器沒有證明精確語義，交 M6 smoke 核對。
3. 核心僅支援既有 33 個核定 WAV，圖像僅支援現有封裝 catalog。其他 ID 診斷缺資源；新圖像佇列未擴充 sprite 幀事件音訊，不宣稱全資源／全音效支援。
4. 沒有原生登入、畫面、混色或聽音驗收。

## 產出路徑

- `engine/source/SoundPackets.cs`
- `engine/source/MapEffects.cs`
- `engine/source/MagicSpells.cs`
- `engine/_integration/dev/sound-packets-tests/`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/sound-effects.md`
- `docs/reports/issue-192.md`
- 本 worktree 未追蹤證據：`scratch/issue-192/checks.json`、`scratch/issue-192/build/`；待確認掛接內容：`scratch/issue-192/pending-wiring.patch`（review 草稿，不是完整 unified diff）。

## 給下一棒的提示

範圍確認後由 codex-local 補上述兩處掛接，把測試 Feed 改成 `WorldSession.Receive`，加入舊 S55 通知／單一路徑與真實接收分派測試，更新 lock、重建並重跑離線驗證。若本輪以 draft PR 交接，請勿當作 T105 完成合併；Claude／調度器先裁定掛接範圍。合併審查仍交 Claude、合併由使用者；實機驗收集中交 dot 的 M6 smoke，核對清單見 protocol 文件。
