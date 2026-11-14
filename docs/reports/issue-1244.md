# Issue #1244 f1211-spell-class56：龍騎士（5）、幻術士（6）魔法窗空白

階段 code，claude-local，第 1 次嘗試。分支 `task/1244`。

## 結論

- **3.81 原版沒有龍騎士、幻術士的魔法窗規則（已驗證）。** 3.81 客戶端只認得職業 0–4：魔法窗的格子表只有 4 種（王族、妖精、黑妖、一般），**職業 0／2／4 以外一律用「一般」表**（技能 id 1–80）；技能圖示表只到 id 180。所以龍騎士的 181／182、幻術士的 201／202 在 3.81 魔法窗**沒有格子**，原版也顯示不出來。
- 本 PR 只做有證據的部分：`ClassOf(5)`、`ClassOf(6)` 從 null 改成 `General`（照 3.81 的退回分支）。之後龍騎、幻術開魔法窗會畫一般等級條，學了 id 1–80 的技能也會列出；**181–、201– 的職業技能仍然不在任何頁上**。
- 「181／182、201／202 要放在哪一頁」是原版沒有的規則，屬於產品決策。依任務指示**不自己編**，標 `needs:claude`（選項見最後一節）。所以 SM20 的「魔法窗列出 181／182」這一項本 PR **沒有達成**。

## 做了什麼

### 1. 靜態分析（3.81 OEP 快照）

- 檔案：`3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe`，SHA-256 `4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9`（FU12§0.1 的同一份）。**快照 FO＝VA − `0x400000`**；原 `LiTo.bin` 有 Themida 殼，沒有可用的 FO（同 FU12、FU17）。
- 工具：IDA 7.7（idat 批次，沿用 `docs/reference/rea-scripts/t896/` 的 `ida_t896.py`／`run_t896.ps1`；IDB 從 `scratch/wt/task-896/scratch/t896/ida/` 複製到本 worktree 的 `scratch/t1244/ida/` 再開，沒動原 IDB）＋ Capstone。runner 用 `subst W:` 取 ASCII 路徑，結束時已解除（Q: 被另一個任務佔用，沒碰）。

| 項目 | VA（快照 FO＝VA−0x400000） | 內容 |
|---|---|---|
| S_ADD_SPELL 處理 | 分派 `0x4B29F7` → 索引表 `0x4B3470[0x1E]`＝22 → 跳躍表 `0x4B3208[22]`＝`0x4B2AD3` → `sub_4AEE70` | 逐位元組讀遮罩，第 k 列第 i 位＝技能索引 `8k+i`（id−1）；名稱取 `dword_647E24[索引]`（超出 `dword_647E20` 就用「Unknown Spell」、圖示 91），圖示取 `word_508648[索引]`，交給 `sub_4C9220(索引, 圖示, 名稱)`（`this`＝`dword_834888` 魔法清單物件） |
| 放進格子 | `sub_4C9220` → `sub_4CD050` | 依職業 `byte_75F694` 決定格數：2→99、0→56、4→40、其他→80；在格子表 `off_635B80` 裡找**圖示相同**的格子，建立項目（`sub_4C8DE0`）。**格子是依圖示對應，不是依 id** |
| 選格子表 | `sub_4928C0`（進入世界），職業 byte 讀自角色資料 +45（`0x492AD4` 一帶寫入 `byte_75F694`） | `==2` → `0x8345E8`（妖精）；`==4` → `0x834548`（黑妖）；`==0` → `0x834774`（王族）；**其他值** → `0x834408`（一般）。`==1`（騎士）另外把一般表第 8、9、10、13、14 格的圖示換成 1624、1881、1887、2349、2343（＝騎士技能 87、89、88、90、91 的圖示）；其他職業把這 5 格寫回 388、389、390、755、651（＝原本 id 9、10、11、14、15 的圖示） |
| 填格子表 | `sub_43F6C0`（`0x43F747`–`0x43F873`） | 從圖示表 `word_508648` 複製（見下表） |
| 魔法等級上限 | `sub_4C8BD0` | 0：等級/10（≤2）；1：等級≥50 才 1；2：等級/8（≤6）；3：等級/4（≤10）；4：等級/12（≤2）；**`default`（5、6…）：0** |
| 其他職業分支 | `sub_4C8CD0` | 只有 0／2／3／4 的 case，其餘走 default |

