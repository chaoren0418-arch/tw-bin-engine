# Issue #1187 — S250/20 登入狀態封包（S_PacketBoxActiveSpells）不再讓 WorldSession Faulted

階段：code（claude-local，第 1 次嘗試）。分支：`task/1187`。來源：#1179 N01（`docs/reports/issue-1179.md`、`issue-1179-evidence.json` 的 `decodeFailure`）、分流 #1181。

## 做了什麼

1. `engine/source/MagicStatus.cs`：S250 子碼 20 改成依 plain 長度（含補零）判別。
   - 修改前：第 51–52 行 `if(sub==20&&plain.Length!=8){ if(r.D()!=204)throw new ProtocolException("unsupported status snapshot size"); ...`——任何非 8 長度都當 S_Icons，112-byte 的 ActiveSpells 讀到 D=0 就丟例外，整個 session Faulted。
   - 修改後：
     - 第 30–37 行：新增常數 `IconsSnapshotLength=216`、`ActiveSpellsMinLength=106`，與診斷屬性 `LoginActiveSpellsPackets`（收到並略過的封包數）、`LastLoginActiveSpellsLength`（最近一次長度）。
     - 第 59–69 行：`8` → 既有 C/H 分支（第 78 行，未改）；`216` 且 D＝204 → 既有 S_Icons 快照（第 69 行起，未改）；**其他且 ≥106** → ActiveSpells：計數、記長度、`return true`，不碰任何 buff、不呼叫 `ReceiveBuff`、不遞增 `MagicRevision`；其餘（<106 且 ≠8、或 216 但 D≠204）丟 `ProtocolException($"unsupported status snapshot size {plain.Length}")`，訊息帶實際長度。
   - **不解析欄位**。依據：伺服器依「值＝72」而非欄位決定寫 C 或 D，長度 106＋3k 補到 8 倍數後 k=0,1,2 同為 112，補零的 0 與欄位 0 無法區分；唯一位置可確定的是第一個可能被填值的欄位（索引 17）之前的索引 0–16，而伺服器對這些欄位永遠寫 0（沒有資訊）。所以沒有可證明無歧義又有意義的欄位可解析。
2. `engine/source/PacketBoxStatusTests.cs`：新增 `[WorldModuleTest("packetbox-active-spells")]`（第 135 行起，自動發現，不改註冊表）與合成器 `ActiveSpells(...)`（照伺服器寫法：值＝72 寫 D 時間戳、其他寫 C，再 `LoginPackets.Pad`）：
   - (a) #1179 證據封包：112 bytes、`FA 14`＋110 個 0 → 不丟例外、buff 不變、計數 1、長度 112、不進 unhandledSubtypes。
   - (b) 含一個 72（112 bytes）、三個 72（120 bytes）、9 個可填欄位全為 72（136 bytes，最長）→ 都吃掉、buff 不變。
   - (c) 216-byte S_Icons（格 0＝15）→ 冥想術 32 約 60 秒，來源 icons1，行為不變；不算入 ActiveSpells 計數。
   - (d) 8-byte C/H（H 30、C 5）→ 鍵 -20005、30 秒、來源 icon20，行為不變。
   - (e) 24-byte（補零後）、未補零 20-byte、216-byte 但 D=203 → 都丟 `ProtocolException`，訊息含實際長度；buff 與計數不變。
3. `docs/protocol/status-icons.md`：S250/20 拆成 216 快照與 ActiveSpells 兩列，加長度判別規則、72 怪癖與最長 136 bytes 的推導；SHA-256 表加 `S_PacketBoxActiveSpells.java`、`C_LoginToServer.java`、`ServerBasePacket.java`（本機 `Get-FileHash` 重算）。

## 伺服器原始碼對照（唯讀，基準 `D:\lineage開發\1-伺服器\src\com\lineage\server`）

| 檔案 | SHA-256 | 行號 | 內容 |
|---|---|---|---|
| serverpackets/S_PacketBoxActiveSpells.java | 0F4E0AA098041A1229582F91A10B56CA6D9798AC1AAADAA0F76FBD50AE77FDE3 | 18–28 | `writeC(PACKETBOX)`、`writeC(0x14)`、逐個 `case 72: writeD(秒)`／`default: writeC(i)` |
| 同上 | 同上 | 36 | `int[] data = new int[104]` |
| 同上 | 同上 | 48–57、66、71、76、122–131 | 會被填值的欄位：78/79、102/103、76/77、61、17、57（80–113 行為註解，不生效） |
| clientpackets/C_LoginToServer.java | C2EEDCB6D78D9DA7F33AF485C596D393D49818A0A05BC5A37CBEDB79BCFC9DE4 | 1290 | 登入還原 buff 後 `pc.sendPackets(new S_PacketBoxActiveSpells(pc))` |
| serverpackets/S_Icons.java | A79C613FB69D5765AC8EAF3B64F2F879AFD026EC47E5FFAEA5AA9F0E74DA047B | 87–92 | `writeC(PACKETBOX)`、`writeC(ICONS1)`、`writeD(data.length)` |
| serverpackets/ServerBasePacket.java | 3348C199E9694BD1EEC050B130F28AB99590CAE16F0047964C17B32DF309A439 | 345–346 | `padding = (8 - (rawLength & 7)) & 7` |

