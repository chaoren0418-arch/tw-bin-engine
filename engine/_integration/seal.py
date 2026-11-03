from common import *
import sys
target=A/'candidate-seal-manifest.json'
def inventory():
 found=[]
 for current,dirs,files in os.walk(OUT):
  dirs[:]=[d for d in dirs if d not in EXCLUDED and not (Path(current)==OUT and d in ['runs','runtime'])]
  for name in files:
   p=Path(current)/name
   if p!=target:found.append(p)
 return sorted(found)
if '--verify' in sys.argv:
 rows=read(target)['files'];assert {r['path'] for r in rows}=={p.relative_to(OUT).as_posix() for p in inventory()}
 full='--full' in sys.argv;hashed=sizeOnly=0
 for r in rows:
  p=OUT/r['path'];assert p.stat().st_size==r['size'],r['path'];body=r['path'].startswith('assets/') and r['path']!='assets/integrity.json'
  if full or not body:assert sha(p)==r['sha256'],r['path'];hashed+=1
  else:sizeOnly+=1
 print(json.dumps(dict(status='pass-full-sha256' if full else 'pass-metadata-sha256-and-body-size',files=len(rows),hashedFiles=hashed,bodySizeOnlyFiles=sizeOnly,fullBodyContentVerified=full,sealSha256=sha(target))));raise SystemExit()
if '--append-goal' in sys.argv:
 m=read(target);p=A/'goal-completed-receipt.json';assert read(p)['goal']['status']=='complete';rel=p.relative_to(OUT).as_posix();assert not any(r['path']==rel for r in m['files']);m['files'].append(dict(path=rel,size=p.stat().st_size,sha256=sha(p)));m['files'].sort(key=lambda r:r['path']);target.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n','utf-8')
else:
 assert read(A/'full-assets-verification.json')['status']=='pass';pins={r['path']:r for r in read(OUT/'assets/integrity.json')['files']};rows=[]
 for p in inventory():
  rel=p.relative_to(OUT).as_posix();size=p.stat().st_size
  if rel.startswith('assets/') and rel!='assets/integrity.json':pin=pins[rel[len('assets/'):]];assert size==pin['size'];checksum=pin['sha256']
  else:checksum=sha(p)
  rows.append(dict(path=rel,size=size,sha256=checksum))
 m=dict(schema='p24-r28-complete-delivery-seal-v1',revision='r28',pathsRelativeTo='delivery',excludedDirectories=sorted(EXCLUDED),excludedMutableRoots=['runs','runtime'],excludedFiles=['_integration/candidate-seal-manifest.json'],fullBodyHashEvidence='_integration/full-assets-verification.json',productManifestSha256=sha(OUT/'build/product-r28-manifest.json'),scope='Complete r28 portable delivery and evidence; native acceptance remains supervisor work.',files=rows);save(target,m)
print(json.dumps(dict(files=len(m['files']),sealSha256=sha(target))))
