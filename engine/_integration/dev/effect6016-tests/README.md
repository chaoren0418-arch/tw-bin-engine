# T074 離線 6016 回歸

先建置目前 dev 產品；Windows／SDK 10.0.300 執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/effect6016-tests/run.ps1 -Native -AssetsRoot <本機assets路徑> -ConfigFile <NuGet設定路徑>
```

省略 `-Native` 不開音訊裝置；省略 `-AssetsRoot` 不讀遊戲素材。`-Native` 只播放測試在 `scratch/issue-145` 產生的合成正弦波，使用目前產品的 controller／host／AudioApiTransport／DirectShow，另使用 T064 combat factory 的零 PCM session。所有路徑、峰值、事件與 DLL SHA 見同目錄 `result.json`。

T460（#1045）起產品不再讀 `assets/audio-manifest.json`：1340／3332 由 `TW_CLIENT_ROOT/Sound/<id>.wav` 直讀並驗程式內 pin，host 以版本字串 `p24-audio-assets-v3-client-sound` 對契約。`-Native` 在 `scratch/issue-145/synthetic-client/Sound/` 產生合成 WAV，先以產品工廠（`AudioPlayer.TryCreate`）檢查契約與根目錄、確認舊版本字串被拒，再以同一條 DirectShow 鏈加合成 pin 播放。直讀、缺檔、pin 不符與 fallback 的離線測試在 DemoChecks 群組 `audio-provider`。

`-AssetsRoot` 以目前產品唯讀解析 map4 港口與快取淘汰；不複製素材。CI 只交叉編譯測試，實際音訊與 WinForms 組件檢查必須在 Windows 執行。任何合成測試都不等於正式世界驗收；交接步驟見 `docs/reports/issue-145.md`。
