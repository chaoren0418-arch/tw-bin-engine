# 原版表現查證索引（第四層證據）

使用者 #427 規定：任何「原生表現」（畫面、動作、版面、音效、時序）都要先有本目錄的查證，才能開 code 任務。查證方法照 #417：用 IDA 7.7 與 P01 的 IDB，靜態分析未加殼的 `TW1608262200.bin`，地址標 VA／File Offset。

| 主題 | 文件 | 查證任務 | 涵蓋功能 | 狀態 |
|---|---|---|---|---|
| 逆向資源盤點；B03／B04／B07（list.spr parser、frame 結構、音效選擇、分類讀取者、dispatch tables） | [`rea-inventory.md`](rea-inventory.md) | #417 | B03、B04、B07、T165、T166 | ✅ 已合併 |
| 封包格式（伺服器端） | [`server-protocol.md`](server-protocol.md)、[`server-rules.md`](server-rules.md) | #132 | 全部 | ✅ 已合併 |
| 伺服器原始碼與 d287 JAR 對照 | [`source-vs-jar.md`](source-vs-jar.md) | T123（#226） | 全部 | ✅ 已合併 |
| 動作、表情與名稱繪製 | `actions-and-names.md` | T160（#428） | T128、T129、T132、T135、T137、T146、T152 | 待查 |
| UI 版面與圖示 | `ui-layout.md` | T161（#429） | T106、T108、T124、T125、T130、T131、T138、T150、T152 | 待查 |
| 光源、天氣與地圖事件（日夜曲線、天氣粒子、光源圖、中毒／麻痺／盲目／酒醉／隱身外觀、`^` 循環音規則；引擎差異 21 項） | [`ambience-and-map-events.md`](ambience-and-map-events.md) | T162（#430） | T147、T148、T149、T153、T166 | ✅ 已完成（PR 待合併） |
| 音效播放策略 | `sound.md` | T163（#431） | T105、T165、T166 | 待查 |
| 已完成 M6 功能的原版對照 | `m6-native-diff.md` | T164（#432） | T100–T153 | 等 T160–T163 |

各查證任務合併時，把自己那一列的「狀態」改成 ✅，並補上文件連結。
