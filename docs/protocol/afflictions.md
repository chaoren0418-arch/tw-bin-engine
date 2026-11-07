# T148 異常狀態協定（#388）

依據 `docs/reference/server-protocol.md` 的 S_Poison／S_Paralysis／S_CurseBlind，以及 `server-rules.md` R08、R17、R24、R38。另唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server\serverpackets\` 的三個 serializer；未複製其原始碼。

| 來源檔 | SHA-256 |
|---|---|
| S_Poison.java | `25e4d654db8f1faec19e0b8cf87f979315a3a1297a94f481014603b2cf5c9be3` |
| S_Paralysis.java | `7e57e26309fa5e8221a189ce7c82c64be5d03ee9171d9be8d0d74fb4abf2146a` |
| S_CurseBlind.java | `cf6288f4245c94b02cbc91cf420fdf5e7a8bd9794b77acbbc30e05bea25947bf` |

## 線路格式與處理

H／D 是 little endian；沿用 `LoginReader.Finish()` 的 8-byte 零補齊檢查。解析完整且合法才變更狀態。本模組不新增 C 封包，也不送出額外 ACK。

| 封包 | 欄位 | 語意 |
|---|---|---|
| S165 | C165, D(objectId), C(green), C(gray) | 00/00 正常；01/00 綠色；00/01 灰色。其他旗標組合及零 ID 拒絕。未知物件忽略。 |
| S202 | C202, C(mode) | 施加／解除配對見下表。未知 mode 沿用未處理計數，不解除既有鎖。 |
| S47 | C47, H(type) | 0 解除；1 自身可見；2 周邊物件可見。大於 2 拒絕。 |

| 狀態 | 施加 mode | 解除 mode |
|---|---|---|
| 麻痺 | 2 | 3 |
| 第二種麻痺 | 4 | 5 |
| 傳送鎖 | 6 | 7 |
| 睡眠 | 10 | 11 |
| 凍結 | 12 | 13 |
| 暈眩 | 22 | 23 |
| 束縛 | 24 | 25 |

S202 分派轉交既有 `ReceiveTeleport`，沿用其獨立鎖集合及 `ActionLocked`。解除其中一項不解除其他項；7 只移除傳送鎖，傳送尚未完成時仍進入 recovery。S202/7 額外 8-byte recovery wrapper 仍可接受，其餘格式不接受額外補塊。S202 的 case 會提早返回，因此 `ReceiveParalysis` 必須自行呼叫 `BasicSupplyReply`：吃肉／回家卷軸被原服拒絕時，S202/7 是解除補給品等待鎖的唯一回包。

異常狀態首次鎖住輸入時遞增既有 `AppearanceRevision`，讓送出閘門與 UI 清除先前輸入；即使施加與解除都發生在排隊請求送出前，舊移動／攻擊也不會復活。鍵盤查詢提供異常狀態提示；滑鼠、網頁與連線 API 使用既有共同動作鎖。

## 狀態生命週期與呈現政策

- S87 的 poison bit 可初始化綠色外觀，S165 明確值優先；顏色只作用於角色像素，不改共用 Tile、陰影、深度與點選資料。
- 已移除物件及切圖前的周邊物件不保留染色；自身狀態跨切圖保留。`WorldSession.Reset()` 經既有 `OpacityObserved` 生命週期清除染色／盲目，麻痺集合由既有 reset 清除。這兩個事件在第一個 S165／S47 才訂閱（`ObserveAfflictions`）：`WorldSession` 的無參數建構子已由 `Ambience.cs` 使用，而染色與盲目只由這兩種封包寫入，訂閱前沒有狀態要清。
- `ComposeAfflictionPatch` 取代了原本的合成迴圈，所以 S171 隱身的 `InvisibilityOpacity` 也在這裡套用，並傳入染色前的原始 `Draw`。之後改合成流程時要保留這個呼叫。
- 綠色／灰色以 RGB555 明度轉換呈現；盲目 1 將自身以外的場景像素轉黑，盲目 2 將場景亮度降為四分之一。解除後直接用未修改來源重新合成，避免累積變暗。
- 盲目合成範圍涵蓋完整 renderer Bounds，以包含角色 patch 外的地圖與船背景；原生及網頁場景共用結果。HUD 不變。這會增加盲目時的合成成本，實機流暢度留待 smoke 觀測。
- 色彩轉換與變暗比例屬客戶端呈現政策，並非已核對舊 BIN 的調色盤或逐像素一致性；狀態圖示維持 T108。

## 離線驗證入口

先依 dev 流程 `--update-lock`、`--build`，再執行：

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-388/afflictions.json afflictions
```

測試位於 `engine/_integration/dev/afflictions-tests/AfflictionChecks.cs`，執行實際產品 DLL，使用合成封包、手寫 RGB555 像素與記憶體 sender；SocketGuard 禁止網路。不建立遊戲視窗、不登入、不接 DB。Windows CI 的 `Affliction contracts` 步驟執行相同入口。
