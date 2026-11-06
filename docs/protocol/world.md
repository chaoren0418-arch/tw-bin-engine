# 世界、移動與 NPC 協定（T073／#130）

2026-11-06 唯讀查閱 `D:\lineage開發\1-伺服器\src\com\lineage\server`。以下是目前來源的結論，不是執行中伺服器或 P24Lab d287 JAR 與這份來源完全一致的證明。沒有啟動服務、查詢 DB、讀機密設定或複製 Java 原始碼。來源 SHA-256 見文末；數字為引擎目前的 opcode，欄位寬度 C/H/D 為 1/2/4 bytes，H/D little-endian。

| 操作 | 來源與封包 | 結論及引擎行為 |
|---|---|---|
| 請求步行 | `clientpackets/C_MoveChar.java`，C29：H 起點 X、H 起點 Y、C heading 0–7 | 伺服器以自身座標計算下一格；請求起點用來偵測漂移，不是權威座標。地形、佔用、動作節奏、角色狀態均可拒絕。 |
| 成功步行 | `serverpackets/S_MoveCharPacket.java`，S10：D objectId、H **起點** X、H **起點** Y、C heading、C 0x80 | 接收者須加方向向量得到終點。`C_MoveChar` 透過 `L1Character.broadcastPacketAll` → `WorldBroadcast.toVisiblePlayers` 廣播，**不含自身**，隱身等情況也會抑制廣播。一般自身步行沒有逐步成功 ACK，不能靠等待 S10 更新畫面。 |
| 自身校正 | `utils/PcPositionSync.java`、`serverpackets/S_PcPositionCorrection.java`（繼承 `S_OwnCharPack`） | 漂移、阻擋、節奏拒絕等呼叫校正；相同基準可合併省略，斷線／死亡／傳送中可跳過。soft 為自身 S87，strong 先解鎖與 map reset 再送 S87；失效的座標世代封包會被丟棄。校正後伺服器重建 known objects。不是每次拒絕都必定有回包。 |
| 物件出現／更新 | `serverpackets/S_OtherCharPacks.java`、`S_NPCPack.java`，S87；`model/Instance/L1PcInstance.java` perception/updateObject | S87 是完整外觀／位置資料，不是移動 ACK。PC 死亡時 action 欄改送死亡狀態；完整新資料應取代舊死亡、移動、變身待補、擺攤狀態。旗標不能可靠判別 NPC／PC（變形 NPC 也能帶 PC flag）。 |
| 離開視野 | `L1PcInstance` 的 known-object 清理、`serverpackets/S_RemoveObject.java`，S120：D objectId | 不同地圖／副本、超出畫面或設定辨識範圍時移除；再次 perception 重新送完整資料。引擎移除選取與對話；未知 ID 的晚到 delta 不能創建物件。S206、斷線重置清掉整個 AOI。 |
| NPC 交談 | `clientpackets/C_NPCTalk.java`、`utils/WorldInteractionGuard.java`，C34：D objectId | 必須為有效 NPC、有效角色與可見的同一世界；一般 `npcTalk`／`npcAction` 皆為 Chebyshev 距離 ≤5。死亡／傳送／擺攤禁止。自己的寵物、召喚物另有伺服器例外，引擎不推測擁有權。 |
| 對話回應 | `serverpackets/S_NPCTalkReturn.java`，S39：D objectId、S htmlId、H flag、H count、count 個 S 參數 | 僅在目前互動 ID／世界／選取版本吻合時顯示。旗標 0 對應無參數；一般參數型為 1。頁面可由任務、NPC TALK、legacy html 或 onTalkAction 產生。發出 C34 不代表成功；目標離場後的 S39 不得重開舊頁。 |

## 座標與操作呈現

- 目前位置由 `Actor` 顯示；送出步行後標「本地預測，非原服確認」。上次 S87／自身 S10 的座標、opcode 與 revision 分開保留，只有真正回包才更新。HUD 與 F9 共用同一顯示文字。
- 點地規劃有限路徑，每一步再次檢查地形、佔位、焦點與世界版本。Esc、改目的地、焦點離開、校正、換圖均停止未送出的動作。
- 沿用既有安全操作：左鍵選物件；雙擊或 Ctrl＋左鍵單次攻擊；右鍵單次交談；雙擊地面物品拾取一件。完整封包不提供可靠物種分類，不能依 PC flag 把單擊自動猜成攻擊或交談。
- 完整目標資料更新會使已排隊操作失效，需再次明確操作；S120 後同 ID 重現不沿用選取。變身等待完整外觀期間也不能選取。
- F9 圖示沿用 #123 的本機 HTTP 資源攔截，讀 `engine/assets/inventory`；不複製圖片進 repo。陰影沿用原始 frame key、half-blend，且不成為滑鼠選取像素。

## 驗證界線

`engine/_integration/dev/world-ui-tests` 對以上客戶端行為送合成封包、使用記憶體 sender，並執行本次產品 DLL 的 HUD、離線 ClientForm handler、renderer 與本機圖示讀取。WebView2 合成頁驗 HTTP/MIME/解碼。這些結果不能證明目前伺服器二進位、真實 OS 輸入、IME、DPI 或玩家觀感；統一交 T075。

## 唯讀來源 SHA-256

路徑相對於上方 `server` 目錄；僅保存識別資訊，不保存原始碼。

| 來源 | SHA-256 |
|---|---|
| `clientpackets/C_MoveChar.java` | `b569737e5fcda200291f803f41f4f9bddaa6cc6986c3869274934bcbc48a12d2` |
| `serverpackets/S_MoveCharPacket.java` | `cfe7d3cd0080572bc275811b6eac90cb63e3dadb03383ddf95792d4bcd15d8b0` |
| `utils/PcPositionSync.java` | `6c1ff6ec2bf53900520dc4aacd05c5513becc19a875392294957a3a17811f8d5` |
| `serverpackets/S_PcPositionCorrection.java` | `4d12c9d8c73c0a4ed13f1adc5d036e95eb0cdeaa92804951a204c5a9ce3c60ad` |
| `serverpackets/S_OwnCharPack.java` | `6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26` |
| `model/L1Character.java` | `efde278d67f29468b2581188c6966a6d6ee9e7f6a87cc2d5650b624700410fa3` |
| `world/WorldBroadcast.java` | `c37429e086de95ce21c67ff86687cca359adcf1150981087987e6694f70b2e17` |
| `model/Instance/L1PcInstance.java` | `0d1f267891c99e662bf0695d799a50e302f42a7f52a0440945372a443ac05c92` |
| `serverpackets/S_OtherCharPacks.java` | `95ee7228e1c9d14d1ae42ec35dbac5fd37166ab2a5ee06f94406960babf99ce5` |
| `serverpackets/S_NPCPack.java` | `dee24b66cd725b3c70a110da0dbfbb61f9e20d6a13b00a798dff046e0251f0aa` |
| `serverpackets/S_RemoveObject.java` | `a3e3e0d61401bf29e702ddfd8055a54abdd170b064da24381748c12763bec5a1` |
| `clientpackets/C_NPCTalk.java` | `7a324af75ce43054f87a500d6a16600ddf01209ee5c5d5d4d2f4cf1377457027` |
| `utils/WorldInteractionGuard.java` | `a72159142ff5964e09b0cbe10b30420a947304de871d084c1d3ad63d1d2bbe8f` |
| `serverpackets/S_NPCTalkReturn.java` | `bfc54936df47f821b54a6acedbe273225e4d53a2b247f21ecf2ce35c653a5847` |
