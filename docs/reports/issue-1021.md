# Issue #1021 — T442：R13-A 本機選項頁可操作（gap-matrix 1:144）

依據：`docs/reference/gap-matrix.md` 15.15.5／15.15.6（T441／#1012，PR #1019）；裁定 #1020（①②③ 都照原版做）。

## 做了什麼

### ① 本機頁模式（`NpcInteraction.cs`、`LocalOptionPage.cs`、`NativeUi/UiClientBridge.cs` 的 `ToggleMenuLocalPage`）
- 新增 `WorldSession.QueryLocal` 與 `ShowLocalOptionPage(key)`。選單鈕 5／6、Ctrl+H／V／C（含 `--classic-ui` 的 NPC 分頁三顆按鈕，都走 `ToggleMenuLocalPage`）開的 help、option1、option2／option2p 改用本機頁模式顯示。
- 本機頁上的 `<a link>`、`<img link>`：`OpenQueryLink` 換到同名的本機頁，換過去的頁仍是本機頁模式。**不送封包**。目標頁沒有模板的 link 照舊停用（例如 help 頁的 `_help*`）。
- 本機頁的物件 id 是 0、沒有參數，`QueryActionPacket` 組不出 C125。`QueryOption` 碰到本機頁時，只走 `LocalOptionAction`，不會往下送 C125。
- 模式切換：玩家查詢結果（`ShowQueryPage`）、伺服器主動頁（`ShowServerPage`）、關頁（`CloseQueryPage`）都會清掉本機頁模式。伺服器送來的空頁（S39 html=""）只關伺服器頁，不關本機頁（推測）。
- 選單的「同一頁再按一次就關」多檢查 `QueryLocal`。

### ② 本機頁上的客戶端動作（`LocalOptionPage.cs`）
做完重畫目前這頁（頁版本 +1，`<var #1002–1029>` 重新代入值）：

| 動作 | 引擎執行 | 原版（TW16，本任務 IDA 查證） |
|---|---|---|
| `accepttrade` | `ToggleAcceptTrade`（沿用） | `HyperActAcceptTrade` vtable `0x11A98F0` 槽 1 → `sub_581790`：`byte_136E99D` 取反，重開 `option13` |
| `getall` | `WorldSession.TogglePickupAll` | `HyperActGetAll` `0x11A94A0` → `sub_582E30`：`byte_135C1E4` 取反，重開 `option13` |
| `bubbleon` | `HeadBubbleOptions.BubbleFrames` 取反 | `HyperActBubble` `0x11A94AC` → `sub_581B90`：`byte_13AAF28` 取反，重開 `option12` |
| `showMargetMsg` | `HeadBubbleOptions.ShopMessages` 取反（存 option-ui.txt） | `HyperActShowMarketMsg` `0x11A956C` → `sub_584BE0`：`byte_136E99F` 取反，重開 `option12` |
| `showhpmp` | HP/MP 視窗 `ShowCharacterHpMp` 取反；沒有原生 HP/MP 視窗（`--classic-ui`）時停用 | `HyperActShowHPMP` `0x11A9518` → `sub_584B50`：`byte_138BE7C` 取反，重開 `option11` |
| `showfloathpmp` | 只重畫 | `HyperActShowFloatHPMP` `0x11A9524` → `sub_584B10`：**不改任何旗標**，只重開 `option11` |
| `dowhisper` | `SetChatChannel(Whisper, !目前值)`（C26 type 2） | `HyperActWhisper` `0x11A94D0` → `sub_585440` → `sub_4F9A90(!byte_135C1E6)` → `sub_F4A0A0`（就是 `/whisper on|off`：`ccc` 184,2），重開 `option12` |
| `dotradechat` | `SetChatChannel(Trade, !目前值)`（C26 type 6） | `HyperActDoTradeChat` `0x11A94C4` → `sub_5828D0` → `sub_4F9A60` → `sub_F4A7B0`（`/tradechat on|off`：184,6），重開 `option12` |

