# 求婚與結婚（T141／#358）

## 已驗證的 wire 契約

依 [server-protocol.md](../reference/server-protocol.md)、[server-rules.md](../reference/server-rules.md) 與 [source-vs-jar.md](../reference/source-vs-jar.md)，唯讀核對 d287 JAR 的 `C_Propose.start`、`C_Attr.start`（654 分支）、`S_Message_YN(int,String)`、`S_ServerMessage`。未執行伺服器。下列為明文 payload；C／H／D 是 1／2／4 bytes，H／D little-endian，S 是 CP950 加 NUL；現有傳輸層補零至 8 bytes。

| 方向 | 格式 | 意義 |
|---|---|---|
| C50 | `C50 C0` | 求婚；目標由伺服器 FaceToFace 決定，不送物件 ID 或姓名 |
| S219 | `C219 H0 D0 H654 S(求婚者姓名)` | 被求婚者的 Yes-No 邀請，沒有參數個數 C |
| C121 | `C121 H0 D0 H654 C(answer)` | answer=1 接受、0 拒絕；D 固定 0，非 requestId，沒有 confirmation ticket |
| S71 | `C71 H655 C1 S(伴侶姓名)` | 雙方均會收到的成功結果，據此顯示伴侶 |
| S71 | `C71 H656 C1 S(拒絕者姓名)` | 求婚方收到拒絕；拒絕方沒有額外確認回包 |
| S71 | `C71 H790 C0` | 婚禮完成文字，不含伴侶身分；不能單獨建立伴侶名稱 |
| S71 | `C71 H657 C0`／`H658`／`H661` | 自己已婚／對方已婚／對象性別不符 |

C121 範例（含補零）：接受 `790000000000008E0201000000000000`；拒絕 `790000000000008E0200000000000000`。C50 求婚為 `3200000000000000`。不實作 C50 子型別 1 的離婚流程，S219／653 保留既有未處理路徑。

d287 `C_Attr` 先讀 H；若為 0 再略過 D、讀訊息 H，接著 654 分支讀 C。取角色 TempID 所指的求婚者並清除 TempID；接受時先設定／儲存一方 partnerId，送 790、655，再設定／儲存另一方並送相同訊息。客戶端不能推論這些儲存是跨角色的原子交易。

## 伺服器前置條件：地點與戒指

- `C_Propose` 子型別 0 先拒絕 ghost；透過 `FaceToFace.faceToFace` 找相鄰一格、互相面向的玩家。來源 FaceToFace 以八方向比對位置及反向 heading，無人／未面對時可回 93／91。這些通用 S71 保留既有訊息分派。
- 雙方 partnerId 必須為 0，性別必須不同，否則分別回 657／658／661。
- **雙方都在 map 4，X=33974..33976、Y=33362..33365，含邊界。** 地點不符或沒有可用目標時不保證另有求婚專屬失敗回包。
- **本次核對的 d287 求婚與接受分支沒有戒指持有、裝備、數量檢查，也沒有消耗或發給戒指。** 不把其他版本的結婚戒指規則加到客戶端。離婚提示提及戒指，不是此求婚路徑檢查戒指的證據。
- 654 接受分支只依 TempID 找求婚者；未再次核對上述地點、未婚、性別條件。其他 Yes-No 也使用 TempID，因此客戶端收到替代確認後會使舊求婚邀請失效。
- 未讀取秘密設定、DB 或物品模板；上述是 handler 條件，不代表已查證正式服或運作中的程序。

## 客戶端狀態與操作

原生視窗右上角「求婚／伴侶」開啟模組視窗。選取婚禮區域內相鄰且面對面的角色後，按「向選取角色求婚」送一次 C50。收到合法 S219／654 時會顯示邀請，可按「接受」或「拒絕」；關閉視窗不等於送拒絕。只有 S71／655 才更新伴侶名稱與成功文字。

所有實作放在 `engine/source/Marriage.cs`；`Trade.cs` 只加一個 Yes-No 模組分支，`WorldSession.cs` 只加一個 S71 回應 case，非本模組訊息照原路徑處理。新增 UI 利用同檔 `ClientForm.OnLoad`，未修改共用 HUD／web 分派。

本地防護包括連線世代、世界 epoch、邀請 revision、死亡、行動鎖、交易／移動忙碌、鄰近對象與求婚前的面向快照。傳輸 admission 再核一次；已送出不自動重試，不預扣道具。等待 15 秒逾時或角色離開時使舊邀請失效；這是客戶端保守政策，非伺服器取消協議。

**目前伴侶名稱是此世界 epoch 內收到的成功結果。** 切圖、切角或重連後清除本地結果，顯示需由伺服器重新確認；不把「未知伴侶」當成未婚，也不從磁碟快取猜測婚姻狀態。本任務未找到可用的登入伴侶查詢封包；後續若需跨地圖常駐伴侶欄位，須另核身分生命週期與查詢契約。

## 證據身分

來源根：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。只記錄結論與 SHA-256，不複製伺服器原始碼。JAR 路徑為 `D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。使用 Microsoft JDK 17 的 `javap -p -c` 唯讀核對方法指令；本文件沒有使用 BIN 地址。

| 相對來源檔 | SHA-256 |
|---|---|
| `clientpackets/C_Propose.java` | `81d8dd895063670d40f03b4712423f13d06fbccf672b43fac1b25ee066edf70d` |
| `clientpackets/C_Attr.java` | `793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23` |
| `serverpackets/S_Message_YN.java` | `f5c6bad673e5af10c851fd6206624597c2514fb88b98fc1f3e574e935c6d0c45` |
| `serverpackets/S_ServerMessage.java` | `6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92` |
| `utils/FaceToFace.java` | `c6235d8150ee64fe890e8c5ad12c25623829cd8848e6efa1e40432db68ed7ce3` |

## 離線驗證與待驗證

測試提交於 `engine/_integration/dev/marriage-tests/MarriageChecks.cs`，連結既有 DemoChecks，支援 `marriage` 組，亦加入 CI 已使用的 `map-continuity` 組及預設完整測試。以產品 DLL、合成封包、記憶體 sender 與 SocketGuard 驗證 C／S byte 格式、狀態、錯包與過期送出；不呼叫產品入口、不開視窗、不連網路。

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-358/marriage.json marriage
```

待 dot 在後續授權 P24Lab smoke 核對實際按鈕與邀請視窗、兩角色求婚／拒絕／成功名稱、地點錯誤、已婚與同性別拒絕、切圖與重連清除。本任務不執行遊戲、伺服器或 DB。