各格子表的內容（由 `sub_43F6C0` 填，技能以 id 表示）：

| 表 | 職業 | 格數 | 內容（依格子順序） |
|---|---|---|---|
| `0x834408` | 一般（1、3 與其他） | 80 | id 1–80（騎士再換 5 格，見上） |
| `0x834774` | 王族 0 | 56 | id 1–48，接 id 113–120（王族技能） |
| `0x834548` | 黑妖 4 | 40 | 格 0–15＝id 1–16；格 16–19＝97–100、格 20＝109；格 24–27＝101–104、格 28＝110；格 32–35＝105–108、格 36＝111；格 31＝id 26 的圖示 |
| `0x8345E8` | 妖精 2 | 99 | id 1–48，接 id 129–179 |

圖示表 `word_508648` 的有效範圍：妖精表複製到 `0x5087AE`（索引 179，不含），索引 176–179 是 0，**之後是別的資料**（索引 180＝46、181＝0、200＝51、201＝0……）。所以 3.81 讀到 181／182／201／202 時拿到的是無關的值，龍騎、幻術的格子表（一般表）裡沒有這些圖示，**不會出現**。

→ 3.81 對職業 5、6 沒有任何專屬處理：格子表、魔法等級上限都走「其他」分支。這是有證據的「原版沒有規則」，不是查不到。

### 2. 客戶端資源（PakViewer CLI，唯讀）

用 `pakviewer-cli pak list` 列出 `D:\lineage開發\0-客戶端` 全部 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon.idx`、`LTsprite.idx`、`LTsurf.idx`、`LTtext.idx`、`LTtile.idx`（清單在 `scratch/t1244/pv/`，不進 git）。和魔法窗有關的只有：

- `Tile.idx` `uimagicitem.xml`（9,999 byte）：`SPELL_WINDOW` 只有 PRINCE／一般／ELF／DARKELF 四組等級鈕（#562、FU7§2），沒有龍騎、幻術組；而且 FU12§2.1 已驗證 3.81 不讀這個 XML。
- `Text.idx` `spelldesc-c.tbl`、`SpellExDesc-c.tbl`、`spelldur.tbl`；`Sprite.idx` `SpellIcon.idx`、`list.ico`；`Tile.idx`／`LTtile.idx` `list.til`；`LTtext.idx` `LTSpellDesc-c.tbl`、`LTspelldur.tbl`、`LTlist.spr`；`LTicon.idx` `LTlist.ico`：技能名稱、說明、圖示清單，**不是版面**。
- `Text.idx` `dragonKnight-c.tbl`、`illusionist-c.tbl`、`lvupillusion-c.tbl`：職業說明文字，不是魔法窗版面。

沒有找到任何龍騎、幻術的魔法窗分頁或格子資源。本報告**沒有宣稱缺圖**：一般等級條的圖（2065–2074）客戶端都有。

### 3. 程式

`engine/source/NativeUi/SpellWindowPanel.cs`：

- `ClassOf`：`5`、`6` → `LegacySpellClass.General`（照 `sub_4928C0` 的「其他」分支）；負值（職業未知）維持 null；註解附上述 VA。
- `native-spell-window` 檢查（既有 `[WorldModuleTest]`，同檔）：
  - 原本斷言 `ClassOf(5)==null` 改掉；新增 `ClassOf(5)`、`ClassOf(6)` 都是 General。
  - 龍騎（class 5）學 181／182：類別是 General、等級條有畫、兩個技能都留在資料裡；`PageOf(181)`＝`PageOf(182)`＝22、`PageOf(201)`＝`PageOf(202)`＝25，都超過一般的 10 頁 → 不在任何頁（照 3.81，註明 needs:claude）。
  - 幻術（class 6）學 id 1 與 201：第 1 頁只列 id 1。

沒有新增 `[CheckGroup]`：這張只改既有檢查，沒有新檢查群組。沒改 `Program.cs`、`DemoChecks.csproj`、註冊表、`rebuild-lock.json`。

## 怎麼驗證

- 已驗證：上表各 VA 的反組譯／反編譯（IDA 輸出在 `scratch/t1244/ida/a2`–`a7`，不進 git）。
- 已驗證：本機 `dotnet build source/OfflineClient.csproj -c Release`：0 錯誤、4 個既有警告（`DOTNET_CLI_HOME` 設在 scratch，並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`）。
- 依本機限制沒有執行 DemoChecks／模組測試；檢查結果以 PR 的 CI 為準。

