# Issue #698 T290：T272 補做——門的 `]`、死亡統計 Reset 與道具窗、地名備援、座標聊天行、名稱旁圖示

依據：`docs/reference/sprites-and-strings.md`（SS）§3、§4；`docs/reference/followups-3.md`（FU3）§2、§4.2；`docs/reference/effects-2.md` §2；`docs/reference/followups-5.md`（FU5）§1；`docs/reference/gap-matrix.md` 13.2 第 8 點；T272 報告 `docs/reports/issue-649.md`。

## 做了什麼

| 項目 | 結果 | 檔案 |
|---|---|---|
| 1. 門的 `]` 子特效（取代已完成的王冠） | 拿掉 `ComposeObjects` 的 `if(!door)`：門在原版也是 `UserObject`，開關動作的 `]` 照角色／怪物的路徑處理（TWP§3.4）。門的動作實例改用「這次播放的開始時間」（`Renderer.DoorPlaybackStamp`），每次開或關各觸發一次；沒有播放中的動作（elapsed＝MaxValue）時不觸發。 | `ObjectRendering.cs` |
| 2a. 死亡統計 Reset 鈕 | `UserPlayStatsLayout` 讀選用的 `Reset_Button`（5330–5332）。按下時（sub_C0BA30）：清空新物品清單、用目前的等級／經驗%／金幣重建基準（基準清單不動，FU3§4.2）、關閉清單窗，之後停用 1000 ms（冷卻中只畫 BaseSurf）。不送 op 55／44（3.81 不處理）。原生面板與 WinForms 備援面板都有這顆鈕。 | `Recovery.cs`、`NativeUi/UserPlayStatsPanel.cs` |
| 2b. `UserObtainUI` 清單窗 | 新面板 `NativeUi/UserObtainPanel.cs`，用 `[UiPanel(151)]` 自動發現，讀 `UserObtainUI.xml`。清單不空時按 NewItemList 開窗，再按一次關閉；位置在統計窗右側，右側超過 800 就放左側；標題 `"** %s **"`＋3126；每列畫圖示（ItemIconSource）、名稱、差額；有捲軸（上／下鈕、滾輪、拖曳鈕位置）、關閉鈕、Esc、`IUiClosablePanel`。統計窗關閉或按 Reset 時一起關。資料是新的 `UserPlayStatsValues.Obtained`（id、差額、名稱、圖示，依第一次記入的順序）。 | `NativeUi/UserObtainPanel.cs`、`Recovery.cs` |
| 3a. S15 名稱備援 | `sub_C038D0`：基準沒有這個 id 時，用「名稱（去掉尾端數量）＋第 6 參數」查基準清單，旗標 0x80 成立時沿用那筆的基準數量。 | `Recovery.cs` |
| 3b. mapDesc 超出範圍 | `TextArchive.ReadDesc()` 讀 `desc-c.tbl`，用解密後的 SHA-256 pin `f06c94f4…b7df` 核對（本機 Text.pak 第 4201 筆，249,300 bytes，和 portable-delivery.md 記的一樣）。`RegionLabel` 遇到 mapDesc ≥ MapDesc-c.tbl 筆數時改用 desc 第 4266 筆；沒有表或該筆是空的時，仍顯示「地區N」。由 `LocationRegionLoader` 在執行期載入。 | `TextArchive.cs`、`LocationShare.cs` |
| 4. 座標聊天行 | 有 `string-c.tbl` 時，整行完全照 1784（`%s從 %s地區…`，填入寄件者與地名），連結涵蓋整行（`LocationShareProtocol.ChatLine`／`LinkText`）。沒有字串表時（CI、既有 `_integration` 檢查）仍用「[座標] 寄件者：」加編號占位字的格式。 | `PlayerChat.cs`、`LocationShare.cs` |
| 5. 名稱旁圖示 | 滑鼠指到時會畫出隊伍與主人圖示：隊長 surf 8264、隊員 2826（位置 +2）、主人 773（位置 +7）。主人圖示多加「地圖 ≠ 509」的條件。缺 surf 時不畫，原版遇到缺圖會直接結束程式。+181 沒有 surf，3.81 不會出現。`MonsterLevelOption` 預設值改成 3（台版地區碼），所以永遠不畫等級差外框，也不做選項 UI。 | `ObjectRendering.cs` |
| 測試 | 新檔 `Q2Followup2Tests.cs`（`[WorldModuleTest("q2-followup-2")]`），涵蓋：門的 `]`、圖示的 surf 與位置、隊長判斷、Reset 的基準與冷卻、名稱備援（有 0x80／沒有 0x80／第 6 參數不同）、清單順序、清單窗（位置、左右擺放、標題、列幾何、捲動、拖曳、關閉、Esc、被堆疊擠出、Reset 關窗、缺節點時報錯）、desc 4266（含 pin 拒絕、缺表、空值、表太短）、1784 整行與連結位移。 | `Q2Followup2Tests.cs` |
| 既有測試調整 | `Q2FollowupTests.cs` 有兩個斷言改成新格式（有字串表時整行是 1784、連結涵蓋整行）。這個檔不在原列的可改清單內，但行為改了，不改測試就會失敗，所以只改這兩行。 | `Q2FollowupTests.cs` |

