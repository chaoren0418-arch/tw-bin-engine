# T101：攻擊技能接收與執行期素材

依據：[M6 契約](../design/m6-contract.md)、[#132 封包表](../reference/server-protocol.md)，以及 #188 主控對兩個 rendering 檔與持久化測試的範圍核准。只讀原服 serializer，沒有複製伺服器原始碼。

## 封包與狀態

- S30：24 bytes 是近戰；40 bytes 依 flight=0／127 分為法術／箭矢。action、gfx 與法術 sequence338 均不作硬編碼白名單。箭矢仍驗證序號338、保留欄與 padding。
- S42：先驗證完整標頭、數量、每個 target／hitFlag，再分別嘗試標準或逐非零 target 的傷害延伸布局；不以偶然出現的延伸 magic 判斷下一筆欄位。拒絕半套延伸及截斷時不改世界狀態。
- HP 只接 S225／S237 等權威回包；S30 damageWord、S42 hitFlag／DisplayDamage 都不能獨立扣 HP。延伸傷害保留 uint，不截成 ushort。
- S42 type0 在可見目標位置建立特效；未知／抑制目標沒有座標，不臆造位置。type8 以封包來源座標與 heading 建立單一方向特效，目標列表仍各自套用受擊旗標。空列表保留來源位置的特效。這些呈現政策有離線回歸，實際原服範圍技能的錨點與時間仍交 M6 smoke 比對。

## 素材與呈現

- 已封裝的既有法術與箭矢66維持原路徑。新增 gfx 經 `EffectSpriteCatalog` 在執行期唯讀 `TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`）的 Text.idx／Text.pak、Sprite.idx／Sprite.pak 與 Sprite00–15 封存；不依賴舊 BIN、不落地解包。
- Text 的 28-byte records 在 header4 後做固定格式 DES ECB 轉換，完整8-byte區塊外的尾端不變。只保留 list.spz／list.spr 索引，無關對話重名不阻擋素材。優先 list.spz：保留前5 bytes，後方完整16-byte區塊做格式指定的 AES CBC 轉換，尾端 XOR 前一密文區塊。只有缺少 spz 時改讀 spr；損壞 spz 不悄悄退回舊版表。
- metadata 使用 gfx→sprite 連結、action0 序列、方向、ticks、110.framerate（預設24）、109.effect 的飛行類別／impact gfx。沒有 action0 的 alias 可繼承來源；拒絕循環。104.attr 的 8／128／256 對應既有 `MapEffectAppearance` 的 screen／multiply／half blend。
- pure effect 從 metadata index1 開始，有限播放一圈；class3 箭矢採30ms步進，class4 法術採60ms步進。箭矢三影格序列保留飛行／末段索引；其餘依自己的動畫時間。命中讀自己的 impact gfx，不套用箭矢66圖像。目標座標在 release 固定，不追逐；已知施放者死亡或換外觀時取消尚未釋放的飛行。
- SPR 支援 indexed palette／直接RGB555、原生空影格、稀疏像素、A/B錨點、Z深度與原圖塊順序；沿用現有 compositor。索引、檔案大小、影格、圖塊、offset、RLE邊界、展開長度與尾端均檢查，快取有上限。
- 找不到或無法解碼的 gfx 會留在 `Renderer.EffectResourceErrors`，不換圖、不使 compositor 崩潰。只支援這個客戶端已核對的 plain28／SPR 格式，未宣稱支援其他封存格式或每個 metadata event 的音效／附屬效果。

## 來源識別與驗證

- 原服 `S_UseAttackSkill.java` SHA-256：`83e00ecf1da363510643ae259d8da3c43d99508fef9edb36fa9dacf74403dcfe`。
- `S_UseArrowSkill.java`：`870d1757e281e346a635c52e6f3b4cf8b5dc22a06959ff5c88a1029bc6a0a769`；`S_RangeSkill.java`：`2e6cce916e7b43835793ae55f346284a94e078a5699e3779542a86d96b8d635f`。
- Text／SPR 格式沿用本機 P03／P04／P22 已有研究，只讀參考，沒有將研究目錄當開發入口。本次 Text.idx SHA-256：`43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f`；Sprite.idx：`327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b`。Text 索引已不同於歷史研究版本，兩筆選定原始 payload 仍相同：list.spr SHA-256 `69faa6bdb817279d6b20d132c7744be387ccecd9843ac82cf454e363932d33c4`、list.spz `ffd64d13d4834664446375d072cdab4c0bf7ba9a13078bf597cbf90eda64f5c2`。
- Text plain28 的 list.spr 原始記錄位置為 File Offset `4+28×15937`，payload 為 Text.pak File Offset `7864336`；list.spz 為記錄15938，payload File Offset `11931753`。執行期以名稱查索引，不硬編碼上述位置。SPZ轉換的歷史原 BIN 證據是 RVA `0069FA30`／`000D5EB0`／`000D8FB0`，不是本次執行 BIN。
- `demo-tests` 的 `attack-skills` 群組使用明示的合成封包與一像素假素材，只測產品程式，不作實際遊戲證據；既有 CI `combat-admission` 入口會一併執行。`attack-skill-resources` 另唯讀核對本機原素材及既有封裝像素，不納入無素材 CI。