## 未驗證什麼

- **待驗證（交 `f1211-retest`）**：
  - SM20：龍騎、幻術按 Ctrl+S，應出現一般等級條（2065 起）；只學 181／182、201／202 時清單仍然是空的（照 3.81，**不算修好**，等 needs:claude 裁定）。
  - SM48：龍騎、幻術施放基本技能。本 PR 沒改施法路徑；目前魔法窗選不到 181–／201–，能不能施放要看其他入口（WinForms 魔法分頁、快捷鍵）。
- 推測：3.81 客戶端本身大概不支援職業 5、6（相關分支全部是 default）；沒有實機確認。
- 未查：3.81 魔法窗每頁幾格、格子在窗內的座標、等級條如何對應到格子（只查到格子表與格數）。

## 需要 Claude 裁定（needs:claude）

3.81 沒有規則，下列方案都是「原版沒有的延伸」，請擇一：

- **A（建議）**：照較新原版 TW16 的做法（FU7§2.1：舊式 `SpellWindow` 有一顆獨立的「職業技能」鈕，`dword_11DE910` 職業 5 範圍 0–15、職業 6 範圍 0–20），龍騎、幻術多一頁職業技能，放 181–195／201–220。需要決定等級條上用哪一段當入口（3.81 的一般條只有 10 段、沒有第 11 段的圖）。
- **B**：維持 3.81（本 PR 現狀），龍騎、幻術的職業技能只在 WinForms 魔法分頁出現，SM20 這項改判為「照原版」。
- **C**：一般頁之後直接接列（第 11 頁起），不畫等級圖。

## 給下一棒的提示

- **順帶發現（另開 code 任務較合適）**：3.81 王族、黑妖、妖精的格子表已經查清楚（上表）。引擎現在的 `PageOf`＝`(id−1)/8` 對這三職業是錯的：例如王族第 7 頁原版是 113–120（引擎給 49–56），黑妖第 3–5 頁原版是黑妖技能（引擎給 17–40），騎士 87–91 原版放在一般表第 2 頁的第 1、2、3、6、7 格（引擎放第 11、12 列，沒有鈕）。這解答了 gap-matrix Q5-2／M8「職業技能在哪個鈕」的大部分。要照做還需要查 3.81 每頁格數與格子座標。
- 3.81 S_ADD_SPELL 處理、格子表、職業分支的 VA 都在上表，查 3.81 魔法相關功能可以直接從 `sub_4AEE70`、`sub_4928C0`、`sub_43F6C0`、`sub_4CD050` 開始。
- IDA 用法：`scratch/t1244/run_t896.ps1 -Root scratch\t1244 -List <q>.txt -Out <dir> -Drive W:`（Q: 常被別的任務佔用）。

## 產出路徑

- `engine/source/NativeUi/SpellWindowPanel.cs`
- `docs/reports/issue-1244.md`（本檔）
- 不進 git：`scratch/t1244/`（IDA 輸出、PakViewer 清單、分析腳本）
