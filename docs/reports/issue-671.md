# Issue #671 — T278：R3-B 寵物：召喚備援與 `PetControl`

分支：`task/671`。依據：`docs/reference/gap-matrix.md` 第 12.5 節（R3-B）、WIN§1.3、§1.5。

## 1. 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/PetControlPanel.cs`（新） | 舊式寵物指令窗 `PetControl` 的原生面板 `PET_CONTROL`（`[UiPanel(35)]` 自動註冊，Window 層，不讀 XML）。兩顆鈕：第 0 鈕 surf 1159／886 在錨點 +(6,30)，第 1 鈕 surf 1156／885 在錨點 +(−31,20)。實作 `IUiClosablePanel`：視窗堆疊（#677）滿的時候只關本次開窗，不送封包。另有 `PetControlTracker`：原服每回一次 `anicom`／`moncom` 就開一次窗。 |
| `engine/source/SummonFallbackUi.cs`（新） | 原生 UI 關閉時的召喚暫行清單：讀原客戶端 `summonList.xml`（`SummonCatalog`，背景讀取），放在 WinForms「寵物」分頁，依等級由高到低分組；選定後呼叫同一個 `LoginConnection.SendSummonChoice`（server＝C125 `D(自己) S(action)`）。收到新的 S39 `summonlist` 時自動切到寵物分頁（`ShowPlayerService("寵物")`）。 |
| `engine/source/PetUi.cs` | 掛上 `PetControlHost`（第 0 鈕 → 既有 `refresh`＝C103；第 1 鈕 → 既有 `attackchr`＝C125，只對寵物）；原來那一行「召喚選擇窗需開啟原生 UI」的提示改成暫行清單；本機召喚入口（技能 51）兩種模式都可以用。WinForms 寵物分頁保留。 |
| `engine/source/PetControlTests.cs`（新） | 3 組離線測試：`pet-control`、`pet-control-tracker`、`summon-fallback`（`[WorldModuleTest]` 自動發現）。 |

沒有改 `Pets.cs`、`SummonPanel.cs`、`UiPanelRegistry.cs`、`UiClientBridge.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 2. 靜態分析證據（TW1608262200.bin，SHA-256 `E12949CF…09641A2`，ImageBase 0x400000，FO＝VA−0x401000+0x400）

工具：pefile＋Capstone（腳本在 `scratch/`，沒有提交）。

**已驗證**
- 建構子 `sub_F3A0C0`（FO `0xB394C0`），由 `sub_8C7C80` 在 VA `0x8C8E9B` 呼叫；實例存在 `dword_1656908`。按鈕容器 `sub_8156B0(2, 0x23, −1)`＝2 顆鈕，容器座標 +0x14／+0x18 建構時是 0。
- 第 0 鈕：`sub_8FA710(0x487=1159, 0x376=886, (6,30), 回呼 0xF3AC80, idx 0)`；第 1 鈕：`sub_8FA710(0x484=1156, 0x375=885, (−31,20), idx 1)`。codepage 932（`dword_13CE508`）時第 1 鈕沒有回呼，TW（950）有。第一個 surf 交給基底按鈕 `sub_8F9EB0`，第二個存在子類別 +0x4C。
- 回呼 `sub_F3AC80`：
  - idx 0：開寵物背包窗（`dword_1656930`），複製 15 列寵物資訊，送 `sub_9FBC00("cd", 160, 寵物 id)`＝TW16 C_CHECK_INVENTORY。
  - idx 1：寵物種類（+0x10）≠ 1 時送 `sub_9FBC00("cdss", 227, 寵物 id, "attackchr", "")`＝TW16 C_HACTION，並設選目標旗標（+0x20）。
- 開窗 `sub_F3ABB0`（vtable `0x11E8E8C` 第 8 格）：種類 == 1 時第 1 鈕停用。
- S39 處理器（VA `0x585BD8`–`0x585D6F`）：html 是 `moncom` 時種類設 1，否則設 0（`sub_F3B6E0`）；寵物 id＝S39 的 objid（`sub_F3B6A0`），之後開 `PetControl`。
- 3.81 對應：op 227 對應 C125 C_NPCAction（followups-3），也就是既有的 `PetPackets.TargetCommand`。op 160 對應 C99 C_Check，但 3.81 伺服器不回任何封包（packetbox-windows §2），所以背包鈕改送 3.81 會回背包的 **C103 C_PetMenu**（既有 `refresh`）。

**推測（程式與報告都有標明）**
- 平常畫第一個 surf（1159／1156），按住時畫第二個（886／885）。
- 容器原點：原版是誰寫入 +0x14／+0x18 沒有查到，這裡用世界區中心 (400,300) 當錨點。
- 停用的第 1 鈕照樣畫，但不能點。

**待驗證／不做**
- 淡入淡出：`sub_F3B100` 一帶有以 alpha 0x1F 遞減的程式碼，沒有實作。
- 按鈕提示文字、Esc 的行為。
- 原生寵物背包窗（`dword_1656930`）不在本組範圍。原生模式下按背包鈕會送 C103，背包內容仍然只顯示在 WinForms 寵物分頁（開發工具 →「服務」）。
- 召喚清單的怪物名稱（`desc` 字串表）待查證，暫行清單只顯示召喚編號、需求等級和 `minCha`。

## 3. 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock`（暫用 lock，只在本機）→ `rebuild.py --build --configfile .github/nuget.ci.config`（已合併 main，含 R3-I 視窗堆疊 #677） | **PASS dev**，product、launcher 的 restore／build 都退出 0，productDll `719d5ddb…fad8`；之後 `git checkout` 還原 lock，**PR 沒有改 `rebuild-lock.json`** |
| `engine/tools/check-no-game-assets.py` | pass（4015 檔，0 錯誤） |
| 新增的 3 組離線測試、既有單元測試 | 依本機限制**沒有在本機執行**，交給 PR 的 GitHub Actions CI |

