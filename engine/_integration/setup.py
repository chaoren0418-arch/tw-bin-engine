from common import *
assert not (OUT/'source').exists()
receipt=read(SUP/'host-r3-review/root-review.json');assert receipt['decision']=='accepted-for-formal-r28-integration'
assert sha(CAND/'candidate-manifest.json')==receipt['sealSha256']=='f322c11e45d9b0fd64048c7fb0d464aa7fc76c1fc0c3b334efac6122b0928915'
baseSeal=BASE/'_integration/candidate-seal-manifest.json';assert sha(baseSeal)=='a333cc86191f23bc72590f5b2d6f2722339f8a90e769b64c1a2315d3b8e66f07'
baseRows=read(baseSeal)['files'];basePins={r['path']:r for r in baseRows}
# Verify immutable baseline product input/runtime pins, defer old asset body hashes to one final full asset pass.
for r in baseRows:
 if r['path'].startswith(('source/','web/','vendor/','launcher/','build/')) or r['path'] in ['assets/integrity.json','build-product.ps1','launch-product.ps1','啟動新版客戶端.cmd','新版客戶端使用說明.md','global.json','NuGet.Config']:
  p=BASE/r['path'];assert p.stat().st_size==r['size'] and sha(p)==r['sha256'],r['path']
save(A/'baseline-pins.json',dict(sealPath=str(baseSeal),sealSha256=sha(baseSeal),pins=[r for r in baseRows if not r['path'].startswith('_integration/')]))
expected={k:v['sha256'] for k,v in basePins.items() if k.startswith(('source/','web/','assets/','audio/'))};lineage=[]
for version,sealHash in [('r1','bd61e78f1f4bca51e92597c1ee24020aedf3564193e4262bab466cea90234548'),('r2','51d3e41f226b1b4507605f7b273d687888b496dd03428747275c5d9898ad46ef'),('r3',receipt['sealSha256'])]:
 root=D/('r28-6016-host-adapter-'+version);seal=root/'candidate-manifest.json';assert sha(seal)==sealHash
 rows=read(seal)['files']
 for r in rows:assert (root/r['path']).stat().st_size==r['size'] and sha(root/r['path'])==r['sha256'],r['path']
 delta=read(root/'delta-manifest.json')
 for row in delta:
  assert expected.get(row['path'])==row['beforeSha256'],(version,row['path']);assert sha(root/row['path'])==row['afterSha256'];expected[row['path']]=row['afterSha256']
 lineage.append(dict(candidate=str(root),sealSha256=sealHash,verifiedFiles=len(rows),deltaFiles=len(delta)))
 for name in ['HANDOFF.md','result.json','delta-manifest.json','candidate-manifest.json']:
  target=A/'references'/version/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/name,target)
for folder in ['source','web','audio']:
 actual={p.relative_to(CAND).as_posix():sha(p) for p in (CAND/folder).rglob('*') if p.is_file() and not any(n in EXCLUDED for n in p.relative_to(CAND).parts)}
 assert actual=={k:v for k,v in expected.items() if k.startswith(folder+'/')},folder
 copy_tree(CAND/folder,OUT/folder)
copy_tree(BASE/'assets',OUT/'assets');print('copied full baseline assets',flush=True)
copy_tree(BASE/'vendor',OUT/'vendor')
for p in (CAND/'assets').rglob('*'):
 if p.is_file():target=OUT/'assets'/p.relative_to(CAND/'assets');assert not target.exists();target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target);assert sha(target)==expected[target.relative_to(OUT).as_posix()]
integrity=read(BASE/'assets/integrity.json');oldCount=len(integrity['files']);assert oldCount==13954
for rel in ['audio-manifest.json','sound/1340.wav','sound/3332.wav']:
 p=OUT/'assets'/rel;assert not any(row['path']==rel for row in integrity['files']);integrity['files'].append(dict(path=rel,size=p.stat().st_size,sha256=sha(p)))
(OUT/'assets/integrity.json').write_text(json.dumps(integrity,ensure_ascii=False,separators=(',',':')),'utf-8')
for name in ['global.json','NuGet.Config','啟動新版客戶端.cmd','build-product.ps1','launch-product.ps1','新版客戶端使用說明.md']:shutil.copyfile(BASE/name,OUT/name)
for name in ['build-product.ps1','launch-product.ps1','新版客戶端使用說明.md']:
 p=OUT/name;p.write_text(p.read_text('utf-8-sig').replace('r27','r28'),'utf-8',newline='\n')
p=OUT/'新版客戶端使用說明.md';p.write_text(p.read_text('utf-8')+'\n\n## 聲音設定\n\n玩家「服務 → 聲音」與本機網頁面板共用主音量與靜音。預設音量 50%；設定顯示已套用前須等待客戶端回覆。靜音、暫不可用及缺件事件不會稍後補播；登出或關閉會停止本模組音訊。此版只封裝 6016 的兩個原 WAV，MP3 仍缺件，未宣稱原 Miles 音量或距離等價。\n','utf-8')
copy_tree(BASE/'_integration/launcher-source',A/'launcher-source')
template=A/'check-template';template.mkdir()
for folder in ['tests','client-tests','audio-tests','fixtures']:copy_tree(CAND/folder,template/folder)
for name in ['prepare_client_checks.py','prepare_audio_checks.py','scope-update-r3.json']:shutil.copyfile(CAND/name,template/name)
for p in template.rglob('*.csproj'):
 s=p.read_text('utf-8');assert '../build/host/P11.ClientEngine.dll' in s;p.write_text(s.replace('../build/host/P11.ClientEngine.dll','$(ProductDll)'),'utf-8')
references=[]
for folder,names in [('host-r3-review',['root-review.json','rebuild-receipt.json','independent-seal-verification-final.json','independent-managed-result.json','independent-dom-result-after.json','independent-static-audit.json']),('audio-r3-review',['root-review.json']),('controller-review',['root-review.json'])]:
 for name in names:
  p=SUP/folder/name;target=A/'references'/folder/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target);references.append(dict(source=str(p),copy=target.relative_to(A).as_posix(),sha256=sha(p)))
save(A/'source-lineage.json',dict(status='pass',baselineSealSha256=sha(baseSeal),accepted=receipt,candidates=lineage,cumulativeInputDelta=[dict(path=k,beforeSha256=basePins.get(k,{}).get('sha256'),afterSha256=v) for k,v in expected.items() if v!=basePins.get(k,{}).get('sha256')],acceptanceReferences=references))
save(A/'integrity-update.json',dict(oldPins=oldCount,newPins=len(integrity['files']),unchangedPrefix=True,added=integrity['files'][oldCount:],audioManifestSha256=sha(OUT/'assets/audio-manifest.json'),missingMp3StillAbsent=not any((OUT/'assets/sound'/f'{n}.mp3').exists() for n in [1340,3332])))
print('Prepared r28 full delivery, exact cumulative candidate source/web/audio and 13957 asset pins',flush=True)
