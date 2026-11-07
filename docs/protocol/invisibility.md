# 隱身封包與顯示契約（T149／#389）

## 已驗證：唯讀伺服器來源

先查 `docs/reference/server-protocol.md` 的 S_Invis／S87 與 `server-rules.md` 的 R10、R11，再唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server`。未讀設定或 DB，未執行伺服器。

| 封包 | 格式／語義 | 客戶端處理 |
|---|---|---|
| S171 / 0xAB | `C(171) D(objectId) C(type)`；D 為 little endian；type 0=解除、1=隱身；依既有傳輸契約補零至 8 bytes | 完整驗證後，只更新已存在 Actor／WorldObject 的 Flags bit 1（值 2） |
| S87 | OwnCharPack／OtherCharPacks 的 `STATUS_INVISIBLE=2` | 完整包取代狀態；隱身的自身或仍由伺服器提供的物件半透明 |
| S120 | `C(120) D(objectId)` | 沿用物件移除與選取清理，該物件不再參與繪製 |

S171 僅一種 wire 格式，沒有對應的新增 C 封包。客戶端拒絕零 objectId、未知 type、截斷、非零 padding 與多餘尾段，拒絕前不變更狀態；未知物件沿用 `UnknownObjectUpdates`，不建立物件或延遲套用到未來同 ID。

`L1SkillUse.applyInvisibilityEffect` 先建立隱身狀態，對本人送 S171/type=1，對其他人廣播 S120。`L1PcInstance.delInvis` 解除後對本人送 S171/type=0，對其他人廣播 S87。`L1PcInstance.onPerceive` 會由伺服器決定是否提供該人物；S87 的存在不是由客戶端能力推導。

因此不臆造 type=2 或「看穿隱身」旗標，不在客戶端檢查 GM／職業／技能。伺服器移除的物件隱藏，仍保留且帶隱身旗標的物件使用半透明；半透明物件保留既有像素點選行為。

## 渲染與生命週期

`Invisibility.cs` 使用既有 `OpacityValue`，`ObjectRendering.cs` 在 Blit 前只增加一個覆寫查詢點。僅 actor／shadow 層受影響，普通地圖、地面物件與其他效果保留原 opacity；既有 Hidden 不會被揭露，進行中的透明度 transition 不會被加亮。`OpacityRendering.cs`、`Renderer.Blit` 均未修改。

狀態直接存在 S87 Flags，無額外 objectId 快取。移動保留 Flags；新 S87、S120 移除、S206（包括同圖）、世界 Reset／切角沿用既有生命週期清除舊狀態。S171 不修改 HP／MP、不預測施法結果、不發送封包。

## 來源 SHA-256

以下路徑相對於上述 server 目錄；只記錄來源識別與結論，未複製伺服器原始碼。

| 來源 | SHA-256 |
|---|---|
| `serverpackets/S_Invis.java` | `891515fe8213d326999ceb8537f9543fe37100da96de23e16b59560f587f24c1` |
| `serverpackets/S_OwnCharPack.java` | `6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26` |
| `serverpackets/S_OtherCharPacks.java` | `95ee7228e1c9d14d1ae42ec35dbac5fd37166ab2a5ee06f94406960babf99ce5` |
| `model/skill/L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `model/Instance/L1PcInstance.java` | `0d1f267891c99e662bf0695d799a50e302f42a7f52a0440945372a443ac05c92` |

## 待驗證

本次來源契約與合成封包測試不等於部署 JAR／原生畫面驗收。M6 smoke 由 dot 驗證本人隱身及解除、旁觀者消失及重現、隱身中換圖／重連。
