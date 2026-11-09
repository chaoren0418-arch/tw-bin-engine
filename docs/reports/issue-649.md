# Issue #649 T272：Q-2 補做——城主王冠、死亡統計值、座標與地圖字串

依據：`docs/reference/sprites-and-strings.md`（SS，T267／#628）、`docs/reference/followups-3.md`（FU3，T275／#665）第 2、4 節、`docs/reference/send-sites-2.md`（SS2）第 14 節。

## 做了什麼

| 項目 | 結果 | 檔案 |
|---|---|---|
| 1. 城主王冠（R2-I） | **#618 已完成**，這次沒有再改程式：`crown.raw` 執行期讀取與 pin、12 格 RGB555、錨點 +17／−52（6080／6094 是 −62）、他人條件 `(Flags & 2) == 0 && y > 0`（FU3§2）、整格不裁切，都在 `ObjectRendering.cs` 的 `CastleCrownImages`／`CrownLayout`，測試在 `ObjectDrawTests.Crowns`。只更新 `Castle.cs` 裡過時的「圖源待查不畫」註解。 | `Castle.cs`（只改註解） |
| 2. 死亡統計值（R2-D） | 客戶端自己算（SS§3.1）：進入世界後第一個 S8 記等級與經驗%；經驗列＝`(等級×100＋經驗%)` 差，格式 `"%.4f%%"`。金幣：3.81 的金幣是背包物品，以登入背包（S5）裡的金幣當基準（**推測**的對應，原版取 S_STATUS +16）。獲得道具照 FU3§4.2：S5 建基準、S15 數量超過基準記差額、S24 回到基準以下移除、S57 移除；只比對物品 id。殺怪數固定 0。字串 3123–3127、3131、1587、3344 讀 `string-c.tbl`（沒有表時用 SS§4.1 列出的原文）。都用 M7-A 擴充點：`[WorldReceive(8/5/15/24/57, Observe=true)]`、`[WorldReset]`、`[WorldSnapshot("userPlayStats")]`。 | `Recovery.cs`、`NativeUi/UserPlayStatsPanel.cs` |
| 　原生面板 | 四列顯示數值（沒有基準的列仍是「—」）；新增 `NewItemList_Button`（5325–5327，滑過畫 3131 提示，清單空時送系統訊息 3127）；`DungeonTime_Image`＋`DungeonTime_List`（名稱＋分鐘＋1587，≥ 10,000,000 改 3344）。WinForms 備援面板的數值欄同步更新。 | 同上 |
| 3. 發送座標字串（R2-M） | 1784／1785／2139 讀 `string-c.tbl`；地名讀 **`MapDesc-c.tbl`**（`TextArchive.ReadMapDesc`，解密後 SHA-256 pin `2bd97914…a8d9`）。1784 照 `sub_8D43B0` 的 C 式 `%s` 填入（寄件者、地名）。兩張表由 `LocationRegionLoader` 在執行期裝進 `LocationShareProtocol.Strings／MapDesc`；沒有表（CI、既有測試）時維持原本的編號占位字。 | `LocationShare.cs`、`TextArchive.cs` |
| 4. 地監時間清單（Q-3） | 死亡統計窗每次由關變開，送一次 3.81 C63 `C(9)`（`3F09…`）；回來的 S250／159 由既有 `PacketBoxStatus.cs` 接收，面板讀 `MapTimes`。op 55／41、55／44 不送。 | `Recovery.cs` |
| 測試 | 新檔 `Q2FollowupTests.cs`，`[WorldModuleTest("q2-followup")]`，由 `--module-tests` 自動發現：註冊、經驗基準、獲得道具規則、金幣差、格式、C63 邊緣觸發、面板（位置、狀態圖、3127、地監列、繪圖）、1784／1785／2139、MapDesc 與 string-c 不混用、MapDesc pin 拒絕。 | `Q2FollowupTests.cs` |

## 怎麼驗證

- 建置：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（暫時 lock，不寫檔；本機 worktree 沒有離線 `vendor`，所以用 CI 的 nuget 設定）。我改的檔案沒有編譯警告。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 單元測試與本組離線測試：依本機限制**不在本機執行**，交給 PR 的 GitHub Actions（windows-latest `--module-tests`），結果看 PR 的 CI。
- 沒有改 `rebuild-lock.json`、`docs/REPORT.md`、releases、封存；沒有啟動遊戲、伺服器或 DB；圖檔與字串表都在執行期從原客戶端讀，沒有進 repo。

## 未驗證／推測

- **推測**：金幣基準取 S5（3.81 金幣不在 S8）；`sub_88EA30` 當成千分位；地監清單超過 XML 4 列的筆數不顯示、不足的列留空、沒收到 159 前顯示 XML 預設字；3131 提示只畫字、不畫底框。
- **沒做**：S15 的「名稱＋第 6 參數、旗標 0x80」備援比對（FU3§4.2 允許先只用 id）；Reset 鈕（原版重建基準、冷卻 1000 ms）；NewItemList 清單不空時的 `UserObtainUI` 清單窗；mapDesc 超出 `MapDesc-c.tbl` 時原版改用 `desc-c.tbl` 4266，本引擎沒讀 desc 表，改顯示「地區N」。
- 聊天行：`PlayerChat.cs`（不在本任務範圍）仍在前面加「[座標] 寄件者：」，所以 1784 的第一個 `%s`（寄件者）在連結文字裡留空，畫面是「[座標] 甲：從 亞丁全大陸地區發送了座標位置情報。」。要完全照原版整行只顯示 1784，需要改 `PlayerChat.cs` 的 `FormatLine`。
- **待驗證（實機，交 Codex／dot）**：死亡後統計窗的四個數字與原版是否一致；開窗時伺服器是否回 S250／159、清單是否填入；收到座標分享時地名是否正確；王冠真機外觀。

## 產出路徑

- `engine/source/Recovery.cs`、`engine/source/NativeUi/UserPlayStatsPanel.cs`、`engine/source/LocationShare.cs`、`engine/source/TextArchive.cs`、`engine/source/Castle.cs`（註解）
- `engine/source/Q2FollowupTests.cs`
- 本報告 `docs/reports/issue-649.md`；分支 `task/649`

## 給下一棒的提示

- 統計值的資料入口是 `WorldSession.UserPlayStats()`／`LoginConnection.UserPlayStatsSnapshot()`；要做 Reset 鈕時，在 `Recovery.cs` 加一個重建基準的方法（清 `statsNewItems`、重記等級／經驗／金幣），並照 SS§3.1 加 1000 ms 冷卻；不要送 op 55／44。
- `UserObtainUI` 清單窗要的資料就是 `statsNewItems`（id → 差額）。
- 若要讓座標聊天行完全照 1784，改 `PlayerChatProtocol.FormatLine` 讓座標行直接用 `LocationShareProtocol.FormatC(template, 寄件者, 地名)`，並同步調整 `LocationSharePresentation.Links` 的連結範圍。
