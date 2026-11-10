# Issue #851 — T367：R9-G 學魔法圖示

依據：`docs/reference/followups-9.md` §11（Q7-11）、§16.2 R9-G；`gap-matrix.md` §15.11。
範圍：只改 `engine/source/SpellTradeSource.cs`、`SpellExchangeUi.cs`、`SpellTradeTests.cs`。

## 做了什麼

1. **`SpellTradeProtocol.SpellIcons`**（`SpellTradeSource.cs`）：加入 `word_136D548` 常數表（TW1608262200.bin SHA-256 `e12949cf…641a2`，`.data` VA `0x136D548`／FO `0xF6BD48`），索引＝技能編號 − 1；`SpellIcon(index)` 表外回 0。
2. op 44（S41，金幣學魔法）與 op 175（S13，以物品學魔法）的 `TradeListItem.Icon` 從 0 改成 `SpellIcon(索引)`；檔頭註解從「未對應 surf、不畫圖示」改正為已驗證的路徑。
3. **`SpellExchangeUi.cs`**：拿掉「模式 7 時 `IconSource` 回 null」的包裝，模式 7 直接沿用 `WeaponRepairUi.cs` 接好的 `IconSource`（`ItemIconSource` 的 `<編號>.tbt`，找不到時用背包 PNG），和背包、商店同一條路（原版 `sub_891DF0` → `sub_88CE60`）。`TradeWindowPanel.cs` 沒改；其他模式不受影響。
4. **測試**（`SpellTradeTests.cs`，`[WorldModuleTest("spell-trade")]`，自動發現）：
   - 表長 236、非 0 有 190 個、總和 300220、FU9§11.1 列的前段與抽樣值（0–8、23、79、80–85、86、96、111、229、尾端全 0）、表外索引（−1、236、int.MaxValue）都回 0。
   - op 44 清單索引 0／3／8／16 → 圖示 383／386／388／393（已學的列停用，但圖示保留）。
   - op 175 清單索引 5／12 → 657／392；表內 0（23、80）與表外（300）→ 0。

## 已驗證（靜態）

- 表的內容是我直接從 BIN 的 FO `0xF6BD48` 讀出來的，原始碼裡 236 個值逐一和 BIN 比對過，完全相同。
- **表長是 236，不是 FU9§11.1 寫的 240**：VA `0x136D720`（索引 236 開始的位置）在 FO `0x47DF53`、`0x47DF6E` 被程式碼直接引用（立即值掃描），內容是成對 dword 組成的另一張表（`46,0,4,0,47,0,5,0,48,0,6,0…`）。所以 FU9 的「240 個 word，非 0 的 192 個」多算了尾端的 `46`、`4`；正確的是 236 個，非 0 的 190 個。`0x136D548` 本身有 28 處立即值引用。
- 資源（PakViewer CLI，用 `docs/reference/rea-scripts/pak/pak_index.ps1` 重新列出 `D:\lineage開發\0-客戶端` 全部 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`；`launcher\TW13081901.pak` 沒有 idx，打不開）：190 個 `<編號>.tbt` 有 185 個在本機（Sprite.idx 138 個，其餘分散在 Sprite00–15）。**缺 5 個：5731、5734、5737、6458、6501**，和 FU9§11.1 一致（參考客戶端 kor25／ly／t89 都有，依 FU9）。照引擎規則，缺的就不畫（`ItemIconSource.Get` 回 null）；也沒有複製任何資源進 repo。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false` → 0 錯誤。
  - 注意：本機全域 NuGet 快取的 WebView2 1.0.4258.31 不完整（少了 `build/*.targets`），重新還原後在 Windows 上還是會報 NETSDK1130（winmd 參照）。這和本次修改無關。本機只為了確認能編譯，用了兩個不進 repo 的繞法：`NUGET_PACKAGES=engine/.nuget/packages`，以及 `-p:CustomAfterMicrosoftCommonTargets=scratch/task851/nowinmd.targets`（濾掉 winmd，改參照 `lib_manual/netcoreapp3.0`）。repo 的建置設定沒有改。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4335 檔，0 錯誤）。
- 單元測試與本組離線測試：依本機限制不在本機執行，交給 PR 的 GitHub Actions CI（`--module-tests`）。

## 未驗證什麼

- 測試是否全部通過，以 PR 的 CI 結果為準。
- 真機畫面（待驗證，交給 Codex／dot）：在 P24Lab 的學魔法 NPC 開模式 7，確認每列左邊的 32×32 圖示和原版一致、缺的 5 個是空白，其他交易模式（商店、修理、私商）的圖示沒有改變。
- 索引 ≥ 236 時原版會讀過界（讀到下一張表）；本引擎回 0、不畫。3.81 伺服器正常不會送這種索引（推測）。

## 產出路徑

- `engine/source/SpellTradeSource.cs`、`engine/source/SpellExchangeUi.cs`、`engine/source/SpellTradeTests.cs`
- `docs/reports/issue-851.md`（本檔）
- 分支 `task/851`

## 給下一棒的提示

- FU9§11.1 的「240 個／192 個」要改成「236 個／190 個」（下一次文件任務順手修正）。
- 缺的 5 個圖示（5731、5734、5737、6458、6501）如果之後要補，只能在執行期從參考客戶端讀，不可以複製進 repo。
- 本機建置環境：全域 `%USERPROFILE%\.nuget\packages\microsoft.web.webview2\1.0.4258.31` 不完整，建議調度器或本機對話安排修復（重新還原，或改用 `engine/vendor` 離線源）。
