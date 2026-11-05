# dev 局部補測證據清單（T044／Issue #41）

來源為 [#37 結果留言](https://github.com/chaoren0418-arch/tw-bin-engine/issues/37#issuecomment-6650577185) 與本機既有證據，整理日期 2026-11-05。原 PNG 未縮圖、重畫、裁切或重編碼；trace 與摘要均按位元組複製，scratch 原件保留。

## 截圖檢查

實際候選 17 張（比任務約估 13 張多），全部逐張目視檢查，未見帳密明文；有登入區的圖片帳號／密碼欄均為空。這是目視檢查，沒有把檔名搜尋當作 OCR，也未讀取機密設定或私密帳密值。全部提交，沒有排除項目。

單檔上限採 2,000,000 bytes，合計上限採 15,000,000 bytes。17 張合計 **8,356,949 bytes**，最大單檔 **712,183 bytes**。17 張圖片及 6 份 trace 的大小、SHA-256 均符合既有本機 evidence-manifest.json（23／23）；摘要另記本輪雜湊，不宣稱它列於歷史清單。

| 圖片 | bytes | SHA-256 | 處置 |
|---|---:|---|---|
| [04-after.png](04-after.png) | 708384 | 5019f4f039c8f0048a3af99b8e77ab7d3c18d707c3667a78df441488fc043e4b | 目視通過，提交 |
| [04-before.png](04-before.png) | 706293 | 8222e7fd1df6c6f534411be17f8992a203f64e5a8b6442e4e3ca8bfaf37259bf | 目視通過，提交 |
| [04-start-diagnostics.png](04-start-diagnostics.png) | 530747 | 7bde3bb31522c9452482e6ca8b71cc3e5c7199200af2d66f5670504bf33f2568 | 目視通過，提交 |
| [05-after.png](05-after.png) | 712183 | 21763cfdb9c83358704e699d82fe924e91fc8d38f0645b5989860d6a5bc120cd | 目視通過，提交 |
| [05-before.png](05-before.png) | 705796 | e5db3ec14129f0a49c9b269e3ec2e2c0409aae3023df944be848f4a01ef2b0a8 | 目視通過，提交 |
| [06-approach.png](06-approach.png) | 704681 | 3422bef3dd39556485f8cfde6b317edbbce66459103858d4595639cf07b4bd7e | 目視通過，提交 |
| [06-before.png](06-before.png) | 711939 | dd8774af535b974f2fd11eafc35f2a24b60d418eaef0720572cae48329fa9b3b | 目視通過，提交 |
| [06-diagnostics.png](06-diagnostics.png) | 531384 | b7fe669617d6ae3e2af7ed7ac89deb81706b000b5db3a4b7ab00ee3d7a6e112d | 目視通過，提交 |
| [07-before.png](07-before.png) | 706208 | 681e5d7854e78d3d133ece9bfb50fc51e60d5bb1f4376def7beaf051634f7290 | 目視通過，提交 |
| [07-effect.png](07-effect.png) | 531025 | dc5bcf99445fbd3904327461082df6607555a521ff2fe9917a490baeacbdcd45 | 目視通過，提交 |
| [10-gold-selected.png](10-gold-selected.png) | 271240 | 320514dc23c23206fa31c918314edd509cd3720339215bea72a8b70263d10793 | 目視通過，提交 |
| [15-audible.png](15-audible.png) | 255522 | d9e2189754a738e6e595a528d0e302e1420390bdd838844c78238e4f16e94d63 | 目視通過，提交 |
| [15-closing.png](15-closing.png) | 255319 | 7d743010698cc6789a33be4f42859055c9c858d4da990ca3b4f0d6e0e2bd7d8d | 目視通過，提交 |
| [15-controls.png](15-controls.png) | 249701 | 9ae902bbc792d32930a702cfa92a11106be44f90f644a9eb5054f85e54539099 | 目視通過，提交 |
| [15-muted.png](15-muted.png) | 255990 | 1a78bb4ac1a8e751f807f4be2e128c079ff27893373790e3b4c566e614b6f930 | 目視通過，提交 |
| [15-unmuted.png](15-unmuted.png) | 256103 | 6898e99c2d1d05c2e0eb22fc8fd439ae8dc1841ac1d1223c8e2d4d8764a4d179 | 目視通過，提交 |
| [15-volume50.png](15-volume50.png) | 264434 | 8ffd1c6db7e6fc37f5eb132bb7aafc1dad8347fe20f800e27eca015a66974c2c | 目視通過，提交 |

## trace 檢查

以下每一行均經 JSON 解析，鍵名區分大小寫且剛好為 utc／dir／op／len／seq；同時驗證恰有 5 個鍵（含重複鍵檢查），不含 payload 或其他欄位。另驗證 utc 為 UTC 時間字串、dir 僅 in/out、op/len 為整數、seq 為整數或 null。共 **4,468 行**通過。

目的地逐檔 git check-ignore 均退出 1（未忽略），因此提交複本，沒有使用 git add -f。原件仍位於 scratch/packet-trace/ 的同名檔案。若未來規則改為忽略，應保留本機並改列排除，不得強制加入。

| trace 複本 | 行數 | bytes | SHA-256 | 處置 |
|---|---:|---:|---|---|
| [trace/04.jsonl](trace/04.jsonl) | 1156 | 85643 | 2a09c3aad72ea42d6214093e70b5f5e22db235e5b992ad15cb9e8df81cb73e2c | 五鍵通過，提交 |
| [trace/05.jsonl](trace/05.jsonl) | 958 | 70846 | a7a09a3092a42a0d22e53b0de4dd7de5c749e34c6b9091839fb6ac7df29c899b | 五鍵通過，提交 |
| [trace/06.jsonl](trace/06.jsonl) | 1315 | 97569 | 10e903ceb1cf5ea49a9a975dc6e63ff409bbda77cf420006dadc11b74e09b5e7 | 五鍵通過，提交 |
| [trace/07.jsonl](trace/07.jsonl) | 282 | 20786 | 9458ee330f304f87761c8c87c3e5a1a523623ff448092057d8b247965a0adae9 | 五鍵通過，提交 |
| [trace/10.jsonl](trace/10.jsonl) | 229 | 16867 | fdde0d4f646dd9d92abefad1a4a70421270e617f7f4d341fa1d6648b2cbd1959 | 五鍵通過，提交 |
| [trace/15.jsonl](trace/15.jsonl) | 528 | 38983 | 750356442f0bf6cf33a952c80f4dff1db366827f50cb65433ae2ab7dfab6fb5a | 五鍵通過，提交 |

摘要：[trace/trace-summary.json](trace/trace-summary.json)，29362 bytes，SHA-256：12dcb860bc33f5c3cfa58fe0af9281c2ef4c0ffc8ad8aee35f82b08a86e5a5ce。與 scratch/issue-37/run/trace-summary.json 位元組相同。

### 摘要與最終 trace 的時間範圍差異

六項操作窗的入站／出站總數與全部 op 分組均重算一致。04／05／06／07／10 的 fullSessionCounts 亦與完整 trace 一致。

15 的 fullSessionCounts 對應原 trace **前 456 行**（最後時間 05:26:57.397 UTC），最終原件另有 **72 行**（05:26:59.795～05:27:27.718 UTC），合計 528 行。額外分組為 S10×48、S33×2、S87×10、S120×8、S123×2、C95×2。前 456 行各組逐一重算完全一致；新增部分均在施法操作窗之後，操作窗仍為入站 85／出站 5。這與摘要先產生、客戶端後續繼續記錄的情況相符，但生成時序屬推測。

保留原摘要及完整 trace，不刪除尾段、不回寫歷史資料。不要把摘要 fullSessionCounts 當成最終 15.jsonl 的全檔數量。

## 原報告

[issue-37.md](../../reports/issue-37.md) 使用既有本機檔案，內容未改，SHA-256：f38d85e228a12c04602340cd87af635efa4c63014ce53b7c49d2bd33e4703db5。其「不公開 raw trace」及「未提交 git」描述的是 #37 當輪；本次 #41 依新授權提交已驗證的五鍵 trace 複本。
