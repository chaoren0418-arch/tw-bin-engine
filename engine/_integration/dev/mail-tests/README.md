# T131 信件離線檢查

使用已建置的真正 `P11.ClientEngine.dll`；合成 C／S 封包與記憶體傳送閘門，不開遊戲、不連線、不存取 DB。UI 測試僅建立與釋放未顯示的控制項。

先依 dev 建置流程執行 `rebuild.py --update-lock` 及 `rebuild.py --build`。在 Windows repo 根目錄執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/mail-tests/run.ps1
```

若 worktree 未配置離線 vendor，可傳 `-NuGetConfig <本機唯讀套件來源的 config>`。腳本把暫存及 .NET home 放在 worktree 的 `scratch/issue-267/`，套件放 `engine/.nuget`；編譯產物在測試目錄的 bin／obj。

已接入 `.github/workflows/build.yml` 的 Windows `map-continuity` job，產品編譯後執行。測項包括：

- 全部 C87 與 S186 格式、ID 小端序、混合 CP950／UTF-16LE、日期、空字串、換行及補充平面字元。
- 不同名稱 byte 奇偶、原始寄信 payload 的伺服器偶數條件、非對齊封包加解密、長度邊界、NUL／無效 surrogate／無法編碼收件人。
- 畸形數量、重複 ID、截斷、缺結尾、異常 padding、未知子型別及不合法結果。
- 未收到 ACK 不更新讀取／刪除狀態、寄出不冒充成功、滿箱失敗、世界重設／斷線、重複排隊、過期 ID、視窗關閉及傳送失敗不重試。
- HUD 單一掛載點、可換行寫信區、唯讀讀信區與真實 WorldSession 清單繫結。