長度推導：全 0 → raw 2＋104＝106 → 112（與 #1179 證據 sequence=30、length=112 相符）；最多 9 個欄位＝72 → raw 133 → 136；S_Icons raw 210 → 216。兩者長度集合不相交。

## 怎麼驗證

- **已驗證**：`dotnet build engine/source/OfflineClient.csproj -c Release`（本機，`DOTNET_CLI_HOME` 設在 scratch 並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`）建置成功、0 個錯誤。
- **已驗證（靜態）**：伺服器原始碼格式與 #1179 證據 plainHex 長度、內容一致（見上表）。
- **待驗證（CI）**：新增的 `packetbox-active-spells` 模組測試與既有 `packetbox-status` 由 PR 的 CI（`P11.ClientEngine.exe --module-tests`）執行；本機依規定不執行編譯產物。
- **待驗證（真機，交 `f1179-retest`）**：P24Lab 既有角色（例如「資源法師」objid=10334）進入世界，login-state＝InWorld，不再出現 `unsupported status snapshot size`；世界快照中 `LoginActiveSpellsPackets`≥1（目前只是 `WorldSession` 公開屬性，未接進 `MagicSnapshot`——那在 `Magic.cs`，不在本任務檔案範圍）。

## 推測

- 真機上 P24Lab 角色若身上有恐懼無助／迴避提升／生命之樹果實／龍之魔眼等 buff，ActiveSpells 長度可能是 112–136 之間的 8 倍數；本版全部接受、不顯示。這些圖示是否另有個別封包補齊（`restoreCustomBuffIcons`、`restoreTimedRateIcons`，`C_LoginToServer.java` 1291–1292 行）未逐一核對。
- P24Lab 執行中的 JAR 是否與 `1-伺服器\src` 一致：見下方「JAR 核對」。

## JAR 核對

- 本機路徑 `D:\P24Lab-ClientEngine` 在此執行環境不存在（`Test-Path` = False），**無法直接核對執行中 JAR 的類別**（待驗證）。
- 間接證據（已驗證）：#1179 證據的封包（opcode 250、子碼 0x14、112 bytes、資料全 0）與 `S_PacketBoxActiveSpells.java` 在角色無相關 buff 時的輸出逐 byte 相符，且不符合 S_Icons（216）與 C/H（8）任一格式；因此未觸發停止條件。

## WorldSession 中其他遇到未知長度會讓 session Faulted 的 S250 子碼（只列，不改）

`engine/source/MagicStatus.cs`（`ReceiveMagicStatus`，由 `Magic.cs:88` ← `WorldSession.cs:71` 呼叫）：

| 子碼 | 行號 | 嚴格點 |
|---|---|---|
| 21 | 78–81 | C/H/C 後 `r.Finish()`，非 8 bytes 丟 `unexpected packet length` |
| 22 | 82–86 | C/C/H/C 後 `Finish()`，非 8 bytes 丟例外 |
| 34、35、36、40 | 87–90 | H 後 `Finish()`，非 8 bytes 丟例外 |
| 44 | 91–95 | D/H 後 `Finish()`，非 8 bytes 丟例外 |
| 57 | 96–99 | 固定欄位≠44 丟例外，非 8 bytes 丟例外 |
| 154 | 101、106 | 不是 8 也不是 16 丟 `unsupported skill icon length` |

另有 S250 處理分散在 `WorldSession.cs:56`（111 位置分享）、`:58`（41 角色設定，`CharacterConfig.cs:74` 長度不符丟例外）、`:59`（17/18/19 好友）、`:82`（171 血盟線上）、`CharacterManagement.cs:107`、`Duel.cs:73`、`LocationShare.cs:163`、`Party.cs:137` 與 `PacketBoxStatus.cs`（B2 子碼，畸形一律丟例外，見其測試第 13 段）。這些多以 `LoginReader.Finish()` 檢查長度，遇到未知長度同樣會 Faulted；是否要改成「計數＋略過」需逐一比對伺服器發送者，建議另開任務。

## 產出路徑

- `engine/source/MagicStatus.cs`
- `engine/source/PacketBoxStatusTests.cs`
- `docs/protocol/status-icons.md`
- `docs/reports/issue-1187.md`（本檔）

未改 `Program.cs`、`DemoChecks.csproj`、註冊表、`rebuild-lock.json`、`docs/REPORT.md`。

## 給下一棒的提示

- CI 綠燈後交 `f1179-retest`：P24Lab 進入世界，確認 login-state=InWorld；`LastWorldDecodeFailure` 應為空。若仍 Faulted，看新的 `decodeFailure`——錯誤訊息現在帶長度。
- 若要在 UI／快照看到診斷，可另開任務在 `Magic.cs` 的 `MagicSnapshot()` 加 `loginActiveSpells` 欄位。
- 若之後要顯示 ActiveSpells 的 buff，需要伺服器端改成固定寬度（或另一個能區分欄位的格式），否則客戶端無法無歧義解析。
