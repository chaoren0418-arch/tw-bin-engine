# Issue #510 — T204：M7-B 能力旗標與 SP／MR

## 做了什麼
- 新檔 `engine/source/AbilityStatus.cs`：透過 M7-A 擴充點（`WorldExtensions.cs`）以 partial class 掛上，**沒有改 `WorldSession.cs`／`Program.cs`**。
  - `[WorldReceive(36)] ReceiveAbility`：S36 Ability，`C(type) C(equipped?1:0)`（SP〈S_Ability〉@21–24）。依 type 覆寫旗標；equipped 不是 0／1、長度或補位不對 → `ProtocolException`（與既有模組一致）。
  - `[WorldReceive(37)] ReceiveSpMr`：S37 SPMR。長度 16＝`D(sp) D(mr)`（`ConfigClient.isROClient()`＝true，本產品 CLIENT_LOGIN_TYPE=2，與 `MagicStatus.cs` 的既有判斷相同）；長度 8＝無參數建構子 `C(50) C(100)`（SP〈S_SPMR〉@269–272，不分 ROClient）。SP／MR 以有號 int 存。
  - `[WorldReset]` 清空；`[WorldSnapshot("ability")]` 輸出 `flags[{type,equipped,updates}]`、`active[]`、`updates`、`spMr{sp,mr,form,updates}`。
  - 公開唯讀 API 給之後的原生 UI：`AbilityFlags`、`AbilityEquipped(type)`、`SpMr`。
- 新檔 `engine/source/AbilityStatusTests.cs`：`[WorldModuleTest("ability-status")]`，由既有 `--module-tests` 入口自動執行（CI 的 module-tests 步驟會跑到），38 項檢查：註冊、初始、S36 覆寫／排序／unsigned type、S36 四種格式錯誤、S37 dword／負值／>255／byte 形式、S37 三種格式錯誤、錯誤封包不改狀態、Reset、session 隔離、封包不再記為 unhandled。
- `engine/_integration/dev/rebuild-lock.json`：`--update-lock` 重產（新增兩個來源檔）。

## 怎麼驗證
- `rebuild.py --update-lock` → `rebuild.py --build`：PASS（第二次同步 main〈含 #523／#531〉後 productDll `0f20cf3d…4a0`）。
- `P11.ClientEngine.exe --module-tests`（照 CI 指令）：第二次同步 main 後 pass，5 組 255 項，失敗 0（含 ability-status 38 項與 main 新增的 recovery 測試）。
- 第三次同步 main（含 #446／#519／#548／#551）：`rebuild-lock.json` 取 main 版後 `--update-lock` 重產；`--build` PASS，productDll `b9c1b756…ab84`；`--module-tests` pass，7 組 385 項，失敗 0（ability-status 38 項；與 main 新增的 packetbox-status、server-features 無 opcode 衝突）。
- 第四次同步 main（含 #463 原生 UI canvas）：`rebuild-lock.json` 取 main 版後 `--update-lock` 重產；`--build` PASS，productDll `2834c906…ba93`；`--module-tests` pass，7 組 385 項，失敗 0（ability-status 38 項）。
- `check-no-game-assets.py`：pass（errors 0）。
- `packet-trace-tests`：pass。其餘既有測試由 CI 跑。

## 沒驗證什麼
- 沒啟動遊戲／伺服器／DB，沒有真機封包；長度與欄位只依 SP 文件推出，未以實際抓包核對。
- **S36 type 值的意義**：reference 文件沒有查證（gap-matrix op 122 只寫「能力旗標變更」），因此只存原始 type，不命名、不做畫面。
- 原版 TW16 op 122（`sub_932300`）收到後的畫面表現沒有查，狀態窗顯示屬原生 UI 模組。
- S37 的 dword／byte 形式以封包長度區分；若伺服器改成非 ROClient 設定（`C(sp) C(mr)` 也是 8 bytes），會被當成 byte 形式，數值仍正確，只是 `form` 一樣標 `byte`。

## 產出路徑
- `engine/source/AbilityStatus.cs`、`engine/source/AbilityStatusTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。

## 給下一棒的提示
- 狀態窗（原生 UI）要顯示 SP／MR 時讀 `WorldSession.SpMr`（null＝尚未收到）；能力旗標讀 `AbilityFlags`／`AbilityEquipped(type)`。
- 要給 type 命名前，先在 reference 文件查證伺服器呼叫 `S_Ability` 的位置（哪些裝備送哪個 type），再補到 SP 或 WO。
- worktree 沒有 `engine/vendor` 時建置會 NU1301；本次用 `mklink /J engine\vendor <原專案>\engine\vendor` 解決（vendor 不進 git；拆除用 `cmd /c rmdir`，不加 /s）。
