# T074 離線 6016 回歸

先建置目前 dev 產品；Windows／SDK 10.0.300 執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/effect6016-tests/run.ps1 -Native -AssetsRoot <本機assets路徑> -ConfigFile <NuGet設定路徑>
```

省略 `-Native` 不開音訊裝置；省略 `-AssetsRoot` 不讀遊戲素材。`-Native` 只播放測試在 `scratch/issue-145` 產生的合成正弦波，使用目前產品的 controller／host／AudioApiTransport／DirectShow，另使用 T064 combat factory 的零 PCM session。所有路徑、峰值、事件與 DLL SHA 見同目錄 `result.json`。

`-AssetsRoot` 以目前產品唯讀解析 map4 港口與快取淘汰；不複製素材。CI 只交叉編譯測試，實際音訊與 WinForms 組件檢查必須在 Windows 執行。任何合成測試都不等於正式世界驗收；交接步驟見 `docs/reports/issue-145.md`。