新測試涵蓋：
- 面板只註冊一次、寫死的 surf 與座標、只在世界中才顯示、版面矩形。
- 平常畫底圖、按住換圖；在不同鈕上按下和放開不會觸發。
- 背包／目標鈕的轉呼叫；按下後關窗，同一個 revision 不會再出現。
- moncom 時第 1 鈕停用；宿主不能送時兩顆鈕都沒有反應；窗外的點擊會穿透；缺 surf 時停用整個面板。
- 真的 S39 anicom／moncom 送進 `WorldSession` 後會開窗、每次回覆換一個 revision；他人寵物不開窗；離開世界後重新初始化。
- 兩顆鈕送出的封包：C125 `attackchr` 與 C103。
- 暫行清單：等級分組與排序、等級不足或沒收到等級時不能選。
- 自動切分頁只發生一次，原生模式不切。
- 選定後送出的 C125 `D(自己) S("263")` 和原生窗相同、只送一次，舊清單不能重送。

## 4. 未驗證什麼
- CI 結果（push 後看 PR）。
- 實機都還沒驗證：
  - 原生模式：點自己的寵物 → 原服回 anicom → `PetControl` 出現在畫面上的位置與外觀；按鈕圖與按下圖是否正確；
  - 原生 UI 關閉時：S39 `summonlist` → 寵物分頁自動出現暫行清單 → 選怪 → 召喚成功。這兩項都要真機。
- 原版第 0 鈕的 C_CHECK_INVENTORY 在 3.81 沒有效果，改送 C103 是**引擎決定**，不是原版行為。

## 5. 產出路徑
- `engine/source/NativeUi/PetControlPanel.cs`、`engine/source/SummonFallbackUi.cs`、`engine/source/PetUi.cs`、`engine/source/PetControlTests.cs`
- `docs/reports/issue-671.md`（本檔）

## 6. 給下一棒的提示
- 真機驗收（Codex／dot，P24Lab 測試角色）：
  - (a) `--native-ui`：對自己的寵物送「查詢」或點寵物，確認 `PetControl` 兩顆鈕出現；按第 1 鈕之後點目標，確認送出 C223。
  - (b) 原生 UI 關閉：用召喚戒指觸發 S39 `summonlist`，確認寵物分頁自動切出、清單可選，並且只送一次 C125。
- 後續可以查的靜態項目：
  - `PetControl` 容器座標（+0x14／+0x18）是誰寫入的，猜測是寵物的畫面座標或滑鼠位置，查到後替換 `PetControlHost` 的錨點；
  - 兩個 surf 實際的用途；
  - 淡入淡出的時間。
- 原生寵物背包窗（`dword_1656930`，建構子未查）可以另開任務；做完之後，第 0 鈕就不用再依賴 WinForms 分頁顯示背包。
