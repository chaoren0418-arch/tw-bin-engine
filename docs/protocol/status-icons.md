# T108 狀態圖示協定補充（#196）

先依 #132 的 `docs/reference/server-protocol.md`／`server-rules.md` 差異 15 查表；以下是 2026-11-07 唯讀核對 serializer、呼叫端與技能常數後的補充。沒有讀取 DB、機密設定或複製伺服器程式。欄位 H／D 為 little-endian；封包含 opcode，尾端依既有 LoginReader 規則補零至 8-byte。

## 接收格式

| 封包 | opcode 後的欄位 | 處理 |
|---|---|---|
| S216 | H 秒、C type | type 5／3／6／7／10 → 技能 3／99／151／159／168；其他 type 保留中性名稱 |
| S250/20、21 單筆 | C sub、H 秒、C type | 專用 S_PacketBoxIcon1／2 格式；type 沒有已證實的名稱對照，顯示「狀態 sub/type」，不同 sub 不共用鍵 |
| S250/20 快照 | C 20、D 204、204 bytes | S_Icons 全量快照。先完整檢查長度、padding 才套用；只解釋已核對的技能欄位，時間戳等其他 bytes 不當圖示 |
| S250/22 | C 22、C icon、H 秒、可選 C variant | 無 variant 的補零與 variant=0 等價；已知光環依下表，未知 icon/variant 保留中性名稱 |
| S250/34、35、36、40 | C sub、H 秒 | 魔力回復藥水、變身、禁言、聖結界 |
| S250/44 | C 44、D objectId、H 時間格數 | 風之枷鎖；只更新自身 objectId，每格 4 秒 |
| S250/57 | C 57、C 44、H 秒 | 慎重藥水，固定欄位必須是 44 |
| S250/154 原生（8 bytes） | C 154、H 秒、D gfx | gfx 2165／747／748／2176／2951 → 技能 8／12／21／48／107；其他 gfx 沿用未支援計數 |
| S250/154 代理（16 bytes） | C 154、H 秒、H key、H 0、H remove | remove=0 新增、1 刪除；未知 key 以「技能圖示 key」呈現，不推測能力或圖像 |

代理 key 518–537 依序對應技能：148、149、155、156、163、166、3、99、151、159、168、26、110、42、109、114、115、8、48、12。

光環 icon 113／114／116／147／148／154／155／162／165 分別對應技能 114／115／117／148／149／155／156／163／166。

快照已核對的格位（data 的零起始 offset，不是 BIN 位址）：

| 單位 | 格位 → 技能 |
|---|---|
| 4 秒 | 0→32、1→31、3→14、4→71、5→78、7→104、8→47、9→56、17→111、18→55、19→158、20→167、21→153、22→134、24→133、26→171、30→174、31→175、32→173、55→191、56→193、57→188、58→211、59→183、61→1017 |
| 16 秒 | 23→176、52→206、53→216、54→217、60→181 |
| 武器共用格 | 10 為 4 秒格數，11+1 為技能 ID，只接受 148／149／155／156／163／166。格 27 是烈炎氣息的重複表示，不另建第二圖示 |

## 時間、解除與來源

- 倒數採單調時鐘；同技能重送會更新起點與時間，不增加重複圖示。時間為零或 remove=1 立即移除；到期在下一次 HUD／狀態讀取移除，不再顯示「等待原服更新」。既有 session reset 清空全部；跨圖不擅自取消 buff。
- 代理解除可清除同技能；原生與快照解除只清除自身來源，避免 S_MagicEquipment(0,2165) 把仍有效的代理神聖武器刪掉。快照的零格不會清掉其他來源；不同來源更新同技能時以最後收到的正值為準。
- 批次時間是量化值，標示「約」。尤其弱化／疾病格 8／9：伺服器對小於 32 秒使用約 2 秒格，大於等於 32 秒使用 4 秒格，封包沒有分支旗標，無法唯一還原。本版按 4 秒格顯示近似值（短效果可能高估），伺服器後續零格會立即解除；真機精確倒數仍待 M6 smoke 確認。
- 所有變更只影響圖示。不能以倒數推導實際能力、施法成功、移動封鎖或伺服器效果已解除。

## 已知邊界

- S_PacketBox 的通用 overload 也有 sub20 C/H、sub21 C/C、sub22 H/H 格式，與專用類別的欄位順序不同、補零後長度可能相同。本版按任務指定的專用 Icon1／Icon2／IconAura 格式；未宣稱能無歧義識別全部 overload。原服目前一般 buff 呼叫端採本文件列出的專用／快照路徑。
- dodge 數值通知 sub88／101 不包含剩餘秒數，不當成定時圖示；其他 packetbox 保持既有分派。S255／67 速度狀態由原模組處理，本次不加第二份速度狀態。
- HUD 使用自行繪製的菱形「+」與三角形「−」、名稱、秒數及完整 tooltip／無障礙文字；不是舊客戶端素材圖像。超出寬度可點左右箭頭或滾輪翻頁。

## 唯讀來源 SHA-256

基準目錄：`D:\lineage開發\1-伺服器\src\com\lineage\server`。以下是檔案 SHA-256，無 File Offset／RVA／VA。

| 相對路徑 | SHA-256 |
|---|---|
| serverpackets/S_SkillIconShield.java | 42b6de9985acd196d51f278278d66f513e98081529e8f1fe180b1cfc3a8c05a8 |
| serverpackets/S_PacketBoxIcon1.java | 028327d0f79578c5dba63dfe0a1348b5b484d26efb352821351a47eeb1daa7f2 |
| serverpackets/S_PacketBoxIcon2.java | b15290a47cc1417954ec14da45d83e6df8f84ea121bbf5f5f3015330d6149cd3 |
| serverpackets/S_PacketBoxIconAura.java | 8ed97f704874e3f979570df08c87b914c5417d9b3be06815c3db78710cec1998 |
| serverpackets/S_Icons.java | a79c613fb69d5765ac8eaf3b64f2f879afd026ec47e5ffaea5aa9f0e74da047b |
| serverpackets/S_IconConfig.java | bc4f1629a14cc656c82402ca482aa55661345c85866e403b353bd3c7aba0cca9 |
| serverpackets/S_MagicEquipment.java | b6e76b4e0003906803e71d092ff1cc2b9e6051c82e2059ea733974ab2b14baa7 |
| serverpackets/S_PacketBox.java | 817ba5fd5b8ada480c6cef6d0529b9cb8ac8c6a33d84af550f8479a31fcce977 |
| serverpackets/S_PacketBoxWindShackle.java | b0a1df2db3e1ddc881e23f3879ffe0e90a26c0a3c57927d7843407faf864621a |
| serverpackets/S_PacketBoxWisdomPotion.java | cc3d17cba81f2a85067dfd1eaaa60e01577bd656c0ccd0e72a7a56fd30e994b8 |
| model/skill/IndependentSkillIcon.java | 5d8cb03f7fc1b6def81387c6fedd35f4f44fea2789f220973c4515c8b9ad51c6 |
| model/skill/SkillBuffIconHelper.java | 6c923bf402757e707f172d7eb8cbda854c3ca7c4924fea2d0b134242224ca217 |
| model/skill/L1SkillId.java | 5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14 |
