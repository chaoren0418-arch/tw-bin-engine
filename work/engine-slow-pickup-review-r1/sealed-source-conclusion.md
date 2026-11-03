# 作者封存後的獨立靜態交叉結論

來源seal `8b1110e77614fbe8f9cb9b68718348c71f75d04110451cbca3c90699114125e1`、source-contract `8c29fd1b806789caec833e7b23cf7bfea719d912751e4429817d89b2b68fea65`、result `33fd784f399450ec3cab0fc6ed6b2bc86ed614a1f6d07b33853255e0c90719e8`均精確核對。只在主管提供seal後讀取；沒有依賴可變中間檔。

已重核279列冊產物；從固定BIN PE section table獨立計算101個chunk的file offset並比對bytes/SHA。直接讀原vtable：0x1150C24/+4C=0x51E920、/+64=0x51E860；0x1150E8C及0x1150F7C的+4C=0x521E20、+64=0xA3C4F0。這些實值與作者修正後的+4C鏈一致，未採舊+80探索假說。

閱讀已驗bytes對應的dispatcher/receiver/selector/frame反組譯：opcode226在0x947530分派到0x92CB70；0x92D329取vtable+4C；其他條件分支0x92D2C8取+64。0xA467A0的1/2/18有特例，15走default action setter，且仍受一般狀態與後續例外影響；初始化frame0後0xA43560先++frame，成功metadata路徑0xA3EF40再呼叫0xA407D0。這支持作者**A3EBF0=false及其他前提下**的接收/逐幀鏈，不能移除條件。true路徑的衍生queue、消費／取消與原輸出命令未完成，維持unknown。

獨立讀固定inner-list.spz（SHA e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242），按gfx重新切entry、驗profile entryHash，解析15.get並重算方向frame key集合及現有profile缺口。42profiles/13gfx均為四token [1,1,4,4]，重算missingFrameKeys與作者一致，完整現有frame set為0。這補強本輪先前「42份無pickup sequence」的獨立結果，沒有矛盾；沒有因此完成原SPR pixel、八方向alias或renderer資格。

同意不能把四token總和直接叫完整utility時間。先++frame、非terminal scheduler frameTime+1/lateness補償、terminal frame0→A44500狀態轉接與queue支線均須保留；共同Speed分支算術是條件式逐幀證據，不能冒充輸入允許或總冷卻。900ms仍是引擎政策。

結論：**未發現與獨立JAR、產品profile或本輪基線相矛盾之處；仍qualified=false，不開Slow gate。** 交叉核對是靜態有限結論，不是完整native動態驗收，也不代替本輪正常pickup生命週期實證。下一個產品提案可優先修正常肉/箭拾取的world/actor/lease生命週期，無需等所有動畫還原，但必須由主管另派精確基準，不能在本輪自行修改。
