# Issue #429 報告：T161 查證原版 UI 版面與圖示

## 做了什麼

- 把 P01 的 IDB（SHA `3a0bf206…7e82f`，已核對）複製到 `scratch/rea/ida/` 後用 IDA 7.7＋Hex-Rays 唯讀查詢。分析對象是 `TW1608262200.bin`（SHA `e12949cf…641a2`，已核對）。
- 從 BIN 找到 UI XML 的解密方式：AES-128-CBC，見 `sub_B3D710`、`sub_A9FA30`、`sub_4D8FB0`。解開本機 `Tile.pak` 的 65 個 UI XML，只輸出到 `scratch/`。
- 讀取主介面、隊伍、書籤、地圖系統的 XML 座標，並從 `Sprite*.pak` 的 `.img` 標頭讀出各圖尺寸。另外把圖渲染到 `scratch/` 目視核對版面。
- 從 RTTI 和工廠 `sub_6BC550` 取得 16 個聊天 Printer 類別，整理出各聊天類型的顯示格式、調色盤索引與顏色。輸入前綴字元集合讀自 `sub_7DE200`。
- 用 `TW13081901.unpacked.exe` 交叉驗證：
  - UI XML 機制在 TW13 已存在。
  - 調色盤前 77 筆逐 byte 相同。
  - 聊天 Printer 類別在 TW13 沒有，是之後才改寫的。
- 產出 `docs/reference/ui-layout.md`，最後一節對照 `engine/source/` 列出 17 項差異，並更新 `docs/reference/INDEX.md`。

## 怎麼驗證

- 文件中每個地址都標了 VA 與 File Offset（區段換算規則寫在文件開頭）。偽碼取自 IDA 輸出，查詢清單在 `rea-scripts/ui_queries.json`。
- 解密結果：65 個 XML 解開後開頭都是 `<?xml`，內容可以直接讀，而且 surf 編號與 `Sprite*.pak` 的圖檔都對得上。
- `.img` 格式：抽樣的每個檔案都符合 8＋寬×高×2＝檔案大小。
- 合成圖：依 XML 座標把底列各區塊貼回 800×600，結果是完整的一條狀態列，右巨集框上印著 F5–F12。

## 未驗證什麼

- HP／MP 浮動窗的座標、尺寸、顏色。XML 註解寫明這部分寫死在程式碼中。
- 紅字訊息的位置。
- 舊封包對應的狀態圖示 surf 編號與排列。新版改由伺服器以 `SC_SPELL_BUFF_NOTI` 指定，但 d287 伺服器不會送這種封包。
- 前綴字元→頻道類型的對照目前只是推測：`!` 喊話、`&` 全體、`@` 血盟、`#` 隊伍、`$` 交易。
- 小地圖的快捷鍵，以及底列小地圖格畫的是什麼內容。
- 公告板是否就是 `HYPER_TEXT_WINDOW`。
- 信件版面：`RenewalMailUI.xml` 在本機 `Tile.pak` 中缺檔。
- 沒有啟動遊戲做真機比對。

## 產出路徑

- `docs/reference/ui-layout.md`
- `docs/reference/INDEX.md`：更新 T161 這一列
- `docs/reference/rea-scripts/ui_xml_decrypt.py`、`ui_img_dims.py`、`ui_img_render.py`、`ui_ida_query.py`、`run_ui_query.ps1`、`ui_queries.json`

## 給下一棒的提示

- T164（M6 原版對照）可以直接引用 `ui-layout.md` 第 7 節。最大的三項差異是：
  - 引擎不是 800×600 版面
  - 快捷列只有 F1–F4，原版是 F5–F12 共 8 格
  - 小地圖用 F5 開關，與原版巨集鍵衝突
- 產品若要還原原版版面，可以在執行期從 `Tile.pak` 解密讀取 XML 與圖檔，不必把資源複製進專案。
- **共用 IDA 時的注意事項：** 各任務的 subst 磁碟代號會互撞。本輪一度在 `W:` 被 task-431 占用時卸載了它的對應，已立即依原路徑恢復，該 idat 程序未中斷。新的執行器 `run_ui_query.ps1` 改用 `K:`，磁碟被占用時直接退出，不再卸載別人的對應。建議其他任務的執行器也加同樣的檢查。
