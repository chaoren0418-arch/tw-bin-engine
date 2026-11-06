# 傳送 recovery wrapper（T110／#198）

## 來源與範圍

依 #132 `docs/reference/server-protocol.md` 差異 1 與 recovery wrapper 說明，唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server\` 下列來源（2026-11-07）；未複製來源、啟動服務或讀取機密設定。

| 相對來源 | SHA-256 | 核對處 |
|---|---|---|
| `utils/Teleportation.java` | `2108857a47e620d35fa06b2b6dfc0ff867dcd97a913fcc57616565ba8a27e38b` | 153–167 行 recovery 呼叫者；175–193 行 wrapper |
| `serverpackets/ServerBasePacket.java` | `3348c199e9694bd1eec050b130f28ab99590cae16f0047964c17b32df309a439` | 210–220 行 `writeByte`；334–352 行 `getBytes` |
| `serverpackets/S_OwnCharPack.java` | `6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26` | `buildPacket` 的字串、PC flag、空 owner 與固定尾段 |
| `model/item/ClassicBadgeService.java` | `8387922a14c45e6fbb86ac67c3f860c3db5adeed1f5b9071b5ae80dae2490e0d` | 55–59 行 `syncSelf` 發出徽章 reset／自身更新 |

`S_Paralysis`、`S_MapID`、`S_CharVisualUpdate`、`S_ClassicBadge` 欄位另見 #132 對照表。上述數字是來源行號，非 BIN File Offset／RVA／VA。

## 尾端契約

設解析完成的欄位長度為 L，原封包長度 N = ceil(L / 8) × 8。伺服器先取得完整且已補齊的 payload，`writeByte` 追加一個 00，再由 `getBytes` 補齊；結果為 **原 N bytes + 8 個 00**。

`LoginReader.Finish` 保留原本 N-byte 路徑，只對下列已核對的解析欄位形狀接受 N+8。兩種路徑都逐 byte 檢查尾端必須為零；不接受 N+1…N+7、N+9、N+16、非零原 padding 或非零 wrapper。

| 封包 | N+8 的額外限制 |
|---|---|
| S202 解鎖 | L=2，mode 必須為 7 |
| S206 地圖 | L=4，underwater 必須為 0 或 1；不納入另一個 `S_MapID(int)` overload |
| S87 自身角色格式 | 固定前綴 21 bytes、name/title 兩個零結尾字串、含 PC flag 的 status、D clanId、零結尾 clan、空 owner、七個尾欄位；heading≤7，最後四欄為 `00 00 FF FF`，L 必須恰好到欄位結尾 |
| S119 外觀 | L=6，沿用既有 objectId／weapon 語義 |

wrapper 沒有專用 opcode 或標記，因此只能辨認符合以上格式的 wire 變體，無法由 bytes 證明發送來源或 S87 是本人；自身身分仍由既有 `WorldSession` 依 objectId 判斷。相同 wire 形狀的其他人物不會被當作自身套用。未增加依賴前一包或本機 pending 狀態的限制，避免伺服器尚未送 S149 就中止時無法恢復。

S149 初始化並未出現在 recovery 包裝清單，仍拒絕額外 8 bytes。其他 opcode 與 S202 其他 mode 的 `Finish` 行為不變。

伺服器也包裝 `S_ClassicBadge`（S250／FF34），但本引擎目前未實作徽章接收器，不呼叫 `Finish`，仍走既有未支援封包處理；本次不宣稱已實作徽章，也不替 S250 加通用例外。

## 離線向量

- 解鎖原包：`CA07000000000000`（8 bytes）。
- recovery 解鎖：`CA070000000000000000000000000000`（16 bytes）。
- 測試獨立實作兩段補齊模型，對四種封包驗證 N 與 N+8；S87 名稱長度變化覆蓋全部 8 種 padding 餘數。
- 執行本次產品 DLL 的實際接收路徑：S149 → S202/7 → S206 → S87 → S119，確認只收到 unlock／map 時仍鎖定，收到自身角色後恢復，外觀更新生效。
- 測試路徑：`engine/_integration/dev/teleport-tests/`；只使用合成資料，不開 socket、不讀遊戲資源，不代表真機驗收。
