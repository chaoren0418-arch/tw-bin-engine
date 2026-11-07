# 名稱、稱號、盟徽與名稱顏色離線測試（T168／#440）

`NameDisplayChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL；不啟動產品主程式、視窗、遊戲或網路（沿用 SocketGuard）。所有伺服器封包都經 `WorldSession.Receive`，所有 C72 都經 product 的送出 gate（記憶體 sender）。

依據：`docs/reference/actions-and-names.md` 第 2 節與第 6 節差異表 #7–#15、#19。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/name-display.json name-display
```

CI 的 `map-continuity` 群組與不帶群組的完整回歸都會執行本測試。

涵蓋（本群組 87 項）：

- 名稱顏色：正義值公式三段與兩端夾限；S87 正義值；S34 `D H` 本人／他人、`S_Lawful(objid)` 三個 H 的形式、未知尾段、截斷、非零 padding、未知物件。
- 粉紅名 S60：顏色 `0x767D`、計時到期回正義值色、127 永久、0 立即清除、換世界失效；gfx 2715 非戰爭紅名、戰爭中改回正義值色。
- 決鬥對手、敵對血盟：名稱顏色不變，只改「可直接攻擊」判定；戰爭旗標 +197（2 敵對、1 本盟）。
- 位置：X0＝L＋(W−6·Big5 bytes)/2；名稱 T−3、有稱號時稱號 T−3／名稱 T+10；有血盟 id 名稱 +10、盟徽 X0−20／T−4；隊伍成員 −9；頭上盟徽置中 T−23；全名模式 T−16。
- 繪製規劃：平常只畫滑鼠指到的物件；全名選項畫所有名稱、稱號只給指到的物件、戰爭中的 +196 角色不畫；不出現「[血盟名]」或「決鬥」字樣；失明 1 只留自己。
- 盟徽：TW16 384-byte RGB555（0 透明）解碼；自動 C72 `D(id)` 逐 byte；不重送、最多 32 筆待回覆；回覆釋放名額；無法解析的回覆不再要求；送出 gate 拒絕時釋放名額；新連線釋放舊名額；送出例外斷線不重送；記憶體 512 筆 LRU；換圖／重設後保留；磁碟快取寫到指定的使用者資料路徑（測試用 `scratch/name-display-tests/`）、下一個連線載入、損壞檔忽略。
- 「全部顯示名稱」選項的儲存與讀取。

**不代表真機通過**：字型實際外觀（MingLiU、1 px 外框）、與原版逐像素位置、滑鼠游標圖、實際伺服器盟徽格式都待 dot 真機驗收。
