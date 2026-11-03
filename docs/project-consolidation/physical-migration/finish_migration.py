"""Publish verified physical-consolidation receipts; does not move or delete data."""
import csv,datetime,hashlib,html,json,os,re,shutil,subprocess,sys
from pathlib import Path
M=Path(__file__).resolve().parent;D=M.parent;R=D.parents[1]
if (M/'resume-observation.json').exists():
    raise SystemExit('Historical consolidation publisher is frozen after development handoff; do not overwrite live ownership.')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,s):p.write_text(s,encoding='utf-8')
def save(p,obj):write(p,json.dumps(obj,ensure_ascii=False,indent=2))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
v=read(M/'final-verification.json'); assert v['status']=='pass-content-preservation-build-entry-and-recovery'
rollback=read(M/'rollback-probe.json');assert rollback['result']=='original-restored-and-hardlink-reapplied'
proc=read(M/'final-process-check.json');assert not proc['projectProcesses'] and not proc['projectListeners']
plan=read(M/'dedupe-plan.json')
sample=next(e for e in plan['entries'] if e['source']==rollback['source'])
assert Path(sample['source']).stat().st_ino==Path(sample['canonical']).stat().st_ino
assert Path(sample['quarantine']).stat().st_ino==sample['before_file_id']
oldmanifest=read(R/'project-manifest.json'); maps=read(D/'path-map.json')
for row in maps:
    row['target']=row['proposed_target']; row['migration_state']='physically-moved-verified'
    row['original_root_exists']=os.path.lexists(row['root']);assert not row['original_root_exists']
    assert Path(row['target']).is_dir()
    row['pre_migration_root_type']=row.pop('root_type','physical-directory')
    row['target_type']='physical-directory'
    row['preservation']='Full original file SHA verified; changed configs checked against original byte backup'
