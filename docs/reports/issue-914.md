# Issue #914 — T399：R10-O 召喚與盟屋拍賣小項

依據：`docs/reference/gap-matrix.md` 15.12.6（R10-O）與 15.12 的 1:52、4:AgitAuctionUI 兩列；FU9§3.1、FU13§4.2–4.3、WIN2§5.3。
分支：`task/914`。

## 做了什麼

### 1. 召喚窗畫 `SummonDrag_Image`（1:52）
- `NativeUi/SummonPanel.cs`：載入時讀 `SummonUI.xml` 根層的 `<Image Name="SummonDrag_Image" Surf="0;2;13">`（`SummonPanel.DragAlphaOf`）。
- 從怪物格按下並移出這一格後，把被拖的怪物 surf 畫在最上層，**圖中心對齊游標**、**透明度 13/32**；`Bounds` 會涵蓋拖曳圖（canvas 的 clip 是面板的 Bounds，不加的話拖出視窗外就畫不到）。
- 放開、還沒移出按下的格子、怪物 surf 缺圖、XML 沒有這個元素時都不畫（規則 5，不畫替代圖）。拖進快速格的行為不變。

### 2. 面板路徑顯示 743（1:52）
- `SummonPanel.Summon`：格子能不能點，照舊看面板這一幀讀到的狀態（停用＝等級公式或魅力）。真的要送出時，再用宿主**目前**的等級做一次 `sub_C00F50` 的送出檢查（**只看等級公式**，`SummonPanel.SendAllowed`）：
  - 不成立 → 呼叫 `ISummonUiHost.LevelTooLow`（新增，介面預設不做事），不送，然後 `Close()` 請宿主關掉這次請求（FU9§3.1「最後一律關窗」；因為沒有送出，所以可以直接關，不會有「送出被判過期」的問題）。
  - 宿主的請求已經換成別次（revision 不同）或目前沒有等級 → 不做任何事（不推定）。
- `PetUi.cs` `SummonUiHost.LevelTooLow`：743（`ServerMessageText.Decode(743)`）寫進聊天欄（`AddBoardChatNotice`，原生 UI 看得到），也呼叫 `RingSummonAllowed` 讓 WinForms 寵物分頁顯示同一行提示。

### 3. 原生 UI 下 `agapply` 不在 HyperText 頁重複出數字框（4:AgitAuctionUI）
- `NpcUi.cs` `NpcHyperTextView`：S136 的 html 是 `agapply`，而且原生 `Bid_Window` 可用（原生 UI 開著、面板沒 fault、`AgitBidPanel.Available`）時，HyperText 頁不放數字框（`ClientForm.HyperTextOmitsNumber`）。
- 判斷用「出價窗**可用**」，不用「出價窗**正在顯示**」：原版的 agapply 一律交給 CAgitAuctionUI（WIN2§5.3），所以使用者按取消關掉出價窗之後，頁面上也不該冒出第二個框。原生 UI 關閉（`--classic-ui`）或出價窗不可用（例如缺 `AgitAuctionUI.xml`）時，照舊用頁面的數字框；其他 S136（`agsell`、數量詢問等）不受影響。WinForms 的數字列（`AmountPrompt.cs`）不在本任務範圍，沒有改。

### 4. 測試
- 新檔 `engine/source/SummonAgitFollowupTests.cs`（`[WorldModuleTest("summon-agit-followup")]`，自動發現）：
  - `SummonDrag_Image` 的 Surf 解析（13／只有 surf＝32／沒有元素＝null／不合法就丟版面錯誤）。
  - 拖曳圖：位置（中心對齊游標）、Bounds、像素混色 13/32（背景與視窗底圖上各一次）、放開／缺圖／XML 沒有元素時不畫、拖進快速格照舊。
  - 送出檢查：等級公式（只看等級、不看魅力、沒有等級不送）；宿主目前等級掉到 24 → `743` 接著 `close`、不送、窗關閉；revision 改變或等級變 null → 不動作；等級正常 → 照常召喚；宿主沒實作 `LevelTooLow` 時走介面預設。
  - `HyperTextOmitsNumber`：agapply＋出價窗可用 → 不出框；出價窗不可用 → 保留；其他 html 保留；比對大小寫完全相同；沒載入的 `AgitBidPanel` 是不可用。
