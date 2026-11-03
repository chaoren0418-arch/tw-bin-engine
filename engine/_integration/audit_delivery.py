from common import *
import difflib
base=read(A/'baseline-pins.json');assert sha(Path(base['sealPath']))==base['sealSha256']
old={r['path']:r for r in base['pins']};changed=[];patch=[];trees=[]
for folder in ['source','web','audio','vendor','launcher','assets']:
 before={k:v for k,v in old.items() if k.startswith(folder+'/')}
 after={p.relative_to(OUT).as_posix():p for p in (OUT/folder).rglob('*') if p.is_file() and not any(n in EXCLUDED for n in p.relative_to(OUT).parts)}
 assert set(before)<=set(after),folder
 assetPins={('assets/'+r['path']):r['sha256'] for r in read(OUT/'assets/integrity.json')['files']} if folder=='assets' else {}
 unchanged=0
 for rel,p in after.items():
  checksum=assetPins.get(rel) or sha(p);prior=before.get(rel)
  if prior and checksum==prior['sha256']:unchanged+=1;continue
  changed.append(dict(path=rel,beforeSha256=prior['sha256'] if prior else None,afterSha256=checksum,size=p.stat().st_size))
 trees.append(dict(tree=folder,before=len(before),after=len(after),unchanged=unchanged))
for name in ['global.json','NuGet.Config','啟動新版客戶端.cmd','build-product.ps1','launch-product.ps1','新版客戶端使用說明.md']:
 p=OUT/name;assert sha(BASE/name)==old[name]['sha256']
 if sha(p)!=old[name]['sha256']:changed.append(dict(path=name,beforeSha256=old[name]['sha256'],afterSha256=sha(p),size=p.stat().st_size))
for name in ['global.json','NuGet.Config','啟動新版客戶端.cmd']:assert sha(OUT/name)==sha(BASE/name)
for name in ['build-product.ps1','launch-product.ps1']:assert (OUT/name).read_text('utf-8-sig')==(BASE/name).read_text('utf-8-sig').replace('r27','r28')
expected={r['path']:r['afterSha256'] for r in read(A/'source-lineage.json')['cumulativeInputDelta']}
for row in changed:
 rel=row['path'];assert rel in expected or rel in ['assets/integrity.json','build-product.ps1','launch-product.ps1','新版客戶端使用說明.md','launcher/P24.Launcher.dll'],rel
 if rel in expected:assert row['afterSha256']==expected[rel]
 target=A/'delta'/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(OUT/rel,target)
 if (OUT/rel).suffix in ['.cs','.csproj','.ps1','.md','.html','.js','.css']:
  original=(BASE/rel).read_text('utf-8-sig').splitlines(True) if (BASE/rel).exists() else []
  patch.extend(difflib.unified_diff(original,(OUT/rel).read_text('utf-8-sig').splitlines(True),fromfile='a/'+rel if original else '/dev/null',tofile='b/'+rel))
assert expected.keys()<=set(r['path'] for r in changed)
assert not (OUT/'runtime').exists() and not (OUT/'runs').exists()
for current,dirs,files in os.walk(OUT):
 dirs[:]=[n for n in dirs if n not in EXCLUDED]
 for name in dirs+files:assert not (Path(current)/name).is_junction() and not (Path(current)/name).is_symlink()
save(A/'changed-files.json',sorted(changed,key=lambda r:r['path']));(A/'changes.diff').write_text(''.join(patch),'utf-8')
save(A/'preservation.json',dict(status='pass',exactInputDeltaFiles=len(changed),trees=trees,cumulativeCandidateDeltaPreserved=True,sameCmdBytesAsR27=True,baselineSealPreserved=True,baselineRuntimeRunsNotCopied=True,noLinks=True,originalAssetPinsUnchanged=True,historicalSuitesRerun=False))
print(json.dumps(dict(status='pass',deltaFiles=len(changed),trees=trees)))
