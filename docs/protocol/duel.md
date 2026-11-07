# 決鬥封包補充（T132／#272）

本文件依 `docs/reference/server-protocol.md`、`server-rules.md`，以及 2026-11-07 唯讀查閱本機伺服器處理器整理；沒有複製伺服器原始碼。C/S 數字為十進位 opcode，H/D 為 little-endian，S 為 CP950 NUL 結尾字串；實際明文補零至 8 bytes 倍數。

| 方向 | 格式（含 opcode） | 語意 |
|---|---|---|
| C5 | `C5` | 無尾段，不帶 target ID。伺服器 FaceToFace 找相鄰、互相面向的玩家。 |
| S219 | `C219 H0 D0 H630 S(name)` | 決鬥邀請；D0 不是可自訂的交易 challenge。 |
| C121 | `C121 H0 D0 H630 C(answer)` | 0 拒絕、1 接受；採 C_Attr 支援的完整 envelope。 |
| S250/5 | `C250 C5 D(opponent) D(self)` | 非零 ID 且 self 為本角色時開始；`D0 D0` 結束。 |
| S71 | `C71 H(message) C(count) S(args)...` | 631 帶拒絕者名稱；633 自己已在決鬥、634 對方已在決鬥，均無參數；91 帶面前角色名稱、93 無參數。 |

接受或送出邀請均不代表決鬥已開始。C_Attr 接受分支對雙方送 S250/5；拒絕只對邀請者送 631。L1PcInstance 決鬥死亡處理送雙方 `S250/5 D0 D0`。633／634 不提供對手 ID，不能據此自行設置對手標記。

客戶端只用 S250/5 更新「決鬥對手」標記，不更改 Combat、技能或 PvP 的准入規則。相鄰／面向與可見目標檢查只限制邀請操作；玩家身分、安全區、是否能攻擊與傷害結果仍由伺服器處理。

## 本機狀態政策

- 邀請、回答使用 world epoch、revision 與 session generation，在傳送閘門內重新核對；送出失敗斷線，不重送。
- 其他 Yes-No 對話會使未回答的決鬥邀請失效。重新收到 630 會更新 revision，舊按鈕不能回答新邀請。
- 協議沒有獨立邀請 token、取消邀請指令或邀請方成功收件回覆。不推測成功、也不設定自動重送；等待時可登出清除本機狀態。
- 世界重建、換圖與斷線使本機標記失效；這是顯示失效政策，不宣稱伺服器已結束決鬥。若伺服器未補送 S250/5，標記不自行恢復。
- S71 仍交由原有訊息流程顯示；非決鬥 S219／S250 子型保持原分派。
- 正常入口「服務 → 決鬥」可邀請；收到邀請會開啟該頁。標記採客戶端文字呈現，不宣稱與原 BIN 配色相同。

## 唯讀來源 SHA-256

以下相對於 `D:\lineage開發\1-伺服器\src\com\lineage\server`；本任務不涉及 File Offset／RVA／VA。

| 來源 | SHA-256 |
|---|---|
| `clientpackets/C_Fight.java` | `c2b6e5057d65b8c7e80f1552b4183361ac625b9fcb65c47f10ec836f275ff323` |
| `clientpackets/C_Attr.java` | `793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23` |
| `serverpackets/S_Message_YN.java` | `f5c6bad673e5af10c851fd6206624597c2514fb88b98fc1f3e574e935c6d0c45` |
| `serverpackets/S_PacketBox.java` | `817ba5fd5b8ada480c6cef6d0529b9cb8ac8c6a33d84af550f8479a31fcce977` |
| `model/Instance/L1PcInstance.java` | `0d1f267891c99e662bf0695d799a50e302f42a7f52a0440945372a443ac05c92` |
| `utils/FaceToFace.java` | `c6235d8150ee64fe890e8c5ad12c25623829cd8848e6efa1e40432db68ed7ce3` |
