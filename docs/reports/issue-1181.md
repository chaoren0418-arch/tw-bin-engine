# Issue #1181：分流 #1179 實機驗收失敗項目

**結論：#1179 只有 N01 一項 ❌，根因已用伺服器原始碼靜態核對確認為客戶端程式缺陷**（S250/20 的登入狀態封包被當成 S_Icons 快照拒收，進世界即 Faulted）。開 1 張修正任務 `f1179-activespells`（code／claude-local）＋1 張重測 `f1179-retest`（test／codex-local，只重測 N01）。沒有環境／資料問題、沒有已知限制。

本張只寫文件與規劃檔，沒有改程式、沒有執行任何程式。

## 1. #1179 已通過（✅），之後不重測

| 編號 | 結果 | 是否在新重測附帶回歸 |
|---|---|---|
| G01（前置） | ✅ | 只當重測前置執行，不算回歸 |
| G05 | ✅ | 否：修正只改 `MagicStatus.cs`，不碰 `panel.js`／開發工具截圖路徑 |
| G06 | ✅ | 否：同上，網頁面板與 UIA helper 不變 |
| G07 | ✅ | 否：`UiaGdi.ps1` 與外層分頁不變 |
| 沿用項（#1173 P01；#1165 G04、F01、#1108 R07；#1150 G02、G03；#1149 S02／S02-GDI／S02-CLOSE；#1118 S01；#1126 D01、C01；#1108 B01、R05、R08–R15、C01、C02、V01） | ✅ 沿用 | 否 |

## 2. 失敗項目對應

| 項目 | 分類 | 對應 | 說明 |
|---|---|---|---|
| N01 ❌（進世界 `ProtocolException: unsupported status snapshot size`，未達 InWorld） | (a) 程式缺陷 | `f1179-activespells` → 重測 `f1179-retest` | 根因見下 |
| N01 子條件（世界中 TabItem 列表、Enabled=true、切聲音／血盟） | 未執行（被上面擋住） | `f1179-retest` | 不是獨立缺陷，修好後一併驗 |

### N01 根因（已驗證）

- 證據：`docs/reports/issue-1179-evidence.json`（`task/1179` 分支 `9a905ce3`）的 `decodeFailure`：sequence=30、opcode=250、**length=112**、plainHex=`FA 14` + 110 個 `00`；堆疊 `MagicStatus.cs:52` ← `Magic.cs:88` ← `WorldSession.cs:71`。
- 客戶端：`engine/source/MagicStatus.cs:51–52` 把「子碼 20 且長度≠8」一律當 `S_Icons` 讀 `D`，不等於 204 就丟例外。
- 伺服器原始碼（唯讀，`D:\lineage開發\1-伺服器\src\com\lineage\server`）：
  - `serverpackets/S_Icons.java`（SHA-256 `A79C613FB69D5765AC8EAF3B64F2F879AFD026EC47E5FFAEA5AA9F0E74DA047B`，與 `docs/protocol/status-icons.md` 已登錄值相同）：`C 20, D 204, 204 bytes` → padding 後 216。
  - `serverpackets/S_PacketBoxActiveSpells.java`（SHA-256 `0F4E0AA098041A1229582F91A10B56CA6D9798AC1AAADAA0F76FBD50AE77FDE3`，**未登錄**）：`C 0x14` 後逐一寫出 `int[104]`，值＝72 的寫 `D(時間戳)`、其他寫 `C`。全 0 時 raw 106 → `ServerBasePacket.java:346` 補到 8 的倍數 → **112**，與證據一致。
  - 呼叫端 `clientpackets/C_LoginToServer.java:1290`：每次登入都送。所以 P24Lab 上**任何角色**進世界都會 Faulted，不只 N01。
- 推測：先前驗收沒撞到，是因為多半停在 CharacterList；本報告未追查哪一輪最後一次成功進世界。

### 已知限制

無。

## 3. 其他依賴 #1179 的排隊任務

查 `gh issue list --state open`（8 張）內文含 `1179` 的只有本張 #1181，沒有需要改 `depends_on` 或關閉的任務。

## 怎麼驗證

- 讀 #1179 Issue 內文、`origin/task/1179` 的報告與證據 JSON。
- 讀 `engine/source/MagicStatus.cs`、`docs/protocol/status-icons.md`、伺服器原始碼 `S_Icons.java`、`S_PacketBox.java`（case 20）、`S_PacketBoxActiveSpells.java`、`ServerBasePacket.java`（padding）、`C_LoginToServer.java:1290`，以 `Get-FileHash` 算 SHA-256。

## 未驗證什麼

- P24Lab 實際執行的 JAR 是否與 `1-伺服器\src` 完全一致（長度 112 與原始碼推算吻合，推測一致）；修正任務設了停止條件。
- 修正後能否真的進世界、世界中服務分頁行為：交 `f1179-retest`。

## 產出路徑

- `docs/plans/issue-1181.json`（`f1179-activespells`、`f1179-retest`）
- `docs/reports/issue-1181.md`（本檔）

## 給下一棒的提示

- 修正任務：ActiveSpells 的「值＝72 改寫 D」怪癖讓長度與欄位不唯一，最穩的做法是吃掉封包、不改 buff 狀態，後續 S_Icons／個別圖示封包會補齊；不要再用「長度≠8 就是 S_Icons」的二分法。
- 修正任務可順便列出 `WorldSession` 其他嚴格長度比對、遇未知長度會讓整個 session Faulted 的 S250 子碼（只列，不改）。
- 重測只做 N01（加 G01 前置）；G05／G06／G07 已通過且不受影響，不要重跑。