assert len(maps)==16
save(D/'path-map.json',maps)
cols=['root','target','migration_state','pre_migration_root_type','target_type','role','files','logical_bytes','unique_within_hashed_16_roots']
with (D/'path-map.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=cols,extrasaction='ignore');w.writeheader();w.writerows(maps)
oldrelease=next(r['source'] for r in v['preservation'] if r['target']==str(R/'releases/r28'))
aliases=[{'old':r['root'],'new':r['target'],'type':'moved-no-old-root'} for r in maps]
aliases.append({'old':oldrelease,'new':str(R/'releases/r28'),'type':'compatibility-junction'})
for a in read(M/'runtime-archives.json'):
    aliases.extend([{'old':a['source'],'new':a['target'],'type':'archived-runtime-or-cache'}, {'old':str(Path(oldrelease)/Path(a['source']).name),'new':a['target'],'type':'historical-runtime-or-cache'}])
before=read(M/'authorization.json')['free_bytes_before'];after=shutil.disk_usage(R).free
status={'utc':now,'scope':'16 specified external roots plus r28 and unique engine entry','B1':'pass: sole integrator; 4 pause confirmations; approved ownership check; no project processes/listeners', 'B2':'pass: r28 physically moved; full seal SHA verified; old delivery junction retained', 'B3':'pass: unique engine source; deterministic product/audio rebuild; root normal entry check-only passed', 'B4':'pass: 14 Lab roots moved; 28 config paths rebound; all 14 MySQL option validations passed', 'B5':'pass: 2 research roots moved; existing in-root P01-P24 historical provenance intentionally retained in place', 'B6':'pass: 10868 safe duplicate deployment paths hardlinked; original bodies recoverably quarantined; rollback probe passed','user_blocker':None,'development_resumed':False,'disk_bytes_reclaimed_from_quarantine':0}
save(M/'status.json',status)
save(M/'verification-correction.json',{'utc':now,'supersedes':['verified-moves-r28.json','verified-moves-external.json'],'reason':'Initial Windows DirEntry.stat inode fields were zero, so the earlier identity-preservation labels were unsupported. Original receipts retained for audit.','replacement':'final-verification.json','proof':'All 26929 external original files and 14494 r28 nonvolatile files SHA checked; 1295 r28 volatile files checked by size and mtime, plus same-volume move receipts. Formal seal 14364 files fully hashed.','volatile_limit':'No full body hash or original real inode claim for the 1295 volatile r28 files.'})
manifest={'schema':'bin-engine-project-index-v2','updated_utc':now,'canonical_root':str(R),'stage':'P24','revision':'r28','whole_project_complete':False,'organization_status':'physical-consolidation-verified-with-recoverable-quarantine','organization_scope':status['scope'],'development_resumed':False,'entry':str(R/'啟動引擎.cmd'),'engine_root':str(R/'engine'),'source_roots':[str(R/'engine'/s) for s in ['source','audio','web']],'current_release':{'root':str(R/'releases/r28'),'sealed':True,'seal_sha256':v['formal_seal_sha256'],'assets':str(R/'releases/r28/assets'),'entry':str(R/'啟動引擎.cmd')},'current_lab':str(R/'environments/p24'),'runtime_ascii_alias':r'D:\BinEngine','mappings_file':str(D/'path-map.json'),'mappings':maps,'path_aliases':aliases,'ownership':{'organizer_thread':'01a18377-e899-75f6-abc1-5d245b37f008','role':'sole migration integrator; handoff only; no new development Goal','development_supervisor':'01a17e6a-9270-7cc2-a338-0120ed68d0ec','development_state':'paused; parent must confirm next sole supervisor and new scope before dispatch'},'external_sources':oldmanifest['external_sources'],'retained_history':'Existing P01-P24 and supervisor trees remain frozen at original in-root paths; do not resume development there','shared_immutable_files':'MySQL binaries, original JAR/maps/SQL are NTFS hardlinks; create independent version before any edit','recovery_root':str(R/'recovery'),'evidence_root':str(R/'evidence'),'reports':{'html':str(D/'index.html'),'progress':str(D/'progress.md'),'migration':str(D/'migration-plan.md'),'handoff':str(D/'HANDOFF.md')},'proof':str(M/'final-verification.json'),'permanent_deletions':0,'disk_bytes_reclaimed_from_quarantine':0,'active_blockers':[]}
save(R/'project-manifest.json',manifest)
result={'schema':'bin-engine-consolidation-result-v2','utc':now,'status':'complete-for-authorized-16-root-consolidation-with-recoverable-quarantine','physical_consolidation_complete':True,'whole_project_complete':False,'canonical_root':str(R),'batches':status,'external_roots_moved':16,'external_original_files_verified':sum(r['files'] for r in maps),'external_original_logical_bytes':sum(r['logical_bytes'] for r in maps),'r28_original_files_accounted':15789,'r28_nonvolatile_full_sha_files':14494,'r28_volatile_metadata_only_files':1295,'r28_sealed_files_full_sha':v['formal_r28_full_sha_files'],'hardlink_replacements':v['hardlink_substitutions'],'recoverable_quarantine_logical_bytes':v['quarantine_logical_bytes'],'permanent_deletions':0,'disk_bytes_reclaimed_from_quarantine':0,'disk_free_before_bytes':before,'disk_free_after_bytes':after,'disk_free_change_bytes':after-before,'disk_change_caveat':'Whole-volume free-space observation; backups/builds and unrelated writers can affect delta. Not a dedupe saving measure.','database_cold_backups':14,'database_config_validations':14,'database_logical_restore_tested':False,'game_operations':0,'services_started_or_stopped':0,'normal_entry_check_only':True,'engine_rebuild_passed':True,'rollback_probe_passed':True,'development_resumed':False,'active_blockers':[],'resolved_issues':['Win32_Process read approved on authorized retry','Moved config writes approved without ACL change','MySQL Unicode option paths fixed through one ASCII junction D:/BinEngine','Integration helper parent-depth assumption fixed in engine only','Initial zero file IDs superseded by full content proof'],'proof':str(M/'final-verification.json'),'rollback_proof':str(M/'rollback-probe.json'),'limitation':'r28 volatile evidence uses size/mtime receipts; DB backup verified at file level only; no native/game/server acceptance performed.'}
save(D/'result.json',result)
table='\n'.join(f"| `{r['root']}` | `{Path(r['target']).relative_to(R)}` | {r['files']:,} | {r['logical_bytes']:,} |" for r in maps)
inventory=f'''# 16 路徑實體整併結果

更新：{now}。16 個來源原本均為實體資料夾，現在均已同磁碟搬入專案根，原 16 路徑已不存在；並非僅建立索引或換名。沒有刪除獨有檔案。

| 原路徑 | 現行根內位置 | 遷移前檔數 | 遷移前邏輯 bytes |
|---|---|---:|---:|
{table}

總計 26,929 檔、9,972,915,133 bytes。這是遷移前內容量，不是整個專案目前實體佔用，也不將硬連結重複計入磁碟節省。

## 分類與去重

- 現用環境：environments/p24，保留實際 DB、設定及 IDA；不與舊階段 DB 合併。
- 歷史環境：history/labs 的 13 個 Lab；DB 狀態、差異設定與 IDA 保留。
- 研究來源：6016 匯出與 ASCII 研究各保留完整來源、IDB、腳本及證據。
- 不可變且內容相同的 MySQL 工具、原 JAR、原地形、原 SQL：570 組，10,868 個部署路徑改用硬連結；原件 2,957,933,947 bytes 置於 recovery/duplicate-quarantine。修改這些檔案前必須建立獨立新版本，禁止原地改寫共享本文。
- 初次靜態盤點共 600 組跨根重複；其餘 30 組涉及設定／證據或用途不宜合併，保留。初次雜湊範圍內有 1,124 個獨有檔，不代表未納入靜態掃描的 DB 也已做語意去重。
- 可重建資料：r28 的 .nuget、.dotnet、tmp 已移入 recovery/r28-build-cache；未永久刪除。engine 新建產物在 engine/build。
- 必要原始資產：封存 assets 留在 releases/r28，engine/assets 為不可寫入的基準 junction；原 BIN、正式客戶端等外部來源保持唯讀，不納入垃圾清理。

原件仍在隔離區，因此本輪去重回收空間為 0 bytes；磁碟餘量前後為 {before:,} → {after:,} bytes，差值 {after-before:,} bytes。差值包含備份、重建與全磁碟其他活動，不能當成去重收益。

## 證據與限制

所有 26,929 個外部原檔已全 SHA 核對；28 個設定檔以遷移前備份 SHA 及授權變更後 SHA 配對。r28 的 14,364 個 seal 檔案全本文核對；其完整搬移清單中另有 1,295 個易變日誌／快取只比大小、mtime 與搬移收據，不宣稱本文雜湊證明。

早期搬移收據把 Windows DirEntry 的零 file ID 當成身分證明，此說法已撤回；以 [更正](physical-migration/verification-correction.json) 及 [最終驗證](physical-migration/final-verification.json) 為準。舊收據保留供稽核。

[機器可讀對照](path-map.json) · [CSV](path-map.csv) · [結果](result.json)
'''
write(D/'inventory-report.md',inventory)
structure='''# 唯一專案結構與入口

唯一實體專案根：`D:\\lineage開發\\TW1608262200研究`。

| 根內位置 | 用途／寫入責任 |
|---|---|
| 啟動引擎.cmd | 正常 r28 客戶端入口；加 --check-only 可檢查但不啟動遊戲 |
| engine/source、audio、web | 後續唯一有效引擎原始碼；由唯一整合者串行合入 |
| engine/vendor、launcher、_integration | 建置相依、啟動器原始碼與驗證工具 |
| engine/build | 可重建產物；本輪重建與 r28 產品／音訊 DLL 相同 |
| engine/assets | 指向 releases/r28/assets 的 junction；封存基準，不得直接改寫 |
| releases/r28 | 正式封存交付；產品 seal 未改 |
| environments/p24 | 現用實驗伺服器／DB／設定；目前停寫，未開機測試 |
| history/labs | 13 個舊 Lab 的狀態與研究歷史 |
| history/research | 6016 匯出、ASCII 研究 |
| evidence/r28-runtime-before-consolidation | 遷移前 runs、runtime 證據 |
| recovery | 冷備份、設定原件、隔離重複原件、舊快取及回復抽查 |
| docs/project-consolidation | 本次報告、索引、基準與執行收據 |
| 既有 P01～P24、主管 | 凍結的階段歷史及權威驗收證據，保持根內原位置 |

既有根內歷史約 112.36 GiB 的原始盤點沒有整批複製或重新搬家；它們已在唯一根內，保留以免破壞歷史相對路徑。此輪沒有宣稱完成所有歷史內容的去重。

## 相容與路徑解析

只保留三個用途明確的相容連結：`D:\\BinEngine` 指向本根（MySQL 選項路徑需要 ASCII）、舊 r28 delivery 指向 releases/r28、engine/assets 指向封存素材。原 16 個 D 槽根資料夾沒有留下相容副本。

14 個 mysql.ini 與 14 個 server.policy 已重綁到新根的 ASCII 別名；憑證、埠號及 policy 權限語意未變。沒有改 ACL、OS 安全設定或 DB 內容。舊報告與歷史腳本保留原文，不代表仍可直接重跑；先以 manifest 對照轉換輸入路徑。正式接續只使用 engine 與 environments/p24。

舊路徑解析範例（僅輸出路徑，不執行歷史程式）：

```powershell
python -X utf8 .\\engine\\tools\\resolve-path.py 'D:\\P24Lab-ClientEngine\\mysql.ini'
```

## 可重跑的必要檢查

在本根執行 `./啟動引擎.cmd --check-only`。建置重現命令為 `python -B -X utf8 engine/_integration/run_stage.py --output engine/_integration/evidence/migration-rebuild-20261028`，既有結果已通過，不需為閱讀報告重跑。該腳本是 r28 基準驗證；未來修改產品時應更新版本與驗收規格，不能沿用舊 seal 宣稱新版本通過。

[manifest](../../project-manifest.json) · [原始碼說明](../../engine/README.md) · [重建結果](../../engine/_integration/evidence/migration-rebuild-20261028/result.json)
'''
write(D/'structure.md',structure)
progress=(D/'progress.md').read_text(encoding='utf-8-sig')
progress=progress.split('## 本輪整理')[0]
progress=progress.replace('本輪只整理、核對、建立索引，不接任開發主管，不操作遊戲或資料庫。','本輪已依使用者後續授權完成 16 根實體整併與可回復去重；不接任開發主管，未操作遊戲或啟動資料庫。')
progress=progress.replace('最新可見對話只回報原生路線前 7 步。','主管本次停寫確認已補正：最後原生收據是第 8 步，位置 (32581,32950)，仍非路線完成。')
progress=progress.replace('主管當時只走到前 7 步，不換算成整段完成。','主管最新停寫確認為第 8 步 (32581,32950)，不換算成整段完成。')
progress=re.sub(r'開發停止的直接證據是.*?\n\n','先前開發停滯的直接證據是主管回合回報 `Selected model is at capacity. Please try a different model.`。其後本輪已取得主管與三位工作者的明確停寫確認，主管 Goal 暫停；目前維持暫停是依使用者「先整理再開發」的要求。主管同時補正最後原生收據為第 8 步 (32581,32950)，仍未完成路線。詳見 physical-migration/pause-confirmations.json。\n\n',progress,count=1)
progress=progress.replace('主管後續只走到第 7 步，不能算成交完成。','主管最新確認為第 8 步 (32581,32950)，仍不能算成交完成。')
progress=progress.replace('本輪只重新固定關鍵雜湊與證據來源，未重新建置封存包，也未重跑遊戲。','本輪已對封存 seal 的 14,364 檔重新作全本文 SHA 核對，另在 engine 重建並比對產品與音訊 DLL 相同；封存包本身未改，未重跑遊戲。')
progress+='''\n## 本輪整理已完成的實際結果

B1～B6 已完成本輪授權的 16 個外部根整併、r28 搬移、engine 建立、相依重綁及可回復去重。詳見 [實體處置](inventory-report.md) 與 [收據](physical-migration/final-verification.json)。目前主管及三個工作者均明確停寫，主管 Goal 已暫停；本輪未建立新 Goal、未恢復開發、未啟動遊戲。

原先 Win32_Process 的權限阻礙已經使用者新授權後的審查允許讀取解除；沒有停止任何程序。唯一既有 mysqld 屬系統 MySQL 5.5，未觸碰。MySQL 的中文路徑問題以單一 ASCII junction 解決，14 環境選項驗證皆通過。這是配置檢查，不等於資料庫引擎啟動或完整遊戲驗收。

最後原生驗收仍以主管停寫回覆第 8 步 (32581,32950) 為準。以上功能進度不因整理成功而升格：map4/6016、原生擷取、同版完整流程、來源缺口仍須後續驗證。
'''
write(D/'progress.md',progress)
migration='''# B1–B6 執行結果與回復

本文件已由計畫更新為實際結果；初版文件保存在 physical-migration/phase1-documents.zip。整理不等於遊戲專案完成。

| 批次 | 實際處置與完成門檻 | 結果 |
|---|---|---|
| B1 | 使用者指定唯一整合者；主管及三工作者停寫確認；讀程序與埠、空間、完整檔案基準；14 組 DB 冷備份 | 通過，未停未知程序 |
| B2 | r28 同磁碟搬至 releases/r28；原 delivery 為相容 junction；seal 全本文核對 | 通過，封存產品未改 |
| B3 | 建立 engine 唯一來源；修正整合工具固定祖先層數問題；產品與音訊可重現；正常入口 check-only | 通過，未啟動 UI／網路 |
| B4 | 14 Lab 分別搬至現用 environments/p24 或 history/labs；28 設定檔重綁；14 mysql validate-config | 通過，無 DB 內容修改 |
| B5 | 2 外部研究根搬至 history/research；原本已在根內的階段歷史保留；runs/runtime 與建置快取分開封存 | 通過，來源與證據保留 |
| B6 | 僅白名單不可變檔全 SHA 判重；10,868 原件移入可回復隔離；部署位置硬連結；原件回復／重套抽查 | 通過，永久刪除 0、空間回收 0 |

## 已解決問題

Win32_Process 初次遭拒絕後，使用者補充授權，合規審查允許一次重試讀取並成功。搬移後設定檔繼承原權限，正常寫入被拒後以已授權審查執行成功，未改 ACL。MySQL 中文選項路徑導致 Illegal byte sequence，改用 D:\\BinEngine 別名後全數通過。建置 helper 的固定祖先層數已只在 engine 修正。早期零 file ID 證明已正式更正，由全本文核對取代。

## 回復順序

1. 再確認本專案停止寫入；讀 migration journals、before snapshots、config-relocations、dedupe-plan。不可直接重跑搬移腳本。
2. 對指定 hardlink 檔，先將現行連結移入暫存保留，再將 quarantine 原件移回；比 SHA。已有 mysql.exe 的實際往返成功收據。保留回復動作紀錄，沒有必要永久刪除。
3. 需要回舊路徑時，先用 config-before-relocation 的原件還原設定，再按外部搬移 journal 反向逐根移回；每次檢查絕對來源／目的邊界及目的不存在。
4. r28 回復先移開相容 junction（只處理已核對的連結本身），再按 journal 將 releases/r28 移回；runs/runtime/cache 依 runtime-archives 對照移回。不可遞迴刪除連結目標。
5. DB 冷備份解壓與 SHA 已驗證，並抽取 3 個檔案回復比對；未做資料庫邏輯恢復或啟動一致性檢查，若需回復運作須另以受控停寫環境驗收。

[MySQL 官方文件](https://dev.mysql.com/doc/refman/8.0/en/server-configuration-validation.html) 說明 validate-config 不初始化儲存引擎；因此本輪不把配置檢查宣稱成 DB 啟動通過。

## 下一步與獨立子任務（尚未派出）

1. 父對話讀回本次 HANDOFF／result、確認唯一開發主管及新 Goal。原整理者只交付，不成為第二主管。現有開發 Goal 仍暫停。
2. 先安排唯一環境負責人，以新 environments/p24 與 ASCII 別名正常啟動實驗服務、登入／保存／重登煙霧驗證；不變更角色資料製造通過。若環境驗收失敗先修遷移相依，不推進功能。
3. 可平行的唯讀任務 A：來源完整性／缺少154特效與 map-local effect 關聯證據，輸出明確來源 ID、hash、缺口，不改 source/assets。
4. 可平行的隔離任務 B：原生擷取逾時重現與最小修復候選，專屬 work/<派工ID>，不得搶佔遊戲／服務或改共享入口；實機操作由唯一主管串行。
5. 唯一主管安排 map4/6016 播放、離區退役、音量行為與同版完整服務流程驗收。避開已造成死亡的危險港口路線；不要用模組測量替代場景證據。
6. 唯一整合者審查候選後合入 engine，重建新版本、核對資源對帳，通過才產出下一個 releases/<revision>。其他工作者不得發布或直接改封存 r28。

每個子任務需明列基準、專屬輸出、禁止共享區、完成標準與證據；任務真正需要才建 work 子目錄，不預建空資料夾。恢復開發需父對話落實主管／新目標的既有要求；本輪沒有額外必要的使用者批准阻礙。清空隔離或永久刪除不在此次授權內，不自動執行。
'''
write(D/'migration-plan.md',migration)
handoff=f'''# 整理交付

完成時間：{now}。唯一根 `{R}`。B1～B6 已完成授權的 16 根實體整併與可回復去重；未恢復開發。

- 16 個舊 D 槽根已不存在；全部內容搬至根內的 environments/history。r28 已集中 releases/r28；engine 是後續唯一有效來源。
- 原始26,929檔全 SHA 核對；r28 seal 14,364檔全 SHA；正常入口 --check-only、engine 重建及14組 MySQL 配置驗證通過。
- 10,868 個重複部署檔改為不可變硬連結，2,957,933,947 bytes 原件仍在 recovery/duplicate-quarantine。回復抽查通過；永久刪除0，回收空間0。
- 14組 DB 冷備份及原設定已保留；只做檔案解壓／SHA回復，未驗 DB 啟動。1,295個r28易變證據僅核對大小／mtime／搬移收據。
- D:\\BinEngine 是中文路徑相容 junction，非另一專案複本；既有 P01～P24 根內歷史及必要外部原始資產保留。
- 主管與三工作者停寫，無現行專案監聽埠；系統 MySQL 未觸碰。沒有待使用者解除的權限阻礙。仍未完成的 P24 遊戲驗收見 progress。

閱讀順序：[總覽](index.html) → [功能進度](progress.md) → [16路徑](inventory-report.md) → [結構](structure.md) → [下一步](migration-plan.md)。

機器收據：[result](result.json)、[manifest](../../project-manifest.json)、[最終保全](physical-migration/final-verification.json)、[回復抽查](physical-migration/rollback-probe.json)、[驗證更正](physical-migration/verification-correction.json)。
'''
write(D/'HANDOFF.md',handoff)
readme='''# 自主 BIN 引擎專案

唯一專案根：`D:\\lineage開發\\TW1608262200研究`。

16 個外部資料夾已完成實體整併、來源核對及可回復去重。開發停留 P24／r28；整理完成不代表遊戲驗收完成。主管及工作者仍暫停，待本次交付接續新目標。

- [整理總覽（HTML）](docs/project-consolidation/index.html)
- [功能進度與未驗項目](docs/project-consolidation/progress.md)
- [16 路徑對照與分類](docs/project-consolidation/inventory-report.md)
- [專案結構與入口](docs/project-consolidation/structure.md)
- [執行收據、回復與下一步](docs/project-consolidation/migration-plan.md)
- [機器可讀 manifest](project-manifest.json)

正常客戶端入口：[啟動引擎.cmd](啟動引擎.cmd)。本輪只以 `--check-only` 核對，沒有啟動遊戲／服務。後續原始碼在 [engine](engine/README.md)，正式封存為 releases/r28，現用環境為 environments/p24。

原件保存在 recovery；重複檔以不可變硬連結共用，原重複本文仍可回復，因此本輪回收磁碟空間為 0。D:\\BinEngine 只是本根的 ASCII 相容連結，沒有第二套實體專案。既有 P01～P24 目錄是根內研究歷史，禁止當成新的開發入口。
'''
write(R/'README.md',readme)
# A self-contained local HTML document; no CDN, telemetry, or asset upload.
sections=[('交付摘要','HANDOFF.md'),('功能進度','progress.md'),('16 路徑盤點','inventory-report.md'),('專案結構','structure.md'),('批次與下一步','migration-plan.md')]
def inline(s):
    s=html.escape(s)
    s=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',lambda m:f'<a href="{m[2]}">{m[1]}</a>',s)
    s=re.sub(r'`([^`]+)`',r'<code>\1</code>',s);s=re.sub(r'\*\*([^*]+)\*\*',r'<strong>\1</strong>',s)
    return s
def render(s):
    out=[];intable=False;code=False
    for line in s.splitlines():
        if line.startswith('```'):
            if intable:out.append('</table>');intable=False
            out.append('</pre>' if code else '<pre>');code=not code;continue
        if code:out.append(html.escape(line)+'\n');continue
        if line.startswith('|'):
            if not intable:out.append('<table>');intable=True
            cells=line.strip('|').split('|')
            if all(re.fullmatch(r'\s*:?-+:?\s*',c) for c in cells):continue
            out.append('<tr>'+''.join('<td>'+inline(c.strip())+'</td>' for c in cells)+'</tr>');continue
        if intable:out.append('</table>');intable=False
        if line.startswith('#'):
            level=min(len(line)-len(line.lstrip('#')),4);out.append(f'<h{level}>{inline(line.lstrip("# "))}</h{level}>')
        elif line.strip():out.append('<p>'+inline(line)+'</p>')
    if intable:out.append('</table>')
    return '\n'.join(out)
body=''.join(f'<section id="s{i}">'+render((D/file).read_text(encoding='utf-8'))+'</section>' for i,(title,file) in enumerate(sections))
nav=' · '.join(f'<a href="#s{i}">{html.escape(title)}</a>' for i,(title,file) in enumerate(sections))
write(D/'index.html','<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BIN 引擎專案整併交付</title><style>body{max-width:1180px;margin:32px auto;padding:0 24px;font:16px/1.7 system-ui;color:#15283b;background:#f6f8fa}nav{padding:16px;background:#e4edf6}section{background:white;padding:24px;margin:24px 0;border:1px solid #d5dfe9;border-radius:8px}table{border-collapse:collapse;width:100%;font-size:14px}td{border:1px solid #cbd5df;padding:8px;overflow-wrap:anywhere}tr:first-child{font-weight:600;background:#edf2f7}code{overflow-wrap:anywhere;background:#f1f4f7}pre{white-space:pre-wrap;background:#eff3f7;padding:16px}a{color:#145c9d}h1{font-size:27px}h2{font-size:21px}p{overflow-wrap:anywhere}@media print{body{margin:0;padding:0;background:white}section{break-before:page;border:0}nav{display:none}}</style><h1>專案整併完成・P24 驗收待續</h1><p>實體集中／來源保全／可回復去重。未恢復開發，未永久刪除，未宣稱空間回收。</p><nav>'+nav+'</nav>'+body+'</html>')
# Restore path resolver checks, small-file links, report checksums.
resolver=[]
for old in [r'D:\P24Lab-ClientEngine\mysql.ini',r'D:\P10Lab-TW13081901\mysql.ini',r'D:\TW1608262200_research',oldrelease,str(Path(oldrelease)/'runs')]:
    p=subprocess.run([sys.executable,'-X','utf8',str(R/'engine/tools/resolve-path.py'),old],capture_output=True,text=True,encoding='utf-8');assert p.returncode==0,p.stderr;resolver.append({'old':old,'resolved':p.stdout.strip()})
links=[]
for p in [R/'README.md',D/'index.html']+[D/f for _,f in sections]:
    text=p.read_text(encoding='utf-8');urls=re.findall(r'href="([^"]+)"',text) if p.suffix=='.html' else re.findall(r'\]\(([^)]+)\)',text)
    for url in urls:
        if url.startswith(('http:','https:','#')):continue
        target=(p.parent/html.unescape(url)).resolve();assert target.exists(),(str(p),url)
        links.append({'document':str(p.relative_to(R)),'target':str(target)})
deliverables=[R/'README.md',R/'project-manifest.json',D/'index.html',D/'progress.md',D/'structure.md',D/'inventory-report.md',D/'migration-plan.md',D/'HANDOFF.md',D/'result.json',D/'path-map.csv',D/'path-map.json',M/'final-verification.json',M/'rollback-probe.json',M/'verification-correction.json']
save(D/'deliverables-sha256.json',[{'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)} for p in deliverables])
save(M/'report-verification.json',{'utc':now,'status':'pass','maps':16,'old_roots_absent':True,'html_utf8':True,'local_links_verified':links,'historical_path_resolver_checks':resolver,'rollback_hardlink_inode_verified_after_probe':True,'no_project_processes_or_listeners':True})
print(json.dumps({'status':result['status'],'report':str(D/'index.html'),'maps':16,'links_checked':len(links),'disk_free_change_bytes':after-before},ensure_ascii=False))