本項取消（gap-matrix 13.2 第 8 點）：城主王冠。`Castle.cs` 沒有改。

## 怎麼驗證

- 建置：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`。用的是暫時 lock，沒有寫檔。本次改的檔案沒有編譯警告（只有既有的 `MagicUi.cs` 警告）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- desc-c.tbl 的 pin 用 `scratch/t698/desc_pin.py`（不進 git）在記憶體裡解密本機 Text.pak 算出，第 4266 筆就是 SS§4.1 說的「強化卷軸箱」。沒有寫出任何資源檔。
- 單元測試與本組離線測試：依本機限制**沒有在本機跑**，交給 PR 的 GitHub Actions，結果以 PR 上的 CI 為準。
- 沒有改 `rebuild-lock.json`、`docs/REPORT.md`、releases、封存。沒有啟動遊戲、伺服器或 DB。

## 未驗證／推測

- **推測**：
  - 「第 6 參數」＝S15 的祝福 byte，旗標 0x80 取同一個 byte（3.81 `S_AddItem` 封印時會 +128）。FU3§9 說這個參數沒追到封包欄位。
  - 名稱比對前先去掉尾端的「 (數量)」。
  - 清單窗 Y 和統計窗頂端對齊；名稱與數量兩個標籤以 ItemInfo_List 原點擺在同一行；數量欄寫差額。
  - 捲軸每格一列，拖曳鈕依比例擺放。
  - NewItemList 再按一次＝關閉（XML `Toggle="true"`）；Esc 關窗。
  - Reset 冷卻中只畫 BaseSurf。
  - 名稱旁圖示走 SurfList.xml 的 surf 目錄（原版 `sub_E538D0` 讀 `<id>.img`，SurfList 中有 file 的列 id＝file）。
- **沒做**：
  - 清單列的 CheckButton 選取：`ItemsListClick` 的原版動作沒查。
  - 清單列的 `Desc_Tooltip`（`Opacity="0"`）。
  - 清單窗的 WinForms 備援：只有原生 UI 有清單窗。
  - 隊員圖示的 `map<索引, surf>`（`sub_9552F0`）由誰填入還沒查到，隊員一律用 2826（FU5§1.3）。
  - 門以外的 `.s32` 地圖特效 `]` 不在本張範圍。
- **待驗證（實機，交 Codex／dot）**：
  - 死亡後 Reset 鈕：數值歸零、1000 ms 冷卻。
  - 清單窗：位置、圖示、名稱是否和原版一致。
  - 有 `]` 的門開關時，子特效的位置。r29 素材的門 JSON 不一定帶 `]`，可能看不到。
  - 隊員與主人圖示的真機外觀。本機沒有 `8264.img`，所以隊長圖示不會出現。
  - 收到座標分享時，聊天行是否只有 1784 那一行。

## 產出路徑

- `engine/source/ObjectRendering.cs`、`engine/source/Recovery.cs`、`engine/source/NativeUi/UserPlayStatsPanel.cs`、`engine/source/NativeUi/UserObtainPanel.cs`（新）、`engine/source/TextArchive.cs`、`engine/source/LocationShare.cs`、`engine/source/PlayerChat.cs`
- `engine/source/Q2Followup2Tests.cs`（新）、`engine/source/Q2FollowupTests.cs`（兩個斷言）
- 本報告 `docs/reports/issue-698.md`；分支 `task/698`

## 給下一棒的提示

- 清單窗的資料入口是 `UserPlayStatsValues.Obtained`（`WorldSession.ObtainedEntries`）。查到 `ItemsListClick` 的原版動作後，在 `UserObtainPanel.Mouse` 加上選取即可。
- 要讓 Reset 送封包時（3.81 不需要），入口在 `LoginConnection.ResetUserPlayStats`。
- 查到隊員圖示的 `map<索引, surf>` 之後，改 `NameLayout.HoverIcons` 的 `PartyMemberSurf` 退回邏輯，並傳入隊伍索引。
- `.s32` 地圖特效的 `]` 還沒做，位置在 `MapEffects.cs`，可以照門的做法呼叫 `ObserveOwnerFrames`。
