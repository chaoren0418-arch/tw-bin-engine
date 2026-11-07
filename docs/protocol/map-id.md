# S206 地圖 overload 契約（#217／T122）

依據 #132 差異 16 與 `docs/reference/server-protocol.md` 的 S_MapID 表。2026-11-07 唯讀查閱 `D:\lineage開發\1-伺服器\src`，沒有複製伺服器原始碼。

## 使用點與來源

搜尋 Java 來源的 `new S_MapID(...)`，共八個建構使用點；唯一單參數使用點為 `com/lineage/server/command/executor/L1MoveX.java:53`。這是 GM 測試未知地圖的移動指令：成功搬移後，先將 mapid 存入 temp ID，再傳單參數 S206，隨後傳自身角色與外觀。其餘七處在 AutoHuntControl、C_LoginToServer、L1PcInstance（兩處）、Teleportation（三處），使用二或三參數版。沒有執行指令，也未確認目前部署是否啟用此 GM 命令。

| 來源（相對上述 src） | SHA-256 |
|---|---|
| `com/lineage/server/serverpackets/S_MapID.java` | `e4f8e0f4d4b98bdc90333233a9daba53ffee547881905956ef2e1cb33e28fb68` |
| `com/lineage/server/command/executor/L1MoveX.java` | `a2b7104c480c67f093c136a5a9ed408b9ce5b668087787efec7394b3770dc9c8` |

## 接收格式

以下長度包含 opcode，不含傳輸 framing；H 為 little-endian。

| 變體 | 完整解密封包 | 長度 |
|---|---|---|
| 一般 `(int, boolean)`，三參數版同 wire 格式 | `CE H(map) C(water) 00 00 00 00` | 8 bytes |
| 單參數 `(int)` | `CE H(map) 00 10 F8 00 00 00 00 00 00 10 0D 35 C5` | 16 bytes |
| 一般版 recovery wrapper（main 的 #198） | `CE H(map) C(water=0或1)` 加 12 個 `00` | 16 bytes |

單參數版 water 必須是 00，後面 12 bytes 必須逐 byte 相符；不能當作可忽略的 padding。一般版仍沿用既有 water 非零判真及零 padding 驗證。其他長度、截斷、錯誤常數與額外 bytes 一律拒絕，驗證完整後才清除舊世界並更新地圖；同圖重設仍清除場景。

離線向量與負例在 `engine/_integration/dev/demo-tests/MapIdChecks.cs`，掛接現有 `map-continuity` 入口（CI 的 Windows job 已執行此入口）。覆蓋兩種 overload、乾／水地圖、同圖清場、所有尾段單 byte 替換、非零 water、截斷、額外 bytes，以及拒絕時世界狀態不變。這些是合成測試，並非真機證據。

## 第 2 輪同步：保留既有 recovery 格式

合併 main 後，依 [teleport.md](teleport.md) 的 #198 契約，一般版已允許 N+8 的 recovery wrapper。它與單參數版同為 16 bytes，必須以完整尾段區分：單參數版只接受上述非零常數；recovery 只接受 water=0 或 1 且 12-byte 尾段全零。其餘內容拒絕，不接受單參數版再加 wrapper 或重複 wrapper。兩種 16-byte 路徑均在完整驗證後才更動世界。

第 2 輪向量新增乾／水 recovery、每個尾段 byte 的全部非零變異、water=2..255 及 recovery 加長負例。另重跑既有 `teleport-tests` 的完整 recovery 流程；不修改傳送模組或共用 `LoginReader`。
