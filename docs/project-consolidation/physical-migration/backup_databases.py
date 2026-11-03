"""Offline physical recovery copies. Does not claim SQL-level or crash-recovery verification."""
import zipfile,json,datetime,hashlib,gzip
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2]
dest=ROOT/'recovery/database-cold-snapshots';dest.mkdir(parents=True,exist_ok=True)
paths=[Path(r'D:\P10Lab-TW13081901')]+[Path(f'D:\\P{i}Lab-ClientEngine') for i in range(12,25)]
receipts=[]
for p in paths:
    snapshot=OUT/('before-'+p.name+'.jsonl.gz');assert snapshot.exists()
    rows=[json.loads(l) for l in gzip.open(snapshot,'rt',encoding='utf-8')]
    selected=[r for r in rows if r['path'].startswith('db-data/') or r['path'] in ('mysql.ini','private-root.cnf','private-game.cnf')]
    archive=dest/(p.name+'.zip');assert not archive.exists()
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as z:
        for r in selected:z.write(p/r['path'],r['path'])
    with zipfile.ZipFile(archive) as z:
        for r in selected:
            with z.open(r['path']) as f:h=hashlib.file_digest(f,'sha256').hexdigest()
            assert h==r['sha256'],r['path']
    rec={'source':str(p),'archive':str(archive),'files':len(selected),'source_bytes':sum(r['bytes'] for r in selected),'archive_bytes':archive.stat().st_size,'verified_all_archive_entries':True,'snapshot':str(snapshot),'scope':'cold physical backup; credentials remain local; no DB query or logical consistency claim'}
    receipts.append(rec);(OUT/'database-backups.json').write_text(json.dumps(receipts,ensure_ascii=False,indent=2),encoding='utf-8');print(p.name,'cold backup verified',archive.stat().st_size,flush=True)
