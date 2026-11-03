import sys,json,gzip,hashlib,os,datetime
from pathlib import Path
from snapshot import walk,digest
OUT=Path(__file__).parent
batch=sys.argv[1];journal=OUT/('moves-'+batch+'.jsonl');rows=[json.loads(l) for l in journal.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
results=[]
for move in rows:
    if move['state']!='moved':continue
    target=Path(move['target']);snap=OUT/(move['snapshot']+'.jsonl.gz');expected={r['path']:r for r in (json.loads(l) for l in gzip.open(snap,'rt',encoding='utf-8'))};actual={p.relative_to(target).as_posix():(p,s) for k,p,s in walk(target) if k=='file'}
    assert expected.keys()==actual.keys()
    stable=0;hashed=0
    for rel,r in expected.items():
        p,s=actual[rel];assert s.st_size==r['bytes']
        if s.st_ino==r['file_id'] and s.st_mtime_ns==r['mtime_ns']:stable+=1
        else:assert digest(p)==r['sha256'],str(p);hashed+=1
    results.append({'source':move['source'],'target':str(target),'files':len(expected),'same_file_id_size_mtime':stable,'changed_identity_rehashed':hashed,'full_sha_snapshot':str(snap),'result':'pass-same-volume-identity-preservation'})
(OUT/('verified-moves-'+batch+'.json')).write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'results':results},ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(results,ensure_ascii=False))
