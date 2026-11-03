"""Exact path relocation in runtime configs only; preserve original bytes for rollback."""
import json,datetime,hashlib
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2]
changes=[]
rows=[json.loads(l) for l in (OUT/'moves-external.jsonl').read_text(encoding='utf-8-sig').splitlines() if l.strip()]
for row in rows:
    if row['state']!='moved':continue
    old=Path(row['source']);new=Path(row['target'])
    if not (new/'mysql.ini').exists():continue
    files=[new/'mysql.ini',new/'server/server.policy']+list((new/'server/config').rglob('*'))
    for p in files:
        if not p.is_file():continue
        raw=p.read_bytes()
        try:s=raw.decode('utf-8-sig');encoding='utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'
        except UnicodeDecodeError:continue
        t=s.replace(old.as_posix(),new.as_posix()).replace(str(old),str(new))
        if t==s:continue
        rel=p.relative_to(new);backup=ROOT/'recovery/config-before-relocation'/old.name/rel
        backup.parent.mkdir(parents=True,exist_ok=True)
        if backup.exists():assert backup.read_bytes()==raw,'Existing backup differs; reconcile before retry'
        else:backup.write_bytes(raw)
        data=t.encode(encoding);p.write_bytes(data)
        changes.append({'file':str(p),'backup':str(backup),'old_root':str(old),'new_root':str(new),'before_sha256':hashlib.sha256(raw).hexdigest(),'after_sha256':hashlib.sha256(data).hexdigest(),'change':'exact root path substitution only; no credentials, ports or permission verbs changed'})
        (OUT/'config-relocations.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'changes':changes},ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'config-relocations.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'changes':changes},ensure_ascii=False,indent=2),encoding='utf-8')
print('Relocated paths in',len(changes),'runtime configuration files; original bytes retained locally')
