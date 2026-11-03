"""Reuse captured metadata; read only bounded source/config text, no second tree walk."""
import json,re,gzip,collections
from pathlib import Path
from inventory import ROOT,OUT,sha
from analyze import excluded,DELIVERY
EXT={'.py','.ps1','.cmd','.bat','.cs','.csproj','.ini','.properties','.config','.js','.html'}
pattern=re.compile(r'(?<![A-Za-z0-9])[A-Za-z]:[\\/](?:[^\s\x22\x27<>|;,\)\]\}]+)')
hits=[];scanned=0;errors=[];targets=collections.Counter()
raw=OUT/'file-metadata.jsonl'
opener=lambda:raw.open(encoding='utf-8') if raw.exists() else gzip.open(OUT/'file-metadata.jsonl.gz','rt',encoding='utf-8')
for line in opener():
    r=json.loads(line);rel=Path(r['path']);p=Path(r['root'])/rel
    if excluded(rel) or p.suffix.lower() not in EXT or r['bytes']>2*1024*1024:continue
    if any(x in rel.parts for x in ['assets','vendor','node_modules','build','bin','obj','mysql','db-files','project-consolidation']):continue
    try:txt=p.read_text(encoding='utf-8-sig')
    except (OSError,UnicodeError) as e:errors.append({'file':str(p),'type':type(e).__name__});continue
    scanned+=1
    for n,l in enumerate(txt.splitlines(),1):
        matches=sorted(set(pattern.findall(l)))
        if matches:
            hits.append({'file':str(p),'line':n,'paths':matches})
            for m in matches:
                norm=m.replace('\\\\','/').replace('\\','/');targets['/'.join(norm.split('/')[:2])]+=1
    if scanned%2000==0:print('SCANNED',scanned,flush=True)
(OUT/'path-dependencies.json').write_text(json.dumps({'text_files_scanned':scanned,'scope':'Script/source/config extensions only, UTF-8 <=2MiB; excludes private/volatile/assets/build/vendor/binaries/general JSON and Markdown; paths only, no secret-bearing line bodies; parent-relative references require separate migration review','errors':errors,'target_counts':dict(targets.most_common()),'hits':hits},ensure_ascii=False,indent=2),encoding='utf-8')
pins=[]
for rel in ['_integration/candidate-seal-manifest.json','build/product-r28-manifest.json','build/client-product-r28/P11.ClientEngine.dll','build/client-product-r28/P24.Audio.dll','launcher/P24.Launcher.dll','assets/integrity.json','_integration/full-assets-verification.json']:
    p=DELIVERY/rel;pins.append({'path':str(p),'bytes':p.stat().st_size,'sha256':sha(p)})
(OUT/'r28-pins.json').write_text(json.dumps(pins,ensure_ascii=False,indent=2),encoding='utf-8')
print('DONE',scanned,'files',len(hits),'hits',len(errors),'unreadable',flush=True)