- 全部資料都是合成的（自寫的小 XML、單色圖），不讀主客戶端或參考客戶端。

## 怎麼驗證
- `python engine/tools/check-no-game-assets.py`：pass（tracked 4464、errors 0）。
- 資源查詢（PakViewer CLI，唯讀，輸出放 `scratch/pak914/`，不進 repo）：`D:\lineage開發\0-客戶端\Tile.idx` 有 `SummonUI.xml`（8332 bytes）與 `summonList.xml`（2982 bytes）；`SummonUI.xml` 第一個元素就是 `<Image Name = "SummonDrag_Image" X ="0" Y = "0" Surf = "0;2;13" Activate="0"/>`。本任務沒有宣稱缺資源。
- 本機編譯：**沒有成功跑完，原因跟本次修改無關**。在這台機器上，NuGet 解出 `Microsoft.Web.WebView2 1.0.4258.31` 時，assets 清單只列出 `.xml`、沒有 `.dll`（nupkg 裡其實有 DLL，`lib/net462/*.dll` 也解出來了），所以退回參照 `lib/*.winmd`，出現 NETSDK1130。改用 vendor 來源、`.github/nuget.ci.config` 或另指 `--packages scratch/nuget914` 都一樣，`rebuild.py --build` 也在 product-build 停在同一個錯誤。看起來是本機執行環境列不到 .dll 檔，不是原始碼的問題。編譯與測試以 PR 的 CI（ubuntu 交叉建置＋`--module-tests`）為準。
- 改動的程式碼有逐項人工核對：API 簽名（`UiCanvas`、`UiSurface.Bitmap`、`WorldModuleTestContext.Equal/Check/Throws`、`AgitSubPanel.Available`、`WorldSession.AddBoardChatNotice/RingSummonAllowed`）與既有測試（`SummonPanelTests`、`SummonWindowTests`、`RingWindowsTests`）的預期都沒有衝突。

## 未驗證什麼
- 真機（M39）：拖曳圖的位置（中心對齊游標）、透明度（`;13` 當成 13/32，跟 FU13§4.2 的 DisableOverSurf 13 同一種解讀）、第二欄 `2` 的意義，都是**推測**。
- 743 印在聊天欄的顏色（用預設色，原版顏色沒查），以及原版是印在聊天欄還是別的地方，**推測**。
- 原版快速格／怪物格送出時只看等級、不看魅力（FU9）。引擎的格子可點條件仍然包含魅力（FU13§4.3：停用格不吃點擊），所以「魅力不足但等級夠」在引擎不會送出；跟原版一致的前提是快速格有內容時也會依魅力停用（FU9「有內容時同怪物格」）。
- 停用格外觀（FU13 D6／D7：DisableOverSurf 是半透明色塊而且常駐）不在本任務範圍，沒有改。
- 盟屋出價窗的加減步進值與出價窗位置（Q8）沒查，沒有改。
- 本機沒有跑過任何測試（依規定，交給 CI）。

## 產出路徑
- `engine/source/NativeUi/SummonPanel.cs`
- `engine/source/PetUi.cs`
- `engine/source/NpcUi.cs`（只改 agapply 的判斷）
- `engine/source/SummonAgitFollowupTests.cs`（新）
- `docs/reports/issue-914.md`（本報告）

## 給下一棒的提示
- 真機（M39／M20，交給 Codex）：
  1. 召喚窗：把怪物從等級頁拖到快速格的途中，看游標上有沒有半透明的怪物圖，位置和深淺跟原版比對。
  2. 戴召喚控制戒指、低等級角色：快速格有高等怪物時按下去，看聊天欄有沒有「等級太低而無法召喚怪物」，窗是不是關掉。
  3. 盟屋拍賣公告板：按 Bid 之後只會看到 `Bid_Window` 一個輸入框；HyperText 頁不會有數字框；按取消關掉出價窗之後，頁面上也不會冒出框。
- 本機 WebView2 套件解析的問題（NETSDK1130）如果其他任務也遇到，可以交給 `needs:orchestrator` 查本機環境。