另外沿用引擎已有、已查證的客戶端 HyperAct（伺服器頁上本來就這樣分派，不送 C125），做完不重畫（重畫會讓頁內名稱框失效）：
- `optionb`（option2 連過去）：`addbuddy`／`delbuddy`／`buddy`（R10-C／R11-C，`BuddyHyperAction`）。
- `optionplp`（option2p 連過去）：`surrender`／`cease`／`pledgefocusadd`（R7-D，`HtmlClientAction`）。

**停用**（照原樣顯示、不送 C125；原版怎麼處理沒查到，或引擎沒有對應設定，**推測**）：`dochat`（`sub_582750` → `sub_4F9910` → `sub_F45680`，`unk_1656948` 沒追完）、`randomParty`（`sub_583E40`，`byte_13CE519`）、`cPartyReject`（`sub_581FB0`，`byte_13CE51A`）、`telcon`、`infracon`、`chatstate`、`broadcasttopledge`；option11 其他顯示選項（`dropItemView`、`npcNameView`、`alpha`、`volume`…）；option14 的視窗模式；`optionpl`／`optionpt`／`optiona`／`optionap` 的血盟、組隊、同盟動作。有停用動作的頁，說明欄會加一行「本機選項頁：原版處理方式沒有查到的選項維持停用，不送出（推測）。」

### ③ HP/MP 值（`UiClientBridge.cs` 的 `ApplyNativeUiStart` 一行）
- 成功時呼叫 `LocalOptionPage.AttachHpMp(()=>OptionPanel.HpMpWindow(root))`，裡面設定 `HyperTextOptionVars.HpMp=()=>panel()?.Settings`，和 15.15.6 ③ 的寫法相同。同一個來源也給 `showhpmp` 切換用，所以寫成一行呼叫，沒有另外加第二行。option11 的 `#1023`／`#1024` 在產品裡會有值。

## IDA 查證（唯讀）
- IDB：從 `scratch/wt/task-629/scratch/rea/ida/tw16.idb` 複製到本 worktree 的 `scratch/t1021/ida/`（不進 git）。用 IDA 7.7 `idat -A` 批次跑，subst `T:`，結束時解除。
- 名稱 → 建構子：沿用 T268 的 `hyper.json`（`sub_BB5180` 反編譯；`dowhisper`＝`sub_B748F0`、`dotradechat`＝`sub_B71510`）。建構子只是包裝函式，所以改用 RTTI：掃全部 `.?AVHyperAct*` 類別 → TypeDescriptor → COL → vtable，**槽 1 是執行函式**。已知的 4 個（accepttrade `0x581790`、getall `0x582E30`、bubbleon `0x581B90`、showMargetMsg `0x584BE0`）和 FU7／world-objects／FU16 的位址相同，showhpmp `0x584B50` 和 ui-layout.md 相同，所以這個對應可以相信。
- 再反編譯 `sub_585440`、`sub_5828D0`、`sub_582750`、`sub_584B10`、`sub_4F9A90`、`sub_4F9A60`、`sub_4F9910`、`sub_F4A0A0`、`sub_F4A7B0`，結果寫在上表。FU7§1.4 的「選項頁切換動作」表（`sub_585440 → sub_F4A0A0`、`sub_5828D0 → sub_F4A7B0`）現在連上了動作名稱。
- 腳本：`scratch/t1021/scripts/ida_rtti.py`（RTTI → vtable）、`ida_vt.py`、`ida_dis.py`（沿用 #431）。輸出在 `scratch/t1021/ida/o1–o5/`。都不進 git。
- 可到達的本機頁與頁上的動作：唯讀讀 `releases/r29/assets/dialogs/templates.json`（引擎實際用的模板），從 help／option1／option2／option2p 沿 link 走，共 128 個頁名（`scratch/t1021/scripts/reach.py`）。本任務沒有宣稱任何資源「沒有」，所以沒有另外跑 PakViewer。

