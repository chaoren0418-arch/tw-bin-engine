# r28 正式整合交付

本輪已完成獨立 r28 portable delivery，待主管唯一執行正常 CMD 及整套原生流程。沒有操作 GUI／COM／遊戲／播放／登入／服務／DB，也沒有改寫 r27 或任何候選／主管／原始資產。本輪可重建與離線封存不等於 P24 完成。

## 固定版本

- 產品 DLL SHA-256：`7762951fba9a2d37d08e2d40a34dfa7f2a6f2219b93689a031a566a8fd8f8cb8`。
- 音訊 DLL SHA-256：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。
- 產品 manifest SHA-256：`6199d69faa708ee03994fc97de670a4e44cdcebed8981dce72c51a799890cabe`。
- Launcher DLL SHA-256：`3ac812b20eb651925126d6fac8c255659927065251e84b8c739e21e818469e26`。
- 最終 source/web/audio 全部逐檔與接受 host-r3 相同；兩個 DLL 與主管接受並獨立重建的 candidate 完全一致。
- 正常同名「啟動新版客戶端.cmd」位元組與 r27 相同，但本目錄 launcher 固定指向 build/client-product-r28/P11.ClientEngine.exe --normal-product；工作目錄為本 delivery，autoLogin=false。沒有改寫、取代或重新導向原 r27 入口。

## 基準、接受與精確差異

r27 基準 seal `a333cc86191f23bc72590f5b2d6f2722339f8a90e769b64c1a2315d3b8e66f07`。baseline-pins.json 保留原 product/input/runtime/assets pins；起始核對非資產產品 pins，完整資產本文在最終包只重算一遍。原版 runtime/runs、舊 build 與舊 integration evidence 未複製。

主管 host-r3-review/root-review.json 明確接受正式整合，封存候選 `f322c11e45d9b0fd64048c7fb0d464aa7fc76c1fc0c3b334efac6122b0928915`。本整合完整核對 host-r1 374、host-r2 321、host-r3 307 pins，逐步驗 before/after delta，證明最後 source/web/audio 無遺漏累積修正。source-lineage.json 保存鏈與接受收據；references 包含三版 seal/delta/result/HANDOFF、主管重建及獨立覆核證據。F1/F2/F3/F3b/F4 已接受，原生 host 尚未驗收。

相對 r27 輸入差異精確 34 檔，見 changed-files.json / delta/ / changes.diff。包括接受候選累積功能、3 個原音訊檔、integrity 更新、r28 版本腳本／說明與 launcher DLL；生成的 r28 build 與整合證據由完整 seal 另行收錄。全部其他 source/web/vendor/舊資產保持一致。

assets/integrity.json 原 13,954 列順序／size／SHA 保留，只附加 audio-manifest.json、sound/1340.wav、sound/3332.wav，共 13,957 pins。原 audio manifest SHA `87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe` 與 WAV bytes 保留；沒有補造 MP3，也沒有宣稱 Miles 距離、pan、context 或衰減等價。

## 本次真正最終包驗證

產品、launcher、三個 managed helper 建置皆 0 warning / 0 error。所有 helper 直接引用本 delivery 最終 DLL，並逐一核對複製依賴 SHA；真實 map parser 測試讀本包 assets。宿主22cases/77checks、音訊設定7cases/39checks、實際 panel.js 的DOM替身35checks，共151項通過。Client methods 摘錄來源直接取最終 source，JavaScript fixture 直接取最終 web，source pins 隨 evidence/build01 留存。沒有使用候選 DLL 冒充最終 DLL。

同名 CMD --check-only 在只有 Windows/System32 與 Windows 的 PATH、沒有 DOTNET_ROOT override、不同 cwd 下通過；固定r28 entry、manifest、13,957asset presence/size及runtime/web hashes有 entry-check.json。只執行 check-only，未走正常啟動分支。此包沿用機器安裝的相容 .NET Desktop / WebView2；不是內含 runtime 的安裝器，正常啟動不需要 PowerShell。開發重建仍使用 SDK10.0.300／PowerShell7／Node與離線vendor套件。

full-assets-verification.json 記錄一次完整13,957資產 SHA 核對、無缺漏／多餘資產。seal 預設 --verify 再核對非資產本文 SHA、資產大小及完整清單；不額外重跑2GB body pass。必要時主管 --full 可重算全檔 SHA。

原 chat23、shadow35922、舊鍵盤／focus／launcher／controller／audio內部 suite 都保留歷史身分，未為湊數重跑；主管 candidate 獨立68/23也未冒充本輪新增。historical-evidence.json 明列。managed／DOM使用明示替身，未證明實際 WebView2／原生焦點或音訊輸出；正常CMD、原生控制同步、兩聲音與gain/mute、世界退役／登出／有界關窗等由主管接續。

## 副本可重建命令

先複製整個 delivery 到獲授權的 review 目錄，令 `$copy` 指向副本：

```powershell
python -B -X utf8 "$copy/_integration/seal.py" --verify --full
python -B -X utf8 "$copy/_integration/run_stage.py" --output "$copy/_integration/evidence/root-checks01"
```

run_stage.py 會重建產品、更新本副本 launcher 的實際 manifest SHA／asset count／r28entry，再建置 launcher 並跑151checks及同名CMD --check-only。所有 source/audio/web/assets/vendor、launcher-source、check-template與fixtures均在包內，重建不需候選或舊r27目錄。output 必須是新的 integration 子目錄。這會改變副本的 build/evidence，請勿在已封存原件重建。

setup.py、audit_assets.py、audit_delivery.py、finalize.py 是本次歷史整合／核對程序，不是副本重建入口。所有步驟命令／exit code／stdout/stderr見 evidence/build01；無下載／安裝／系統設定修改。

本次所有建置與151checks先成功完成，最後收據彙整因 product-build stdout 尾行混用舊編碼而 strict UTF-8 decoder 報錯。原始 log 未改；complete_build_receipt.py 只重新核對既有成功命令、零警告錯誤與相同DLL結果並補齊收據，未重建或重測。原因及處理見 evidence/build01/receipt-recovery.json，run_stage.py 已容許資訊尾行替代解碼，正常副本重建不需這個補收據腳本。

交付 seal 為 _integration/candidate-seal-manifest.json，pathsRelativeTo=delivery，排除自身及可變runtime/runs與cache。完成Goal收據最後追加入seal避免循環自雜湊。正式原生驗收未完成，不代表P24完成。
