"""Only immutable tool/original JAR/map/SQL copies; never DB/config/private/runtime."""
import json,gzip,collections,hashlib
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2]
move_rows=[json.loads(l) for l in (OUT/'moves-external.jsonl').read_text(encoding='utf-8-sig').splitlines() if l.strip()]
moves=[r for r in move_rows if r['state']=='moved'];groups=collections.defaultdict(list)
def allowed(rel):
    p=Path(rel)
    if p.suffix.lower() in {'.log','.err','.pid','.cnf','.ini'}:return False
    return rel.startswith('mysql/') or rel.startswith('server/jar/') and p.suffix.lower()=='.jar' or rel=='server/Server_Game.jar' or rel.startswith('server/maps/') and p.suffix.lower()=='.txt' or rel.startswith('sql/') and p.suffix.lower()=='.sql'
for move in moves:
    with gzip.open(OUT/(move['snapshot']+'.jsonl.gz'),'rt',encoding='utf-8') as f:
        for line in f:
            r=json.loads(line)
            if not allowed(r['path']) or not r['bytes']:continue
            p=Path(move['target'])/r['path'];s=p.stat()
            assert s.st_size==r['bytes'] and s.st_mtime_ns==r['mtime_ns'],str(p)
            # Windows DirEntry.stat may report file_id=0; never treat it as identity.
            with p.open('rb') as body:assert hashlib.file_digest(body,'sha256').hexdigest()==r['sha256'],str(p)
            groups[(r['bytes'],r['sha256'])].append({'path':str(p),'old_root':move['source'],'relative':r['path'],'file_id':s.st_ino,'mtime_ns':s.st_mtime_ns})
plan=[];group_receipts=[]
for (size,h),members in groups.items():
    if len(members)<2 or len({m['old_root'] for m in members})<2:continue
    members.sort(key=lambda m:(0 if m['old_root']==r'D:\P24Lab-ClientEngine' else 1,m['path']))
    canonical=members[0]
    with Path(canonical['path']).open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==h
    group_receipts.append({'sha256':h,'bytes':size,'canonical':canonical['path'],'members':len(members)})
    for m in members[1:]:
        q=ROOT/'recovery/duplicate-quarantine'/Path(m['path']).relative_to(ROOT)
        assert not q.exists()
        plan.append({'source':m['path'],'canonical':canonical['path'],'quarantine':str(q),'sha256':h,'bytes':size,'before_file_id':m['file_id'],'before_mtime_ns':m['mtime_ns'],'reason':'immutable original tool/JAR/map/SQL content matches full pre-migration SHA; filename and deployment location retained with hardlink'})
(OUT/'dedupe-plan.json').write_text(json.dumps({'mode':'move original duplicate into project quarantine, replace deployment path with hardlink; no permanent deletion','space_released_by_quarantine':0,'entries':plan,'groups':group_receipts},ensure_ascii=False,indent=2),encoding='utf-8')
print('planned',len(plan),'recoverable substitutions in',len(group_receipts),'groups; quarantined bytes',sum(r['bytes'] for r in plan))