## 怎麼驗證
- `dotnet build engine/source/OfflineClient.csproj -c Release`（本機，輸出到 `scratch/t1021/build`）：0 個錯誤。3 個警告（`HtmlActionTests.cs:108`、`MagicUi.cs:20`）是原本就有的。
- `py -3 -I engine/tools/check-no-game-assets.py`：`pass`（4627 個追蹤檔，0 個錯誤）。
- 新的離線測試 `engine/source/LocalOptionPageTests.cs`（`[WorldModuleTest("local-option-page")]`，自動發現）。內容：動作表與 `Prepare`；link／img link 本機換頁、不送封包、舊版本失效；getall／accepttrade／bubbleon 執行後重畫、值會更新；randomParty／dochat／cPartyReject／alliancelist 停用；dowhisper／dotradechat 送 C26 type 2／6；showhpmp 有視窗才可用、showfloathpmp 只重畫；`AttachHpMp` 接上 `HyperTextOptionVars.HpMp`；addbuddy 在本機頁開 588 名稱框；查詢結果頁、伺服器頁、空頁、關頁的模式切換；伺服器頁上的 getall 照舊送 C125。測試只用自己寫的合成模板，不讀客戶端素材。
- 依本機執行限制，**本機沒有執行任何測試**。單元測試與本組測試交給 PR #1022 的 GitHub Actions CI。
- CI 結果：第一次跑時 `local-option-page` 有 2 項失敗（82 項檢查）：`Prepare` 用注入的「一律可用」判定時，會把不在本組的動作（dochat）也啟用。已改成「不是本組的動作一律停用」（commit `308b70fa`）。重跑後 `dotnet`、`map-continuity`（含全部 `[WorldModuleTest]`）、`npc-offline` 都通過。

## 未驗證／推測
- **待驗證（真機，smoke-m7 SM16 附加項）**：鈕 6／Ctrl+V 開 option1 → 點環境／社群／指令能換頁，左下圖示回 option1；option11 的 HP/MP 值；option12 的泡泡話框、個人商店名稱、密語、買賣；option13 的交易、撿取全部，點了會切換並重畫這頁。
- 推測：停用動作的原版處理方式；伺服器的空頁不關本機頁；本機頁顯示時，WinForms 備援的標題是「伺服器頁」（`NpcUi.cs` 依 `QueryActionable` 判斷，不在可改範圍），舊 HUD 的服務追蹤器也會把它當成伺服器頁去開 NPC 分頁（選單本來就會開這一頁）。
- 原版重開的是固定的頁（option11／12／13／optiong）。引擎照任務規定重畫目前這頁，所以在 `optiong`（選單到不了）以外的頁，結果相同。
- `dotradechat`：原版在 `unk_165699C` 成立時不送 184,6，引擎沿用 `/tradechat` 的做法，一律送（和 R6-E 相同）。

## 產出
- `engine/source/LocalOptionPage.cs`（新）
- `engine/source/LocalOptionPageTests.cs`（新）
- `engine/source/NpcInteraction.cs`（只改本機頁模式）
- `engine/source/NativeUi/UiClientBridge.cs`（只改 `ToggleMenuLocalPage` 與 `ApplyNativeUiStart` 的 HpMp 一行）
- `docs/reports/issue-1021.md`（本檔）

## 給下一棒的提示
- 合併後重跑 `gap_count_r10.py`：1:144 應該可以改成「已有」（預期 235／55），M7 就符合 15.15.7 的結案條件。
- `dochat`（1004）：要再追 `sub_F45680` 與 `unk_1656948`（3.81 是否有對應的 C26 type）。`randomParty`／`cPartyReject`：引擎沒有組隊自動分配、拒絕聊天組隊的設定。
- option14 的視窗模式、option11 其他選項（`dropItemView`、`npcNameView` 等，引擎有 `IOptionWindowHost` 對應的設定），如果要接，處理函式位址都在 `scratch/t1021/ida/o3/report.json` 的 RTTI 表（`HyperAct*` 的槽 1）。
- 真機：smoke-m7 SM16 附加項交給 P24Lab 測試角色執行。
