# T173 音效共用層檢查（#447）

先在 repo 根目錄執行 `python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`，產生 `engine/build/client-product-dev` 的產品 DLL，再執行：

```powershell
dotnet restore engine/_integration/dev/sound-rules-tests/SoundRulesChecks.csproj --configfile .github/nuget.ci.config
dotnet build engine/_integration/dev/sound-rules-tests/SoundRulesChecks.csproj -c Release --no-restore -o scratch/issue-447/tests/bin
dotnet scratch/issue-447/tests/bin/SoundRulesChecks.dll scratch/issue-447/checks.json
```

CI 入口：`.github/workflows/build.yml` 的 `map-continuity` job，步驟「Sound rules, S22 mode and packet media checks」。

涵蓋範圍（全部是合成資料，不讀遊戲素材、不開網路、不碰原生音訊）：

- 距離音量 `100 − 14·(nx²+ny²)/20`、曼哈頓距離 > 14 為 0：sound.md 換算表逐點，加上 −30..30 窮舉。
- 200ms 冷卻：每 id 一份、跨角色、跨管線（影格音效與 S22 共用）。
- S22 `C(mode) H(sound)`：模式 0 單次、同 id 播放中不重播；模式 1 環境音循環（EnvSound 類別，冷卻只在啟動）；模式 2 停止；3..255 忽略；截斷與非零 padding 拒收。
- 4 類音量（Sound／EnvSound／Voice／Music）各自可調；三個效果類別全為 0 時不播（A06020）。
- `CombatSfxController` 經 `SoundGate` 派送時帶出每個 voice 的音量倍率；`AudioCore` 以「主音量 × voice 倍率」設定原生音量（假 driver），改主音量時保留倍率。

`Core()` 的 1 byte 合成素材寫在 `scratch/issue-447/voice-scale/`，必須從 `scratch/` 底下執行。
