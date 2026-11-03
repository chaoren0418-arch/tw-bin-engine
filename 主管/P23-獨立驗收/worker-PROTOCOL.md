# P23 原場景透明實作與驗證邊界

本階段沿用 P22 完整雙區塊繪製、冷載 equal-z 順序與越界像素，新增由本機主角 cell 觸發的原群組透明狀態。不是敵人遮住即透明，沒有更改原服規則或角色深度。

## 原來源

固定 BIN base 0x400000，SHA e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2。reference/opacity/source-manifest.json 列重新核對的每段 VA/RVA/FileOffset/大小/SHA；bin 保存原 bytes、隔離 IDA 程序及失敗紀錄，decompiled 保存反編譯。原檔與原 IDB 皆唯讀。

- F0B570：s32 group ordinal 與數字 id 不可混用，群組含完整 member 清單。以 tile pointer 找 cell 中第一個匹配節點，並非直接修改同 ordinal 自己的 draw。
- F0EB70：同 cell 可有多筆 group/type，包含重複 group。南塊 slot2 先於北塊 slot0；保留全部 1825 trigger，依原 cold slot 限制入列 1796，另 29 保留 metadata。211 群組未刪減。北塊超出名義範圍但原 slot 可入列的資料保留。
- F0A050：153CE60 的本機主角座標控制；內部 X=worldX*2-32768，cache base32384/32896。先清舊 group active，再依現在 cell 原順序 enter，最後恢復未 active 的舊群組。A4C270 移動、A51920 初始化呼叫。
- 85E090/85E600/85EA90/85EE80：type0 bits1 半透明，type1 bits2 隱藏；節點 flags0x400/800、alpha byte、target word；每50ms改4級。保留 intrinsic0x100 與相反方向取消；不簡化成任意固定 alpha。
- A231C0 等時排程依插入順序，允許重複；A23AC0 取消全部同 callback+group。原 x86 oracle僅在計時排程/合成記憶體邊界替代；不宣稱原客戶端實機。
- 97B700 內部權重表及 976C20/977460 sparse RLE：RGB555 按通道 floor((dst*(31-k)+src*k)/31)，固有半透明 floor((src+dst)/2)。32級與原表逐值一致。

## 產品

SceneOpacity 由資產建索引，每 WorldSession 独立。正常本地預測移動與原自身 CHARPACK 更新 cell，其他 actor 移動不改主角透明。cancel/map epoch 清除狀態。正常時鐘為 Environment.TickCount64，live 不強制時間。

OpacityRendering 掃描受變動節點，按 dirty rectangle 重繪該區所有原 draw，保留全域 depth/mask。透明混色仍留底層可見 entity ownership，opaque 地物遮住者不可選。MagicTargetTests 保留 P22 真失敗的歷史未觸發模型；OpacityReplayTests 另驗原動態結果，未刪零像素門檻。

原生使用同一背景與 object patch；Web 每50ms傳同 renderer 的640×400 PNG。Web 真實 img.decode 與來源 SHA 核對成功後才發布 renderedScene。UI測試每張只暫停新 Web frame publication，產品時鐘與世界繼續；首 await 前 immutable capturedFrame 保存 world/opacity/objects/applied sequence/tick/hash。DrawToBitmap、CapturePreview、來源PNG分開保存。

## 驗證分類

1. 原 BIN 核心在 Unicorn 執行19步狀態及全部 timers，57次原核心呼叫；原混色表32768輸入、128個實際RLE樣本。非原機連線。
2. 五幅獨立來源完整場景，每幅10727424 color/mask pixels；step0等同P22。產品 dirty recomposition 每幅零差異。含原生選取owner的opaque/half/hidden/transitional單元驗證。
3. P22三份真失敗封包明示離線重播，原x86判定群組。三個狼姿勢動態後正可見；r3-final與r4-final另在無trigger位置為真正零可見/不能施法/C6=0反例。r4-before hidden=[]不是第三個負例。
4. live 原JAR、加密protocol、正常行走與重登另存 evidence/opacity-*-r*；opacity_live_verify.py 以實際記錄tick重放原x86，核對全states/groups/timers，以及原旁觀移動封包、SQL保存、資源及禁止opcode。

## 未涵蓋

A3A530 的 actor+612 預設FFFFFFFF；F0B570由s32尾延伸HHHi資源物件讀group id，F0E9B0建立並賦值。當前兩份s32該count原始0000，不存在這類local resource actor；其完整繪製/關聯fade未實作，不能將普通server CHARPACK角色當該分支。見 associated-objects-scope.json。

仍不是原2×2 cache/地圖streaming/portal或完整引擎；其他地圖、未支援TIL/人物/動畫/法術/學習/交易/倉庫等前階段限制保持。原BIN實機未執行。任何未知來源不靜默猜測。


## live 發現並修正的精確分支

原r2 actual trace event16，restore intrinsic0x101後重入117：原85E090 terminal else保留target0，舊產品誤設1（13節點，畫面flags/alpha相同但內部狀態不等）。r2原raw/binary不改；live-failure-native-vectors.json以原BIN完整重播r1/r2兩端共40非reset事件，僅此一事件13target差異，其餘全欄一致。OpacityTests納入四個實際事件suite，修正只移除terminal else多餘target賦值，不忽略target欄。
