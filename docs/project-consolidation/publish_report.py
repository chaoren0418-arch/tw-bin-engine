"""Assemble local-only report, manifest and integrity receipt from captured evidence."""
import json,datetime,html,re,csv,gzip,shutil,collections
from pathlib import Path
from inventory import ROOT,OUT,sha
P24=next(ROOT.glob('P24-*'));D=P24/'reference/development/release-integration-r12/delivery'
S=ROOT/'主管/P24-本機主管接管與完整整合-20261028'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def fmt(n):return f'{n/2**30:.3f}'
inv=read(OUT/'inventory.json');hs=read(OUT/'hash-summary.json');ds=read(OUT/'duplicate-groups.json');pins=read(OUT/'r28-pins.json');dep=read(OUT/'path-dependencies.json')
mapping=[];hmap={r['root']:r for r in hs['roots']}
for row in inv['roots'][:-1]:
    p=Path(row['root']);n=p.name
    if n=='P24Lab-ClientEngine':role='現用隔離伺服器／DB／IDA；保存狀態不可丟棄';target='environments/p24';batch='B4'
    elif n=='P24Lab-6016-Export-r1':role='6016 IDA IDB、匯出與契約來源';target='history/research/6016-export-r1';batch='B5'
    elif n=='TW1608262200_research':role='早期 ASCII 研究／多份獨有 IDB 與輸出';target='history/research/ascii';batch='B5'
    else:role='歷史隔離伺服器／DB'+('／獨有 IDA 分析' if n.startswith(('P21','P22','P23')) else '／階段設定');target='history/labs/'+n;batch='B4'
    mapping.append({**row,**hmap[str(p)],'root_type':'physical-directory','role':role,'canonical_root':str(ROOT),'proposed_target':str(ROOT/target),'migration_state':'planned-not-moved','batch':batch})
save(OUT/'path-map.json',mapping)
with (OUT/'path-map.csv').open('w',encoding='utf-8-sig',newline='') as f:
    fields=['root','files','logical_bytes','root_type','role','hashed_files','unique_within_hashed_16_roots','excluded_files','proposed_target','migration_state','batch'];w=csv.DictWriter(f,fields,extrasaction='ignore');w.writeheader();w.writerows(mapping)
