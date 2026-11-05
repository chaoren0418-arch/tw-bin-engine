# dev 局部真機補測基線（T042／T044）

依 [#37 結果留言（2026-11-05 05:32 UTC）](https://github.com/chaoren0418-arch/tw-bin-engine/issues/37#issuecomment-6650577185) 及[原報告](../reports/issue-37.md) 整理：**✅ 04、05、07；⚠️ 06、15；❌ 10**。本頁只提交既有局部證據，不代表完整 N01 驗收，也不是本輪重新執行遊戲。

既有測試為 dev 版本、map0、服務驗證測試角色；DLL SHA-256 為 5c45ebe0bd0d779b3ca0769d701a1c10486773569da1e5462e441a5f42eed0ae（沿用 #37）。圖片與 trace 大小、SHA-256、機密檢查及處置見 [MANIFEST](dev-retest/MANIFEST.md)。

## 六項結果

C 為出站、S 為入站，op 使用十進位。除 10 為整段登入工作階段外，以下數字皆使用原摘要的操作時間窗。入站含附近物件及定時回報，不能把相近時間的封包全部歸因於操作；C29 也不是逐步伺服器 ACK。

| 項目 | 結果與數字 | 重點 trace 統計 | 截圖 |
|---|---|---|---|
| 04 八方向 | ✅ 八方向各 2 次，共 16 次；map0 起終均 (32571,32957)。起點由客戶端 state、終點由正常登出後儲存座標核對。 | C29×16、C95×2；入站 99／出站 18。 | [前](dev-retest/04-before.png)、[後](dev-retest/04-after.png)、[起始診斷](dev-retest/04-start-diagnostics.png) |
| 05 點地 | ✅ 點視窗相對位置 (720,300)，(32571,32957)→(32574,32957)，共 3 格；登出後座標一致。 | C29×3；入站 23／出站 3。 | [前](dev-retest/05-before.png)、[後](dev-retest/05-after.png) |
| 06 近戰 | ⚠️ 牛目標 2000036217，按 G 共 4 次後目標離開可見範圍／選取消失，立即停止；未完成擊殺或 15 次。EXP 412855581→412855581，角色 HP 686 不變，最後 DamageWord=0，沒有怪物 HP 差或死亡證據。 | C229×4、C95×2、S30×4；入站 81／出站 6。S237×1 不等於目標 HP 差證明。 | [前](dev-retest/06-before.png)、[接近](dev-retest/06-approach.png)、[診斷](dev-retest/06-diagnostics.png)；均非擊殺圖 |
| 07 光箭 | ✅ 技能 4、牛目標，只按 1 次 M；MP 117→115，Gfx167／Action18／DamageWord15，截到藍色效果。傷害欄 15 不當作獨立 HP 差。 | C6×1；S30×1、S33×1、S237×1；入站 19／出站 1。 | [前](dev-retest/07-before.png)、[藍色效果](dev-retest/07-effect.png) |
| 10 金幣 | ❌ 金幣 33400、selectedId=1900249101、droppable=false，丟棄按鈕停用；未丟出／拾取，沒有 33400→33399→33400。 | C25=0、C112=0；整段僅 C95×5、C119×1、C137×1；入站 222／出站 7。 | [選取金幣](dev-retest/10-gold-selected.png) |
| 15 音訊 | ⚠️ 音量 50%，未靜音／靜音／取消靜音各按 1 次 F3 初級治癒術；MP 依序 117→114、114→111、111→109，可能含自然回復。三次皆有施法／效果回包，沒有播放證據。 | C6×3、C95×2；S55、S71、S158、S174 各 3；入站 85／出站 5。 | [控制](dev-retest/15-controls.png)、[未靜音](dev-retest/15-audible.png)、[靜音](dev-retest/15-muted.png)、[取消靜音](dev-retest/15-unmuted.png) |

06 最後截圖因原測試 UIA 擷取失敗未產出，沒有 06-after.png。07 登出 MP 116 屬後續自然回復，施法後 115 取自即時觀測。15-audible.png 僅是原檔名，不代表有聲音通過；另附 [切換聲音頁前](dev-retest/15-volume50.png) 與[關閉前](dev-retest/15-closing.png)。

## 三類未通過原因

- **06：測試條件問題。** 目標離開可見範圍，4 次近戰後停止；現有證據不能判斷怪物扣血或擊殺，需先安排穩定可見的弱怪條件再補測。
- **10：產品限制。** [GroundRules.Droppable](../../engine/source/Ground.cs) 只允許特定的肉與箭，金幣 droppable=false；IsGround 亦只辨識對應地面物。這次沒有繞過規則，不能解讀為已完成金幣丟棄／拾取。
- **15：功能缺口。** 在本次來源基準 b573c7be069789d1f4c6c8121e9338a51dc88530，Magic／Combat 沒有任何音效派送，現有遊戲音效接線只有 Effect6016；可對照 [Magic.cs](../../engine/source/Magic.cs)、[Combat.cs](../../engine/source/Combat.cs)、[ClientEffect6016.cs](../../engine/source/ClientEffect6016.cs) 與 [Effect6016Host.cs](../../engine/source/Effect6016Host.cs)。這是 #41 的靜態來源核對，不將它冒充 #37 已做聽音驗收，也不宣稱全部音訊系統失效。

| 15 階段 | 設定 | 最近 host 設定回覆 | Playback | RemainingVoices | 施法窗新 host 事件 |
|---|---|---|---|---:|---:|
| 未靜音 | gain=0.5、muted=false、applied=true | ControlsApplied | null | 0 | 0 |
| 靜音 | gain=0.5、muted=true、applied=true | DispatchSuppressed | null | 0 | 0 |
| 取消靜音 | gain=0.5、muted=false、applied=true | ControlsApplied | null | 0 | 0 |

最近設定回覆不是每次施法新增的播放回覆；未量測 Windows 音訊峰值，也沒有可聽聲音證據。

## 完整操作窗 op 統計

每項原始 trace 是完整工作階段，含操作窗外流量；表格則取 [原摘要](dev-retest/trace/trace-summary.json) 的 counts，已用 trace 重算逐項核對。

| 項目／trace | 範圍 | 全部出站 op | 全部入站 op |
|---|---|---|---|
| [04](dev-retest/trace/04.jsonl) | action-window | C29×16、C95×2 | S10×78、S87×7、S118×1、S119×1、S120×6、S123×2、S158×1、S209×3 |
| [05](dev-retest/trace/05.jsonl) | action-window | C29×3 | S10×14、S87×3、S118×1、S119×1、S120×2、S158×1、S209×1 |
| [06](dev-retest/trace/06.jsonl) | action-window | C95×2、C229×4 | S8×2、S10×48、S30×4、S33×1、S37×1、S87×9、S120×10、S123×2、S155×1、S225×1、S237×1、S250×1 |
| [07](dev-retest/trace/07.jsonl) | action-window | C6×1 | S10×13、S30×1、S33×1、S87×1、S120×2、S237×1 |
| [10](dev-retest/trace/10.jsonl) | entire-session | C95×5、C119×1、C137×1 | S5×1、S8×4、S10×105、S33×2、S37×6、S39×1、S40×1、S64×17、S87×24、S93×1、S100×6、S115×1、S118×1、S119×2、S120×17、S123×5、S155×1、S158×1、S161×1、S164×1、S174×1、S178×1、S206×1、S209×2、S223×1、S225×1、S233×1、S242×1、S243×5、S250×10 |
| [15](dev-retest/trace/15.jsonl) | action-window | C6×3、C95×2 | S8×6、S10×37、S33×4、S40×6、S55×3、S71×3、S87×9、S120×6、S123×2、S158×3、S174×3、S250×3 |

15 的原摘要 fullSessionCounts 只對應最終 trace 前 456 行；原件另有操作窗之後 72 行，總計 528 行。六項操作窗統計均一致，差異詳見 [MANIFEST](dev-retest/MANIFEST.md)。原摘要及 trace 均原樣提交，沒有刪尾段或改寫數字。

## 已驗證與待驗證

本輪已驗證：17 張截圖逐張目視未見帳密明文且容量合格；6 份 trace 共 4,468 行只有指定五鍵；17 PNG／6 trace 與歷史清單大小及 SHA-256 一致；六項操作窗統計重算一致。原 issue-37.md 內容未改。

尚待驗證：06 怪物 HP／擊殺、10 金幣完整流程、15 實際聲音／音訊峰值，以及完整 N01。由 Claude／調度器先安排金幣支援、Magic／Combat 音效派送與穩定近戰條件，再安排使用者執行局部原生驗收；避免沒有前置變更就重跑相同限制。
