# Issue #1047 — T462 去伺服器依賴：skills.json 與 magic/catalog.json（b15-deserver-skills-magic）

2026-11-13，runner：claude-local，分支 `task/1047`。依 `docs/reference/asset-recipes/rule-tables.md`（T446／#1031）第 1、2 節。

## 結論

兩個讀取點都不再碰伺服器 SQL 產生的檔：

| 原讀取點 | 改成 |
|---|---|
| `NpcUi.cs:76` 讀 `dialogs/skills.json` → `SkillCatalog` | `SkillCatalog.FromClient()`：名稱取客戶端 `Text.pak` 的 `spelldesc-c.tbl`；等級用原版算式 `((id−1)>>3)+1`，不用表 |
| `MagicSpells.cs` 讀 `assets/magic/catalog.json` | 不讀任何檔：名稱、MP 取 `spelldesc-c.tbl`；動作看 `SkillCast.Wire`；冷卻用原版預設 `SpellListCooldowns.DefaultDelayMs`（10 ms）；「只能對自己」取 `SpellExDesc-c.tbl` 的「對象：施法者」 |

做法是 T446 結論的「執行期直讀」，跟 T453 報告與契約 3.6 一致：**沒有**把這兩個檔加進 `text` recipe，準備器也不必再產生它們。T446 對這兩張表沒有判 blocked 的部分。客戶端表讀不到（CI 沒有客戶端、版本 pin 不符）時照「已知限制」的暫行做法：學魔法清單明確提示不可用、全部停用，不放替代資料。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/ClientSpellTable.cs`（新） | 讀 `spelldesc-c.tbl`、`SpellExDesc-c.tbl`（在記憶體解碼、核 pin），提供名稱、MP、「只能對自己」與等級算式；延遲載入 `Current`／`LoadError`，測試用 `Use()` 替換 |
| `engine/source/MagicSpells.cs` | 拿掉讀 `magic/catalog.json` 的整段；`Get(id)` 一律照上表組出；`MagicSpell` 拿掉沒人讀的 `Effect` 欄 |
| `engine/source/NpcServices.cs` | `SkillCatalog` 改成從 `ClientSpellTable` 建（拿掉 JSON 建構式與 `System.Text.Json`）；加 `FromClient()`；S41 沒有技能目錄時改顯示 `SkillCatalogUnavailableNotice`，提示文字改成「依原版技能等級算式」 |
| `engine/source/NpcUi.cs` | 第 76 行改用 `SkillCatalog.FromClient()` |
| `engine/_integration/dev/demo-tests/ClientSpellChecks.cs`（新） | `[CheckGroup("client-spells")]`＋`[CheckGroup("combat-admission")]`（後者 CI 有跑） |
| `engine/_integration/dev/demo-tests/CombatLoopChecks.cs` | 學魔法流程改用合成的 `spelldesc-c.tbl`，不再寫假的 `skills.json` |
| `engine/_integration/dev/demo-tests/CombatAdmissionChecks.cs` | 拿掉寫假 `magic/catalog.json` 的那段（產品已經不讀了） |

沒有改 `Program.cs`、`DemoChecks.csproj`、任何註冊表、`build.yml`、`rebuild-lock.json`、`docs/REPORT.md`。沒有讀 SQL、JAR、DB。程式裡沒有任何技能名稱、MP 或等級的常數表。

### 規則細節

- **`spelldesc-c.tbl`**（解碼後 SHA-256 `387c4a9d…3e30ab9`）：CP950 嚴格解碼；第一行是筆數；之後每行 `<索引> <名稱> (<MP>/<HP>[/<材料數>])`，技能 id＝索引＋1。名稱取最後一個 `(` 之前的文字（去掉結尾空白）。真表有 4 行括號前沒有空白（例如索引 50「召喚術(30/30/3)」），規則照樣適用；括號內第一欄不是數字（索引 107「會心一擊 (HP-1/MP-1)」）時 MP 未知。索引超出宣告筆數、索引重複、編碼錯誤都拒絕。
- **`SpellExDesc-c.tbl`**（`2fcb23e0…8a5feeb2`）：每行以 `^` 分段、去掉 `\fX` 色碼，取第一個「對象：」段；值**剛好**是「施法者」才算只能對自己（「施法者、隊伍成員」不算）。這個檔有無效的 Big5 字（Python 的 cp950 解不開），所以用取代式解碼，只看「對象：」段。說明表讀不到時名稱仍可用，「只能對自己」一律當不知道（給 `buff`）。
- **對象的優先順序**：Wire 是 `DHH`／`DHHS`（攻擊）、`HH`、`HD`、`S` 時照 Wire；其他才看「對象：施法者」→ `none`，否則 `buff`。
- **pin**：兩個表都核解碼後 SHA-256，和 `ReadDesc`／`ObsceneFilter` 的做法一樣。不是支援的客戶端版本就當成讀不到。
- **讀不到時**：`MagicSpells` 仍然讓已學會的技能可以施放（名稱「原服技能 N」、MP 0，技能窗顯示空白）；`SkillCatalog` 是 null，S41 學魔法清單顯示「學習魔法暫不可用：讀不到客戶端技能表 spelldesc-c.tbl（原因）」，每一列都不能選。

### 行為差異（和以前讀 r29 基線檔比）

照 T446 1.3、2.3：

1. 6 個技能名稱改成客戶端譯名（30、111、115、119、120、213）。
2. 36 個基線 `none` 佔位 id 從「`none`、不能買」變成「技能 N（缺少名稱與價格依據）、不能買」（既有的備援文字）。
3. `magic/catalog.json` 原本只有技能 1、3、4、8；現在 1–220 都有名稱、MP 與對象。技能 3 的提示仍是「自己」；其他「對象：施法者」而且 Wire 是一般 `D` 的技能也變成「自己」（以前是「自己／指定對象」；52 筆裡有幾筆是書籤等特殊 Wire，照 Wire 不變）。
4. 沒有 SpellList 表時的冷卻備援從 0 變成 10 ms（基線檔的四筆本來就是 10，`CastCadenceChecks` 的 1320 ms 不變）。

## 怎麼驗證

### 已驗證

- **來源表**：用 PakViewer CLI（`pak extract`，唯讀）從 `D:\lineage開發\0-客戶端\Text.idx` 解出兩個表到 `scratch/issue-1047/`（不進 git），SHA-256 和 T446 記錄的一樣。用 Python 確認：`spelldesc-c.tbl` 是 CRLF、183 行、第一行 220；有 5 行不符合「名稱 (數字/數字)」的嚴格格式（上面提到的 4 行沒有空白與索引 107）；`SpellExDesc-c.tbl` 有 177 行有「對象：」，其中剛好是「施法者」的 52 行（和 T446 的數字相同）。
- **本機編譯**：`python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <主專案>/engine/NuGet.Config --output scratch/issue-1047/build` → `PASS dev`（lock 為 temporary，未寫回）。`dotnet build` `DemoChecks.csproj`、`SkillCastChecks.csproj`、`NpcChecks.csproj`（Release）→ 都是 0 個錯誤。設定了 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`。**本機沒有執行任何編譯出的程式。**
- **`engine/tools/check-no-game-assets.py`**：pass（4682 個追蹤檔，0 錯誤）。
- **CI**（PR #1078，commit 第一版）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。`Combat admission contracts` 步驟（`combat-admission` 群組，含新的 `ClientSpells`）`PASS 1785 DEMO checks`；`--module-tests` 也在同一個 job 通過。

`ClientSpellChecks` 的內容（全部是測試自編的合成表，名稱是「測試甲」之類）：

| 項目 | 測試 |
|---|---|
| 解析 | 筆數、名稱、MP；括號前沒有空白；第三欄材料數；括號內不是數字或沒有括號 → MP 未知；空名稱、亂行跳過；表裡沒有的 id 沒有項目；第一行不是筆數、索引超出、索引重複、被切斷的雙位元組字都拒絕 |
| 對象 | 只有剛好「對象：施法者」→ none；「施法者、隊伍成員」→ buff；DHH 攻擊優先於說明；HD／HH／S 不受影響；沒有說明表 → buff |
| 等級與學費 | `Level` 1、8、9、121、220；`SkillCatalog` 名稱取表、學費＝基價×等級²；第 16 級、表裡沒有的 id、基價 0 → 沒有學費 |
| MagicSpells | 名稱、MP、動作 18／19、冷卻＝`DefaultDelayMs`、`Describe` 的提示；沒有表時仍可施放、名稱「原服技能 N」；支援範圍 1–220 |
| Text 封存 | 合成 `spelldesc-c.tbl` 過不了 pin；缺表、表重複都拒絕 |
| S41 學魔法 | 沒有客戶端表：提示等於 `SkillCatalogUnavailableNotice`、含檔名，兩列都停用、`ValidateService` 拒絕；有表：名稱與學費（100、400）取表，可以學 |
| 真客戶端（只在 `TW_CLIENT_ROOT` 有 Text 時；CI 沒有） | 220／183 筆、52 筆「施法者」、技能 1、3、4、8 的 MP 與對象對上 T446 2.2 的基線比對 |

既有測試：`combat-loop` 的學魔法流程改用合成表（學費 6000 不變）；`cast-cadence`／`duel` 的夾具不再寫 `magic/catalog.json`（冷卻 10 ms 來自原版預設，期望值不變）；`skill-cast-tests` 的「catalog-independent support」與 `SpellTradeTests`（用反射塞 `entries`）沒有改，欄位名稱保留。

### 推測

- `.NET` 的 code page 950 對 `spelldesc-c.tbl` 嚴格解碼會成功（Python 的 cp950 嚴格解碼成功；兩者的表可能有少數差異）。如果真客戶端嚴格解碼失敗，表會被當成讀不到，學魔法會提示不可用——這要真客戶端驗證。
- 3.81 原版決定「要不要先選目標」的邏輯沒有反組譯（T446 2.4）；「對象：施法者」是 T446 提出的自主規則。

### 待驗證（交 Codex／真機）

- 真客戶端跑 `DemoChecks . <out.json> client-spells`（`TW_CLIENT_ROOT` 指向 `D:\lineage開發\0-客戶端`），會跑上表最後一列。
- 真機：NPC 學魔法清單的名稱與學費、技能窗的名稱與 MP、只能對自己的技能（例如保護罩）不要求選目標。
- 原生模組測試（`--module-tests`）在 CI 跑；本機沒跑。

## 產出路徑

- `engine/source/ClientSpellTable.cs`（新）
- `engine/source/MagicSpells.cs`、`NpcServices.cs`、`NpcUi.cs`（修改）
- `engine/_integration/dev/demo-tests/ClientSpellChecks.cs`（新）、`CombatLoopChecks.cs`、`CombatAdmissionChecks.cs`（修改）
- `docs/reports/issue-1047.md`（本檔）
- 本機暫存：`scratch/issue-1047/`（解出的兩個表、Python 查核腳本、建置紀錄；不進 git）

## 給下一棒的提示

- **T467（總流程）／T468**：`dialogs/skills.json`、`magic/catalog.json` 已經**不需要**產生，也不需要放進 assets 樹；產品現在完全不讀這兩個路徑。可攜 zip 與 integrity 清單如果還列著它們，可以拿掉。
- **T470**：真客戶端驗證時順便看 `ClientSpellTable.Current` 是否非 null（`LoadError` 會說原因）。如果 .NET 嚴格解碼 `spelldesc-c.tbl` 失敗，改成跟 `SpellExDesc-c.tbl` 一樣的取代式解碼即可（`ClientSpellTable.Parse`）。
- **T471**：這兩個檔本來就不在 `b15Exceptions`（它們在 junction 的 assets 裡，不是追蹤檔），所以 allowlist 不用改。
- 其他要用技能名稱的地方：用 `ClientSpellTable.Current?.Get(id)` 或 `MagicSpells.Get(id).Name`，不要再新增技能名稱的資料檔。
- 測試要在本機重跑請交給 Codex：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-1047/run-<時間>/client-spells.json client-spells`（`combat-admission` 群組也會跑同一組）。
