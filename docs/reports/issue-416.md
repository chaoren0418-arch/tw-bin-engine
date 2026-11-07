# Issue #416：T153 暫時能力、酒醉、水中呼吸、刪除技能

## 做了什麼

- 新增 `engine/source/TimedBuffs.cs`，處理五個封包：
  - S166／S188：更新力量、負重、敏捷，剩餘時間進既有的 `MagicBuffs`。
  - S103：只有自身 type 1～3 才搖晃，type 0 解除。type 8 是三段加速，不算酒醉，仍計入 `unhandled`。
  - S126：水中呼吸的剩餘時間，同樣進 `MagicBuffs`。
  - S160：完整解碼後才呼叫 `RemoveLearnedMagic`，封包有錯時不會改到狀態。
- `WorldSession.cs` 只加五個 case；`Magic.cs` 只加 `RemoveLearnedMagic`；`Renderer.cs` 只加 `ApplyScreenShake`。`MagicUi.cs` 未修改。
- 酒醉在 S206、Reset 時清除（綁 `WorldEpoch`，伺服器傳送後會重送 S103），收到自身 S255 mode 1 時也清除。
- 測試放在 `engine/_integration/dev/timed-buffs-tests/`，接上 DemoChecks 的 `timed-buffs`、預設入口及 CI 會跑的 `map-continuity`。
- 協定結論寫在 `docs/protocol/timed-buffs.md`，分已驗證與推測。
- 已合併 origin/main（含 #388 異常狀態）並以 `--update-lock` 重產 dev lock（213 來源、222 鎖定檔）。

## 審查時請注意

1. **S160 原本就已實作**：`Magic.cs` 的 `ReceiveMagic` 早已處理 `opcode is 164 or 160`。依任務範圍加了 case 之後，該分支的 160 路徑不會再被執行；因為 `Magic.cs` 只能加介面，所以沒有刪。外部行為不變（訊息、例外、`BasicSupplyReply` 都保留）。之後可由負責 `ReceiveMagic` 的任務清掉。
2. **搖晃不是直接掛在 `OnPaint`**：`OnPaint` 在 `Program.cs`，不在可修改清單內。所以在 `TimedBuffs.cs` 的 `ClientForm` 覆寫 `CreateHandle`、訂閱 `Paint` 事件，利用 `OnPaint` 先呼叫 `base.OnPaint(e)` 的順序套用位移。若改為在 `OnPaint` 直接呼叫 `ApplyScreenShake` 會更直接，但需要放寬範圍。
3. **搖晃有邊緣縫隙**：`SetClip` 在位移後才設定，畫面一側最多會露出 6 像素的表單底色。真機若覺得明顯再調整。
4. **計時會顯示成狀態圖示**：S166／S188／S126 走既有的 `ReceiveBuff`，所以 HUD 圖示列也會出現，未改圖示程式。S126 沒有技能編號，用負數鍵 `-3126`，顯示為通用圖示。

## 怎麼驗證（已驗證）

- .NET SDK 10.0.300：`rebuild.py --update-lock`、`--build` 通過（product、audio、launcher），DemoChecks 0 警告 0 錯誤。本機沒有 `python` 指令，改用 `py -3`（3.12.10）。
- DemoChecks（每組都會先跑 Party、Pets、Polymorph，所以總數大於模組本身）：
  - `timed-buffs`：560 項通過，本模組 284 項。
  - `map-continuity`：22642 項通過，含同樣 284 項。
  - `afflictions` 428、`combat-admission` 1409、`buddies` 445、`item-transfer` 349、`doors` 597、`polymorph-cadence` 309，全部通過。
- 測試涵蓋：S166、S188、S103、S126、S160 兩種長度；計時、解除、到期、其他物件；截斷、padding、多餘資料的原子拒絕；搖晃掛載點的像素位移與 `OnPaint` 順序。本模組沒有新增 C 格式。
- NuGet 用 scratch 中的設定唯讀引用原專案 `engine/vendor`，未複製遊戲資源。
- `check-no-game-assets.py` 通過：3412 個追蹤檔、0 錯誤。CI 結果見 PR。

## 未驗證什麼

- 未啟動遊戲、伺服器、DB，沒有真機畫面。像素測試用的是測試內的小控制項，證明的是繪製順序，不是 `ClientForm` 實際畫面。
- **搖晃的波形是自訂的**（振幅 `等級×2` 像素，週期 1.2／1.7 秒），不是取自原客戶端。
- **自身加速解除酒醉是推測**：伺服器 `setDrink(false)` 不送 S103，客戶端用 S255 mode 1 比照；加速裝備若也送 mode 1，可能多清一次。
- 不帶群組的預設入口未跑（worktree 沒有 `engine/assets` junction，依 #389 紀錄會停在 `Movement123`），它的接線只經過編譯驗證。
- 產品內的 `--magic-tests` 未跑；S160 行為改由本模組測試覆蓋。
- 三段加速（S103 type 8）與網頁場景的搖晃未實作。

## 產出路徑

- 模組：`engine/source/TimedBuffs.cs`
- 測試：`engine/_integration/dev/timed-buffs-tests/`（`TimedBuffChecks.cs`、`README.md`）
- 協定：`docs/protocol/timed-buffs.md`
- 報告：`docs/reports/issue-416.md`
- 本機未追蹤證據：`scratch/issue-416/*.json`
- 分支：`task/416`

## 給下一棒的提示

- Claude 審查時請先看上面「審查時請注意」第 1、2 點。
- dot 在 M6 smoke 建議核對：喝酒後畫面搖晃的幅度與節奏、傳送後是否延續、喝加速藥水是否停止；力量／敏捷提升的數值與倒數；伊娃的祝福倒數；刪除技能後魔法清單是否更新。
- 若波形要對齊原客戶端，只需改 `TimedBuffRules.ShakeOffset`。
