# Issue #551 — T233：送 C14 並解析 S139（protocol-gaps E）

依據：`docs/reference/protocol-gaps.md` 第 4、8 節（T225／#532）；3.81 欄位：`docs/reference/server-protocol.md` 的 C_ServerVersion／S_ServerVersion。

## 做了什麼

- **新檔 `engine/source/ServerFeatures.cs`**
  - `ServerFeatures` record：`Status`、`ServerNo`、`LanguageCode`、`CodePage`、`Flags`、`Uptime`、`TrailingBytes`；衍生 `FastPickup`（旗標 `0x10000`）、`PickupThrottleMs`（套用既有的 `GroundRules.PickupThrottle`：600／986 ms）、`ContinuousAttack`（`0x800000`）、`CodePageMismatch`（932／936 與引擎 950 不合）。
  - `CodePageOf`：照 `sub_94DBB0` 的對照 0→949、3→950、4→932、5→936，其他→1252。
  - `C14()`：沿用 TW16 C_VERSION 欄位形狀 `H(52) D(CodePage=950) C(0) D(0) D(0) C(0)`，補齊到 24 bytes。3.81 `C_ServerVersion` 不讀任何欄位；引擎沒有的雜湊等值一律送 0，不偽造原 BIN 的值。
  - `TryParse(S139)`：讀法照 `sub_94DBB0`：`c` 狀態；狀態 0 時 `c d×5 c c c(語系) d(功能旗標) d(UPTIME)`，後面的 `H(1) C(0)` 與補齊只記長度。格式不符時回 false 與原因，**不丟例外**。
  - `LoginConnection`（partial）：連線層保存 `ServerFeatures`（`Cancel()`／每次 `Run()` 開頭清除；切角色時 `World.Reset()` 不影響）；`SendC14` 吞掉所有非取消的送出例外，只記 `c14Error`。
  - `WorldSession`（partial，M7-A 擴充點）：`[WorldReceive(139)]` 接進世界後才到的 S139、`[WorldReset]` 清除、`[WorldSnapshot("serverFeatures")]`。沒有改 `WorldSession.cs`。
  - `[WorldModuleTest("server-features")]`：44 個離線斷言（合成封包，不含伺服器原始碼或遊戲資源）。
- **`engine/source/LoginConnection.cs`**（5 處）
  - 收到首包（seed）後、送 C119 前先送 C14（與原版「S_KEY 後送 C_VERSION」順序相同）。
  - 尚未進世界的任何階段收到 S139：記封包追蹤、保存旗標後 `continue`，不改登入狀態、不重設逾時，不再落到「unexpected opcode」。
  - 進世界後每個封包套用後，把 WorldSession 收到的 S139 收進連線層。
  - `Snapshot()` 加 `serverFeatures`（`c14Sent`、`c14Error`、`s139Count`、`error`、`features`）。
  - `Cancel()` 清除。
- `LoginProtocol.cs` 沒有改（`LoginReader`／`LoginPackets.Pad` 直接沿用）。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

**容錯（任務要求「送出失敗或沒有回應時不能影響登入」）**：沒回應→`ServerFeatures` 為 null，呼叫端照現行預設；格式錯誤→只記 `error`；狀態 1／2／3（原版顯示 52／45／3347 後結束）→只記錄、`Accepted=false`、旗標不採用；語系 932／936 不合（原版 558 後結束）→只標 `codePageMismatch`；C14 寫入例外→只記 `c14Error`（取消例外照常往外傳）。

## 怎麼驗證（已驗證）

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` 後 `engine/build-product.ps1` | PASS dev（已同步 origin/main `55cb231c`），product DLL SHA-256 `478853a9c491e4d0cdba42ab7b6c6f269131a410b64632733bd33cbb6dc2e9d5` |
| `P11.ClientEngine.exe --module-tests`（CI 同指令） | pass，5 組 0 失敗、261 斷言；其中 `server-features` 44 斷言、`world-extensions` 預設登錄表無錯誤 |
| `login-tests`（LoginChecks） | pass，396,589 斷言 |
| `character-management-tests` | pass，218 斷言 |
| `engine/tools/check-no-game-assets.py` | pass，0 errors |
| CI | 見 PR |

## 未驗證什麼（待驗證）

- **沒有連 3.81 伺服器實測**：P24Lab 是否真的回 S139、回在 S233 之前還是之後、實際 `CLIENT_LANGUAGE` 值，都要等真機 smoke（dot）看 `Snapshot().serverFeatures`。依靜態分析，3.81 對 C14 屬 TRANSPORT、任何階段可收，不會因此斷線（推定，PG§4 已驗證伺服器端無檢查）。
- 送 C14 會讓客戶端→伺服器的加密金鑰多走一步；伺服器同樣依序解密，理論上一致（推定），需真機確認登入仍正常。
- 拾取節流**尚未**改跟旗標走：`Ground.cs` 的 `PickupFastFlag` 目前以 `World.Actor?.Speed==1` 判斷，不在本任務可改範圍。

## 產出路徑

- `engine/source/ServerFeatures.cs`（新）、`engine/source/LoginConnection.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。
- 分支 `task/551`。

## 給下一棒的提示

- 真機 smoke 時檢查 `login.Snapshot().serverFeatures`：預期 `c14Sent=true`、`s139Count=1`、`features.flags=0x77D82`、`fastPickup=true`、`pickupThrottleMs=600`。若 3.81 沒回或回錯，登入也應照常完成。
- 若要讓拾取節流跟伺服器旗標：另開 code 任務改 `Ground.cs`，`PickupFastFlag` 改用 `ServerFeatures?.FastPickup`（沒收到時的預設需裁定：986 或 3.81 已知的 600）。注意 PG§4 寫的「旗標開時 600 ms」與現行 `Speed==1` 判斷不同，需先釐清 WO §2 的「fast flag」是指哪一個。
- 本機 worktree 建置需要 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg`（未追蹤），本次從主專案複製一份；Python 不在 PATH，用 `%LOCALAPPDATA%\Programs\Python\Python312`。
