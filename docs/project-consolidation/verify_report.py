"""Validate report references and captured evidence, without executing game/build tools."""
import json,gzip,re,datetime,hashlib
from pathlib import Path
from html.parser import HTMLParser
from inventory import ROOT,OUT,sha
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
checks=[]
for p in list(OUT.glob('*.json'))+[ROOT/'project-manifest.json']:
    read(p)
checks.append('all JSON parses')
m=read(ROOT/'project-manifest.json');assert len(m['mappings'])==16
for row in m['mappings']:
    p=Path(row['root']);assert p.is_dir() and not p.is_junction() and not p.is_symlink();assert row['migration_state']=='planned-not-moved'
checks.append('16 original roots still physical directories; migration planned only')
for p in m['reports'].values():assert Path(p).is_file()
for key in ['root','entry','source','audio','web','assets','build','launcher','seal']:assert Path(m['current_release'][key]).exists()
checks.append('all manifest report and current release paths exist')
for label,rel in re.findall(r'\[([^\]]+)\]\(([^)]+)\)',(ROOT/'README.md').read_text(encoding='utf-8')):assert (ROOT/rel).exists(),rel
checks.append('root README links resolve')
class Page(HTMLParser):
    def __init__(self):super().__init__();self.ids=set();self.targets=set();self.tables=0;self.remote=[]
    def handle_starttag(self,t,attrs):
        a=dict(attrs)
        if t=='section':self.ids.add(a.get('id'))
        if t=='a' and a.get('href','').startswith('#'):self.targets.add(a['href'][1:])
        if t=='table':self.tables+=1
        for key in ['src','href']:
            if a.get(key,'').startswith(('https://','http://')):self.remote.append(a[key])
page=Page();text=(OUT/'index.html').read_text(encoding='utf-8');page.feed(text)
assert page.ids=={'progress','inventory','structure','next'} and page.targets==page.ids and page.tables>=7 and not page.remote and '\ufffd' not in text
checks.append('HTML four sections, navigation/table structure and local-only content validated')
inv=read(OUT/'inventory.json');counts={};sizes={};n=0
with gzip.open(OUT/'file-metadata.jsonl.gz','rt',encoding='utf-8') as f:
    for l in f:
        r=json.loads(l);counts[r['root']]=counts.get(r['root'],0)+1;sizes[r['root']]=sizes.get(r['root'],0)+r['bytes'];n+=1
for r in inv['roots']:assert counts[r['root']]==r['files'] and sizes[r['root']]==r['logical_bytes']
assert n==725666 and len(inv['links'])==57 and not inv['errors']
checks.append('compressed 725666-file metadata reconciles with all 17 root totals; 57 links excluded')
e=read(OUT/'evidence-index.json');assert all(e['r28_pin_checks'].values())
for r in m['r28_pins']:assert sha(Path(r['path']))==r['sha256']
checks.append('seven current r28 key hashes unchanged; six accepted-receipt comparisons pass')
for p in OUT.glob('*.py'):compile(p.read_text(encoding='utf-8'),str(p),'exec')
checks.append('inventory/report scripts parse; no runtime/game test executed')
result={'status':'complete-phase1-report-and-index','physical_consolidation_complete':False,'wholeP24Complete':False,'validated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'checks':checks,'external_roots':16,'external_files':sum(r['files'] for r in inv['roots'][:-1]),'external_logical_bytes':sum(r['logical_bytes'] for r in inv['roots'][:-1]),'canonical_root_files':inv['roots'][-1]['files'],'canonical_root_logical_bytes':inv['roots'][-1]['logical_bytes'],'hash_scope':'16 roots excluding declared volatile/private paths; not a full canonical-root duplicate audit','hash_files':13481,'duplicate_content_groups':600,'unique_files_within_hash_scope':1124,'dependency_scanned_text_files':24282,'dependency_hits':1481,'dependency_non_utf8_skips':6,'writes':{'root_files':['README.md','project-manifest.json'],'documentation_root':str(OUT),'existing_original_files_deleted':0,'original_files_moved':0,'links_changed':0,'services_stopped':0,'database_operations':0,'game_operations':0,'generated_intermediate_removed':'file-metadata.jsonl, only after compressed byte equality check'},'blockers':m['blocked_actions'],'next':'Parent decides B1 ownership/stop evidence and concrete migration batches; no old development chats awakened','deliverables':[str(OUT/'index.html'),str(ROOT/'README.md'),str(ROOT/'project-manifest.json'),str(OUT/'path-map.csv'),str(OUT/'HANDOFF.md')]}
(OUT/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
files=[ROOT/'README.md',ROOT/'project-manifest.json']+[p for p in OUT.iterdir() if p.is_file() and p.name!='deliverables-sha256.json']
(OUT/'deliverables-sha256.json').write_text(json.dumps({'generated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':[{'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)} for p in files]},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':result['status'],'checks':checks,'artifact_files':len(files)},ensure_ascii=False))
