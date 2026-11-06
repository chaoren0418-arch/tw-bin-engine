# Issue #155 主專案建置資源恢復

## 結論與做了什麼

已恢復主專案 dev 建置與 check-only，兩者退出碼皆為 0。

- 讀取 `AGENTS.md`、`docs/REPORT.md` 與 #153 報告，fetch 後執行 `git checkout -B task/155 origin/main`。基線為 `ed848ea460ed3ed2e24746ffd8f97fc3f6ad4e41`。
- 先確認 r29 的 `assets/integrity.json` 存在，抽驗 20 個素材的 SHA-256 及大小，全部相符。
- 確認 `engine/assets` 是指向 `releases/r28/assets` 的 junction，再用 `cmd /d /c rmdir engine\assets` 非遞迴拆除連結，重建 junction 指向 `D:\lineage開發\TW1608262200研究\releases\r29\assets`。
- 使用者 NuGet 全域快取指定路徑缺少套件，從 nuget.org 下載 `Microsoft.Web.WebView2` 1.0.4258.31 至 scratch。SHA 核對符合 #88 後，複製到 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg`，並再次核對目的檔。
- 更新 `docs/BUILD.md` 的素材位置及對應排錯說明，補上 #153 原因。

## 怎麼驗證（已驗證）

### 素材及套件

- integrity SHA-256 為 `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`，重建 junction 後再次讀取一致。
- nupkg SHA-256 為 `56f7f4b8bf9aee4b8efefbbdd4f67d5f74ebd1b100ed0806da71bf76af481aa9`。
- 來源為 `https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg`。
- 建置前查核既有輸出與快取樹中的 590 次檔案記錄，確認檔案 link count 為 1，目錄及檔案皆無 reparse point，避免寫入封存或硬連結。

### 建置與 check-only

本機 shell 的 PATH 缺少 Python，使用 `py -3.12` 找到既有安裝，再僅於建置子 shell 加入其路徑。將 `NUGET_PACKAGES` 指定至專案 `engine/.nuget`，HTTP 快取與建置記錄放 `scratch/issue-155/`。執行以下指令。

```text
python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-155/build
engine\啟動新版客戶端.cmd --check-only
```

- SDK 為 10.0.300，來源核驗通過 172 個來源檔、181 個 pinned 輸入。
- product／launcher restore 與 build 均退出 0，兩個 build 均為 0 警告、0 錯誤。
- 產品輸出為既定 `engine/build/client-product-dev/`、`engine/build/product-dev-manifest.json` 與 `engine/launcher/`，中間產物使用既定 obj 目錄。
- check-only 核對 24 個 runtime／web／索引檔 SHA，以及 13,957 個素材的存在與大小。Desktop Runtime 為 10.0.8，WebView2 Runtime 為 156.0.4314.58。

```json
{
  "status": "checked-only",
  "revision": "dev",
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}
```

| 產物 | SHA-256 |
|---|---|
| `productDllSha256` | `7fa85547ec50caf0049d503db114da5f519b235c8173ea45cfda26788946fe08` |
| `audioDllSha256` | `f135b5080fb5acab27905d243e3a03fa06bfb7fc941bcf313f8839535abcd63d` |
| `launcherDllSha256` | `222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d` |
| `productManifestSha256` | `34d0295cf6f6be7c5a7af79c701d74d59b12b35d6ecad619ff4fea1261916f4e` |

### 20 檔抽樣紀錄

將 integrity 的 13,957 筆按 path 排序，取索引 `floor(i × (13957−1) / 19)`，其中 `i=0..19`，形成可重現、含首尾的等距樣本。下表實測 SHA 均符合 integrity，大小亦全數符合。

