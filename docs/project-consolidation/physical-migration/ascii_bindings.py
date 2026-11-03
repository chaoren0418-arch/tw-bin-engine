import json,hashlib,datetime
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2];ALIAS=Path(r'D:\BinEngine')
assert ALIAS.is_junction() and ALIAS.resolve()==ROOT.resolve()
record=json.loads((OUT/'config-relocations.json').read_text(encoding='utf-8'))
for r in record['changes']:
    p=Path(r['file']);raw=p.read_bytes();assert hashlib.sha256(raw).hexdigest()==r['after_sha256']
    text=raw.decode('utf-8');updated=text.replace(ROOT.as_posix(),ALIAS.as_posix()).replace(str(ROOT),str(ALIAS))
    before=ROOT/'recovery/config-unicode-attempt'/p.relative_to(ROOT);assert not before.exists();before.parent.mkdir(parents=True,exist_ok=True);before.write_bytes(raw)
    p.write_bytes(updated.encode('utf-8'));r['unicode_attempt_backup']=str(before);r['unicode_attempt_sha256']=r['after_sha256'];r['after_sha256']=hashlib.sha256(p.read_bytes()).hexdigest();r['runtime_alias_root']=str(ALIAS);r['change']='exact original root -> one canonical project ASCII alias; original config preserved; no credentials/ports/permission verbs changed'
    (OUT/'config-relocations.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
record['ascii_alias']={'path':str(ALIAS),'target':str(ROOT),'reason':'MySQL8.0.40 rejects Chinese option path with OS errno42; only one compatibility root, no second data tree'}
(OUT/'config-relocations.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print('28 config bindings switched to the single ASCII compatibility alias')
