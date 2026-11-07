# #361／T144：座標分享

## 結論與做了什麼

**部分完成，不能關閉 T144。** 已完成實際 C254 座標發送、S250 接收與聊天文字呈現；可點擊座標及畫面標示尚未掛接。已詢問是否可在 `PlayerHudChat.cs` 加最小 UI 掛接，尚未取得範圍擴充的回覆；目前產品修改限於原定三個檔案。

- 唯讀查證 `C_Windows`、結構檢查及回包 serializer；正確格式是 C254/sub11，傳給指定角色，並非公開頻道廣播。完整欄位、來源 SHA-256、zone 的證據限制見 [協定](../protocol/location-share.md)。
- 聊天框輸入 `/loc 角色名稱`，透過 `SendLocation` 發送目前角色 map/x/y；不接受任意座標。角色移動後的目前位置可能是既有引擎的本地預測，並非新取得的伺服器 ACK。
- 接收 S250/sub111，保存發件者、map/x/y/zone；以 `[座標] 名稱：地圖 … (x, y)` 顯示。S71/1783 與 1782 分別顯示成功及離線失敗；不產生虛構的本機座標回聲。
- 共用聊天 sender gate、傳送中鎖及節流；排隊期間換圖、換角、死亡、動作鎖或位置改變皆拒送。其他 S250 與 S71 保持原分派。
- 測試提交至 repo，接入既有 `DemoChecks` 的 `location-share`、預設組及 CI 已執行的 `map-continuity` 組；未修改 workflow。

## 已驗證

| 檢查 | 結果 |
|---|---|
| dev `rebuild.py --update-lock` | 183 個來源檔／192 個鎖定輸入通過；僅更新 dev lock |
| dev `rebuild.py --build` | product／audio／launcher 成功；產品 0 errors，既有 `MagicUi.cs:20` 兩項 nullable warnings |
| `location-share-tests/run.ps1` | **86 項通過**；真實產品 DLL、合成封包、記憶體 sender，無遊戲入口／網路連線 |
| 既有 `player-chat-tests` | **107 項通過**，包含未顯示的聊天控制項回歸 |
| `check-no-game-assets.py` | 通過，0 errors |
| CI | 開草稿 PR 後追蹤；結果另補於本報告 |

離線測試涵蓋 C254 精確位元組與 CP950、S250 的全部欄位及整數邊界、S71 兩種回覆、每個封包長度的截斷、參數數量及 padding 拒絕、錯誤包不更新歷史、其他 subtype 分派不變、100 筆上限、reset、共用節流、舊世代／移動／死亡／麻痺拒送、無逐步 ACK 時使用目前位置。

重現順序：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-361/build
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/location-share-tests/run.ps1
python -B -X utf8 engine/tools/check-no-game-assets.py
```

本機 `python` 不在 PATH，使用已安裝的 Python 3.12 完整路徑。NuGet 線上還原遇到 TLS 認證錯誤，改用 `scratch/issue-361/nuget.config` 唯讀指向原專案 `engine/vendor`；套件快取、暫存、建置輸出全部留在 worktree。檢查 CRLF 來源的空白使用 `git -c core.whitespace=cr-at-eol diff --check`，保留原檔換行格式。

## 未驗證／未完成

- **座標點擊與位置標示未完成。** 目前只有文字及型別資料，不能宣稱完整 UI 功能。
- 未啟動客戶端、遊戲、伺服器或 DB，未連任何遊戲服務。未驗證跨客戶端實際投遞與原生 UI 操作。
- zone 僅證明為透明轉送欄位；送出 0 的原客戶端顯示效果待 smoke，不能推斷為地圖分流或安全區。

## 產出路徑

- `engine/source/LocationShare.cs`、`PlayerChat.cs`、`WorldSession.cs`
- `engine/_integration/dev/location-share-tests/LocationShareChecks.cs`、`run.ps1`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs` 測試掛接
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/location-share.md`、本報告
- 本機證據（不入 git）：`scratch/issue-361/location-share.json`、`player-chat.json`、`build/result.json` 與建置紀錄

## 給下一棒的提示

1. 調度器／Claude 確認是否允許修改 `PlayerHudChat.cs` 的最小 UI 掛接；其中 `RichTextBox` 設定 `DetectUrls=false`，只改資料層無法合理完成點擊互動。建議讓 `LocationShare.cs` 承擔座標點擊狀態、繪製與世代清理，聊天 UI 僅加初始化及更新呼叫；避免反射或全域事件鉤子的迂迴實作。
2. UI 完成後補同圖／跨圖／視野外、換圖換角清標記與普通文字不可偽造位置的離線測試，再轉正式 PR；此草稿不關閉 #361。
3. 原生雙角色投遞與 zone 互通由 dot 依既有 P24Lab 授權於後續 smoke 驗證。
