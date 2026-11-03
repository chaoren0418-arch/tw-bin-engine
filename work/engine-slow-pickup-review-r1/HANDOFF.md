# ENGINE-SLOW-PICKUP-REVIEW-PREP-R1

準備完成，candidateReviewed=false，productCandidate=false；Slow拾取仍qualified=false，不授權實作。本輪只寫本工作目錄，固定r31 release，不拿未整合r32當基底。

實際140筆：基線檢查61全通過；擬議安全契約24項，14符合／10個舊基底差異；另外55筆觀察分列，沒有灌入pass數。正常Speed0/1肉/箭真wrapper換world生命週期12案、synthetic前提actor wrapper 12案、合法S158/S87/S67/S15/S24先到控制12案。詳見baseline-findings.md與baseline-probe.json。

主要可接續問題：真S206/S87換世界後，ready且900ms已過仍因舊groundSending不能admit新拾取；舊success發舊world事件、舊exception Faulted新世界。false不發舊事件但同樣等舊callback才釋放busy。公開路由未能先admit新操作，因此未冒稱舊finally清掉已admit新owner；人工lease/actor另列。合法權威回包先到對照全成立，不能把這些變更照搬為completion拒絕條件。

獨立掃描限manifest指定42個profile JSON，無pickup sequence者42，完整8方向pickup為0；未抽取新資產。原4個JAR class靜態鏈證實提交前後身份/數量/收據與S158/action15（GM invis例外），不證native輸入可放Slow。review-contract.md列frame初始索引、metadata取樣、A407D0條件、alias/八方向/pixel門檻。

作者封存後另核279artifacts、101BIN chunks PE映射/bytes、42profiles/13gfx原metadata；15.get四token[1,1,4,4]與missing frame keys一致，無矛盾。opcode226→條件式+4C鏈支持，但+64queue consumer/cancel、完整scheduler窗/輸入與完整像素仍unknown。sealed-source-conclusion.md及sealed-source-crosscheck.json保存有限結論，不新跑native/JAR handler。

固定DLL `12ad5bbfac696033ee1f8d807346f401216138f4ce51cb58da9438e0b4ba5ec1`；177release來源、共509外部pin核對。helper0warning/0error，socket事件0；沒有drop操作、GUI/正常產品/原生live/DB/SQL/服務，沒有3171全回歸、chat/subagent/模型或Goal變更。

source-pins/artifact-manifest、可重跑helper/run.ps1、commands、來源與基線資料俱全。封存後只可用verify.py唯讀核驗；禁止原地重跑collect/crosscheck/run/seal。到此停寫供主管核對；下一個正常pickup生命週期候選需新派工與final r32精確差異綁定，原r31證據不覆寫。