lines=['# 16 路徑盤點與資料分類','',f"量測時間：{inv['created_utc']}。非原子快照；容量為檔案邏輯長度，並非實際配置磁區或可直接回收空間。",'',f"16 根合計 **{sum(r['files'] for r in mapping):,} 檔、{fmt(sum(r['logical_bytes'] for r in mapping))} GiB**；既有權威根另有 **{inv['roots'][-1]['files']:,} 檔、{fmt(inv['roots'][-1]['logical_bytes'])} GiB**。本輪自建 docs 不納入掃描。",'', '16 個最上層皆為實體目錄；掃描全部 17 根共跳過 57 個內部 reparse point，詳細目標在 inventory.json。沒有跟隨連結；沒有讀取錯誤。全部舊路徑保持原地，以下新路徑是計畫，尚未建立或搬移。','', '| 舊路徑 | 檔數 | GiB | 雜湊檔／範圍內獨有檔 | 分類 | 根內計畫位置 |','|---|---:|---:|---:|---|---|']
for r in mapping:lines.append(f"| {r['root']} | {r['files']:,} | {fmt(r['logical_bytes'])} | {r['hashed_files']:,}／{r['unique_within_hashed_16_roots']:,} | {r['role']} | {Path(r['proposed_target']).relative_to(ROOT).as_posix()} |")
lines += ['', '## 重複、獨有及不可直接刪除的內容','',f"對 16 根內已知可變／私密目錄以外的 **{sum(r['hashed_files'] for r in mapping):,} 檔、{fmt(sum(r['hashed_bytes'] for r in mapping))} GiB** 實讀 SHA-256；全部在單檔雜湊前後大小與時間穩定，無讀取錯誤。{sum(r['excluded_files'] for r in mapping):,} 檔未讀本文，主要為 DB、runtime、私密設定等；僅計數與長度，非備份或一致性驗證。",'',f"發現 **{len(ds)} 組跨根同內容**，同內容多份的邏輯差額約 **{fmt(sum((len(r['files'])-1)*r['bytes_each'] for r in ds))} GiB**。這只是 16 根已雜湊集合的重複量，不是安全刪除量，也不是硬碟實際可回收值。",'', '14 個 Lab 的 Server_Game.jar 全部是 d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf，MySQL 主程式與固定 SQL 有大量相同本文；各 Lab 的設定、port、DB 身分及角色資料仍不同，整個資料夾不是可互換複本。', '',f"同一雜湊只出現在單一指定根內的檔案共 **{sum(r['unique_within_hashed_16_roots'] for r in mapping):,} 個**，逐檔記於 unique-static-files.jsonl。『獨有』只代表已雜湊的 16 根，未對權威根 112 GiB 或其他磁碟做全面內容去重；不能稱為全機唯一。",'', 'P21／P22／P23／P24 的 TW1608262200.idb 雜湊各異；6016 export 的 IDB 也不同。早期 ASCII research 多個 IDB 各異，即使大小相同也可能內容不同。IDA 分析狀態及 exports 屬研究證據，不能用原 BIN 一樣就判定可刪。','', '| 類別 | 例子 | 處置 |','|---|---|---|','| 已證重複靜態本文 | MySQL binaries、Server_Game.jar、部分 SQL | 先評估隔離／重建契約，再集中工具或封存；本輪保留 |','| 已知獨有或版本差異 | IDB、IDA exports、階段 config／policy、操作收據 | 保留 hash 與來源鏈，逐批移到 history |','| 可重建候選 | bin/obj、SDK cache、helper build、臨時檔 | 要先有完整 source/vendor/SDK 及成功重建收據，不能直接清除 |','| 可變保存／敏感資料 | db-data、runtime、private 設定、角色快照 | 核對服務與一致性備份；不輸出帳密，不視為可重建 cache |','| 必要原始素材 | 原 BIN、客戶端 map/Tile/Sprite 等、r28 封裝 assets | 保留權威來源與 pins；不把來源缺口以其他版本素材填補 |','| 驗收證據／失敗紀錄 | 角色死亡、回城、旅館、音訊量測、錯誤 log | 保留可追溯證據；大 log 需先擷取索引及核對保留政策 |','', '## 權威根的主要空間來源','', '| 位置 | GiB |','|---|---:|']
for n,b in list(inv['roots'][-1]['top_level_bytes'].items())[:8]:lines.append(f'| {n} | {fmt(b)} |')
size=read(OUT/'r28-size-breakdown.json')
lines+=['',f"r28 delivery 實際目錄 {fmt(sum(size.values()))} GiB，其中 assets {fmt(size['assets'])} GiB，runs {fmt(size['runs'])} GiB。runs 是實測輸出，可變且不在 seal 內；不能把約 4.95 GiB 的 log 直接當無用檔刪掉，也不能把它算作必要發布包。runtime／cache 同樣需分離。",'',f"相依掃描讀取 {dep['text_files_scanned']:,} 個 script/source/config 文字檔，產生 {len(dep['hits']):,} 筆帶檔案與行號的路徑命中。不是完整靜態／動態相依分析；排除範圍、解碼失敗及相對父目錄限制見 path-dependencies.json。",'', '## 機器清單與量測邊界','', '- inventory.json：根容量、檔數、各 top-level 分佈、57 個連結目標。','- file-metadata.jsonl.gz：725,666 個實體檔的路徑／大小／mtime；不含檔案本文。','- static-hashes.jsonl、duplicate-groups.json、unique-static-files.jsonl：16 根有限範圍 SHA 去重與逐檔明細。','- hash-summary.json：每根雜湊／排除統計及完整排除檔清單。','- path-map.json／path-map.csv：舊新路徑、分類及「planned-not-moved」狀態。','- size-breakdown.json、r28-size-breakdown.json：容量熱點；不是可刪除名單。','- r28-pins.json：本輪重新量測的關鍵版本 SHA。','', '掃描時不讀資料庫本文、不連服務、不追連結；沒有取得全機程序清單。修改時間、stable_during_hash 與對話 idle 都不能證明所有背景服務已停。']
(OUT/'inventory-report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
# Preserve concise evidence and verify each claimed fixed pin against accepted supervisor receipt.
expected=read(S/'formal-r28-review/root-review.json');actual={Path(r['path']).name:r['sha256'] for r in pins}
checks={
 'seal':actual['candidate-seal-manifest.json']==expected['sealSha256'],
 'productDll':actual['P11.ClientEngine.dll']==expected['productDllSha256'],
 'audioDll':actual['P24.Audio.dll']==expected['audioDllSha256'],
 'productManifest':actual['product-r28-manifest.json']==expected['productManifestSha256'],
 'launcherDll':actual['P24.Launcher.dll']==expected['launcherDllSha256'],
 'assetReceipt':actual['full-assets-verification.json']==expected['fullAssetReceiptSha256']}
assert all(checks.values()),checks
sources=[S/'completion-matrix.json',S/'task-queue.json',S/'native-r28/current-status.json',S/'formal-r28-review/root-review.json',D/'_integration/HANDOFF.md',D/'_integration/result.json',D/'新版客戶端使用說明.md',P24/'prepare.py',P24/'lab.py',P24/'cache_sources.py',P24/'cache_assets.py']
sources += [S/'native-r28'/n for n in ['normal-save-review.json','ground-review.json','inn-room-review.json','cave-entry-review.json','cave-eye-combat-review.json','cave-home-save-review.json','bonus-strength-save-review.json','holy-weapon-review.json','active-village-services-review.json']]
save(OUT/'evidence-index.json',{'read_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'r28_pin_checks':checks,'sources':[{'path':str(p),'sha256':sha(p),'bytes':p.stat().st_size} for p in sources],'resource_correction':{'provenance':'parent delegation with trusted user transcript/history; partially corroborated by cave-home-save and original source rules; no new DB query','greenPotion':'object10386/item40013 count3','homeScroll':'object10404/item40079 3->2 saved','innKey':'object10326/item49290 saved then absent about two hours later; direct deletion cause not established'},'caution':'not all historical evidence bodies independently revalidated; runtime tests not rerun'})
manifest={'schema':'bin-engine-project-index-v1','updated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'canonical_root':str(ROOT),'stage':'P24','revision':'r28','whole_project_complete':False,'organization_status':'index-unified-physical-migration-pending','current_release':{'root':str(D),'entry':str(D/'啟動新版客戶端.cmd'),'source':str(D/'source'),'audio':str(D/'audio'),'web':str(D/'web'),'assets':str(D/'assets'),'build':str(D/'build'),'launcher':str(D/'launcher'),'seal':str(D/'_integration/candidate-seal-manifest.json'),'sealed':True,'modify_in_place':False,'full_native_acceptance':False},'evidence_root':str(S),'historical_stage_root':str(P24),'candidate_root':str(P24/'reference/development'),'current_lab':r'D:\P24Lab-ClientEngine','reports':{'html':str(OUT/'index.html'),'progress':str(OUT/'progress.md'),'inventory':str(OUT/'inventory-report.md'),'structure':str(OUT/'structure.md'),'next_steps':str(OUT/'migration-plan.md')},'mappings_file':str(OUT/'path-map.json'),'mappings':mapping,'ownership':{'organizer_thread':'01a18377-e899-75f6-abc1-5d245b37f008','organizer_scope':'docs/root index only; not supervisor','last_supervisor':'01a17e6a-9270-7cc2-a338-0120ed68d0ec','last_integrator':'01a17a22-17e4-70c0-9792-166ddb86ea42','new_dispatch':False},'blocked_actions':[{'action':'read process ownership','target':'Win32_Process','outcome':'denied','reason':'OS Access denied; no relocation/stop attempted'},{'action':'create/read formal Goal','target':'organizer conversation','outcome':'unavailable','reason':'get_goal/create_goal absent from available tools'}],'external_sources':[r'D:\lineage開發\0-客戶端',r'D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin'],'r28_pins':pins,'files_moved':0,'existing_files_deleted':0,'database_operations':0,'game_operations':0}
save(ROOT/'project-manifest.json',manifest)
readme=f'''# 自主 BIN 引擎 — 專案入口

唯一專案根：`{ROOT}`。目前 **P24 / r28**，離線交付已接受，完整同版實機驗收未完成。

- [完整彙整報告（HTML）](docs/project-consolidation/index.html)
- [進度與未完成項](docs/project-consolidation/progress.md)
- [16 路徑盤點](docs/project-consolidation/inventory-report.md)
- [結構／建置契約](docs/project-consolidation/structure.md)
- [遷移批次與下一步](docs/project-consolidation/migration-plan.md)
- [機器可讀專案 manifest](project-manifest.json)

最新固定產品位於 `{D.relative_to(ROOT).as_posix()}`，正常入口是其中的 `啟動新版客戶端.cmd`。請先讀同目錄使用說明；不要使用 P24 根目錄同名舊入口當作 r28。

原始碼基準是該交付的 `source/`、`audio/`、`web/`；封存包不可原地修改，後續由唯一整合者在隔離工作範圍維護下一版。既有 P01～P24 與主管目錄是研究／驗收歷史。

本輪僅完成索引統一。16 個外部資料夾仍在原地，沒有刪除或搬移；實體整併依遷移批次辦理。既有 AGENTS.md 與主管所有權不因本 README 改變。此整理對話不喚醒舊主管、不派遊戲測試。
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
# Minimal deterministic Markdown rendering: local-only, no remote fonts/scripts.
def inline(s):
    s=html.escape(s)
    s=re.sub(r'`([^`]+)`',r'<code>\1</code>',s)
    s=re.sub(r'\*\*([^*]+)\*\*',r'<strong>\1</strong>',s)
    return s
def render(txt):
    out=[];table=False;code=False
    for line in txt.splitlines():
        if line.startswith('```'):
            out.append('</code></pre>' if code else '<pre><code>');code=not code;continue
        if code:out.append(html.escape(line)+'\n');continue
        if line.startswith('|'):
            if not table:out.append('<div class="table"><table>');table=True
            cells=line.strip('|').split('|')
            if all(re.fullmatch(r'\s*:?-+:?\s*',c) for c in cells):continue
            out.append('<tr>'+''.join('<td>'+inline(c.strip())+'</td>' for c in cells)+'</tr>');continue
        if table:out.append('</table></div>');table=False
        if not line.strip():continue
        m=re.match(r'^(#{1,3}) (.*)',line)
        if m:out.append(f'<h{len(m[1])}>{inline(m[2])}</h{len(m[1])}>')
        else:out.append('<p>'+inline(line)+'</p>')
    if table:out.append('</table></div>')
    return '\n'.join(out)
sections=[('progress','進度','progress.md'),('inventory','盤點','inventory-report.md'),('structure','結構','structure.md'),('next','下一步','migration-plan.md')]
body=''.join(f'<section id="{i}">{render((OUT/f).read_text(encoding="utf-8"))}</section>' for i,t,f in sections)
doc='''<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>自主 BIN 引擎｜進度與專案彙整</title><style>body{font:16px/1.7 "Microsoft JhengHei",sans-serif;margin:0;color:#253044;background:#f5f7fa}nav{position:sticky;top:0;background:#142c43;padding:14px 5%;display:flex;gap:24px}nav a{color:white;text-decoration:none}main{max-width:1200px;margin:24px auto;padding:0 24px}section{background:white;padding:32px;margin:24px 0;border-radius:10px;scroll-margin-top:70px}h1{font-size:28px;color:#143e59}h2{font-size:21px;border-bottom:2px solid #c9dde4;padding-top:20px}.table{overflow:auto}table{border-collapse:collapse;width:100%;font-size:14px}td{border:1px solid #dbe4e9;padding:10px;vertical-align:top;overflow-wrap:anywhere}tr:first-child{font-weight:bold;background:#eaf1f5}code{background:#eff3f6;padding:2px 4px;overflow-wrap:anywhere}pre{white-space:pre-wrap;background:#eff3f6;padding:16px}p{overflow-wrap:anywhere}@media print{nav{display:none}section{margin:0;padding:0;break-before:page}body{background:white}td{font-size:10px}}</style><nav>'''+''.join(f'<a href="#{i}">{t}</a>' for i,t,f in sections)+'</nav><main>'+body+'</main></html>'
(OUT/'index.html').write_text(doc,encoding='utf-8')
print('REPORT/MANIFEST GENERATED',checks)