| 相對於 r29/assets 的路徑 | 實測 SHA-256 |
|---|---|
| `audio-manifest.json` | `87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe` |
| `maps/effects/3626.json` | `4c89d2e9732e1273d3d6c25a3013ce951e6a0e62868ef1de19010cd5ac05b811` |
| `maps/map/133/80007fff.s32` | `ae76b9abcc54a2da5b27e816cc0a887142f70a00283fcb356a707d72bf2e6cd9` |
| `maps/map/2002/80028002.s32` | `42acd38249099b478cf50ab440a63ce72ab5b27e91e98ffa05ff6b637439efac` |
| `maps/map/4/7ffd8009.s32` | `5d293e983ff5bfa07e30b4576d3c842a60c86c40a7e6fc2608615c4cb86f3933` |
| `maps/map/4310/80008000.s32` | `6787b071901ca5d871324917c4ed8534739ccb3605d680dee2b91f556b9238a0` |
| `maps/map/4831/7fff8000.s32` | `a804322b2378f62d53ef4fff4c3cc57354c6fb3d9cdf44a4bf69cdd89fcce38a` |
| `maps/map/511/7fff8000.s32` | `ccfe52b614dfbdf97fd713f80d61e4cdd35e7c8c3000a06b4bba8868c96d1271` |
| `maps/map/5435/80017fff.s32` | `1667d86b99c7868453409a62dead1be0c9d9383d3cbfeb1c00571f5fa6ad22f8` |
| `maps/map/630/7fff8004.s32` | `576d8664fa3a36c9a0acfde2634a1de54d2420577e21582e48cbea4bdd1345a7` |
| `maps/map/777/7ffd8001.s32` | `f1c7e6da3a3ddb1eca66e6ea510613e7e7636c2b497c8c835dc5f35080c9ebdd` |
| `maps/terrain/116.bin` | `921c761b455e4c24155d3345cf430caa97f012dfe8b5da81e5315d4da4bec5af` |
| `maps/tiles/1044.til` | `bec3c07356b398714269086df0210e43bdb83ad4c56482bb097f0bc6d05a6ba6` |
| `maps/tiles/1737.til` | `215fddb990d95ab384b7dfe2be8c5b1293d5c8c9eca2536287c46ea2e5842a55` |
| `maps/tiles/2402.til` | `7c06df925f2462b11e4065960627c30cde7a3aebfbaa92826398ef8f1e76b24f` |
| `maps/tiles/3093.til` | `a07a4fae77fbd6deabd117e1535a329f6d1483507fb0e6d8614763fb9fa39b2f` |
| `maps/tiles/376.til` | `b8e70b67f6f552960368a7f130e036a208a7085b12677bc02a2e67ec8f128797` |
| `maps/tiles/5622.til` | `ce334338cb4dbe98b356d8686932543259663544a9fdfc5f6e6841e33895d6e9` |
| `maps/tiles/6369.til` | `c4dffb182ae3f2f38b7a81ddc65e3f3518e06bb5ff93f97a6c66d97a5de52d3a` |
| `world-appearances.json` | `18c29638f97b0f80bb04928c04aac42679dd96cb998929e8fc8b3c238f42bd6f` |

## 未驗證什麼

- 本輪素材內容 SHA 驗證範圍為上述 20 檔。check-only 的素材本體檢查涵蓋全部索引項目的存在與大小。#153 已有先前全量 SHA 證據，本輪未重跑全量內容雜湊。
- 未執行遊戲登入、畫面、戰鬥或音效驗收，未啟動遊戲、伺服器或 DB，未連正式服務。
- 封存 release 與 hardlink 全程唯讀，未還原 r28／r33 缺檔，未複製遊戲素材。唯一刪除為非遞迴移除舊 junction。

## 產出路徑

- 提交 `docs/BUILD.md`、`docs/reports/issue-155.md`。
- 本機資源為 `engine/assets` junction 與 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg`，依現有忽略規則留在本機。
- 查核腳本、20 檔抽驗 JSON、套件下載檔、建置各步 argv／stdout／stderr／result，以及 check-only 原始輸出保存在 `scratch/issue-155/`。

## 給下一棒的提示

- 由調度器／Claude 審查 `task/155`，後續原生驗收交具有授權的 dot 在 P24Lab 執行。
- 本次資源修復作用於主專案目錄，Git 提交包含文件。其他工作目錄需要自行接妥本機資源，清理時先非遞迴拆除 junction。
- 任務開始時已有未追蹤的 `engine/_integration/dev/receipt.json`。本輪保留該檔且未提交，建置驗證其 product／audio DLL SHA 相符。本輪 manifest SHA 以本報告及 `scratch/issue-155/build/result.json` 為準。
