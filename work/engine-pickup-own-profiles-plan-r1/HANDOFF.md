# ENGINE-PICKUP-OWN-PROFILES-PLAN-R1

已完成來源與交付規劃；**沒有擴大產品支援、修改／編譯產品或執行產品測試**。所有寫入僅在本fresh work root。下一實作須等待r34獨立接受、主管明確契約及最終base綁定；目前引用sealed r34只作設計對照，不能當accepted下一版本基準。

42個既有own profiles（含0/0對照）均具備同一base action15的八方向四token資料，ticks為1/1/4/4；既有各profile的8個terminal frame0均由accepted pixel comparison與本輪profile hash／key核對支持安全重用。同一gfx不同weapon的完整拾取sequence相同，base frame0 canonical bytes也相同。因此其餘41個profiles在**有限名義引擎政策**下有足夠body來源可規劃；strict native eligibility仍0，並非原生private-neutral、variant或FIFO等價。

## 最小擴展提案

保留r34的239766-byte resource完全不變，增加一個extension resource。profile allowlist新增41組；gfx0的四個其他weapon共用既有24個body frames與sequence。其餘12個gfx各存24個新body frames，共**288幀**，同gfx不同weapon不重複嵌入。

預期resource：**2,473,547 bytes**，SHA256 `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba`。LogicalName提案：`P06.Resources.PickupOwnBase15ProfileExtension.json`。此值涵蓋41個profile路徑／hash／body-source bindings、12個sequence groups與288幀原像素JSON；不含來源稽核offset資料。`plan.py`的extension expression定義完整結構，canonical UTF8 sorted compact JSON在記憶體計算，**未寫像素payload**。

全部42 profiles共有1344次幀參照（新增41組為1312次）；去重後416個body frames＝104個既有base frames＋312個pickup frames。312中24已在r34，288為新增。身份層次保留：104 historical-selected-payload-matched；312 current-idx-and-bounded-slice-only，新增288全部屬後者。沒有整包archive/BIN版本認證。

`profiles.json`逐項列gfx／weapon、action15、各方向1→2→3→0 exact keys、profile hash／安全base重用、來源引用、共用群、shadow及combat資訊。`body-groups.json`只列13組去重body keys與已封存frame/identity表的引用，沒有再複製416幀的大型像素稽核表。

## 實作時必須保持的差異

- 使用sequence裡的**exact key**，不可沿用r34 gfx0的sid/frame公式。2786、2796、6650、6658、6661、6671的terminal sid從8起；2796的pickup基礎key為48:1／48:2／48:3，並非frame0／1／2。方向各有自己的sid，不能鏡射。
- 幀字典至少以gfx/sprite group區分；不同sprite可有同一字面key，如24:0。只准入42個明確(gfx,weapon)配對，不能因gfx已支援便接受任意weapon。base frame0從當前已驗hash的profile取得，不能跨profile借body。
- shadow依原exact-key策略。13組共104個terminal base keys命中，312個pickup keys全部缺失；42profiles參照層為336命中／1008缺失。pickup姿勢須移除原idle shadow後保持無shadow，terminal才恢復自己的exact-key shadow。不得補畫、借用idle、擴大shadow來源研究。
- 所有42 profiles的walk/breath/attack/damage/die/spellDir/spellNoDir皆有8方向Supports，但時長不同；`profiles.json`保留各action appearance double Duration及attack/spell Speed1逐幀整數計算的靜態值。這些是來源計算，不是產品runtime量測。
- body共用不代表combat timing共用。保留既有ActiveOwnCombat、WithProjectileHit、Timing.Matches、實際profile及duration比較。damage例子：0/0與0/4為583.3333333333334，0/20與0/46為582；不可用ObjectVisual.StepMs截整數值。death顯示不受有限Duration限制，S87 weapon8仍循既有base weapon0外觀路徑。
- 保留captured ActionCadence的normal/haste時長；有Timing時用AnimationMs，沒有Timing才用appearance.Duration。projectile-hit cache即使OwnCombat為null仍可優先。新weapon/profile切換即使共用同一body也須取消舊owner instance。
- scheduler、owner/serial、逐筆S158/15、latest eligible replacement、terminal584、900/3000與權威數量等沿已接受的最終r34契約；沒有擴展Speed2/3、非零Brave、remote。metadata events仍僅保留，沒有新音效/effect dispatch。

## 驗證與限制

本輪僅執行來源hash／mapping／count／reference一致性斷言，精確筆數見mapping-checks.json及result.json。沒有產品build/runtime/native測試；既有402、84與像素解碼檢查皆是引用的歷史證據，**未加到本輪計數**。已讀accepted decoded block JSON作記憶體投影，未重新解碼SPR、掃PAK、寫payload、copy asset package、建junction或改任何既有seal。

未發現本範圍body、base key或shadow manifest來源缺失。pickup shadow缺失是已知顯示政策條件；native私有旗標／variant／FIFO未知仍保留，不在本輪重開研究。`candidate-tests.json`全部proposed/unexecuted；live、正常GUI、原生timing及完整同版回歸仍由後續授權安排。

唯讀重算命令（本目錄）：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\verify.py --seal '<published artifact-manifest SHA256>'
```

verify核對本seal、全部source pins，再執行plan.py的純記憶體重算與輸出JSON等值檢查；不執行產品、不寫cache。plan.py --write只用於封存前本root，不可對封存件重跑寫入。

真Goal本輪開始讀為blocked，未新建或修改；未建chat/subagent或改模型。無DB/services/SQL/GUI/game/socket/native執行。完成封存後停寫；若r34覆核要求修正，由主管另行優先派工。
