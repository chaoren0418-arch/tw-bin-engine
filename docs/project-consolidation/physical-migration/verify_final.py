"""Final content verification; never accept Windows DirEntry file_id=0 as identity."""
import json,gzip,hashlib,datetime,os,subprocess,zipfile,shutil,difflib
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2]
cache={};bytes_read=0
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    global bytes_read
    s=p.stat();key=(s.st_dev,s.st_ino,s.st_size,s.st_mtime_ns)
    if not s.st_ino:key=str(p)
    if key not in cache:
        with p.open('rb') as f:cache[key]=hashlib.file_digest(f,'sha256').hexdigest()
        bytes_read+=s.st_size
    return cache[key]
def save(name,obj):(OUT/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
changes={r['file']:r for r in read(OUT/'config-relocations.json')['changes']}
all_moves=[]
for batch in ['r28','external']:
    all_moves += [r for r in (json.loads(l) for l in (OUT/('moves-'+batch+'.jsonl')).read_text(encoding='utf-8-sig').splitlines() if l.strip()) if r['state']=='moved']
archives=read(OUT/'runtime-archives.json')
mapping={Path(r['source']).relative_to(ROOT/'releases/r28').as_posix():Path(r['target']) for r in archives}
results=[]
for move in all_moves:
    target=Path(move['target']);count=hashed=metadata_only=0
    with gzip.open(OUT/(move['snapshot']+'.jsonl.gz'),'rt',encoding='utf-8') as f:
        for line in f:
            r=json.loads(line);rel=Path(r['path']);p=target/rel
            if move['snapshot']=='before-r28' and rel.parts[0] in mapping:p=mapping[rel.parts[0]]/Path(*rel.parts[1:])
            assert p.is_file(),str(p)
            if str(p) in changes:
                c=changes[str(p)];assert sha(Path(c['backup']))==r['sha256'];assert sha(p)==c['after_sha256'];hashed+=1
            else:
                assert p.stat().st_size==r['bytes'],str(p)
                if r['sha256'] is not None:assert sha(p)==r['sha256'],str(p);hashed+=1
                else:assert p.stat().st_mtime_ns==r['mtime_ns'],str(p);metadata_only+=1
            count+=1
    results.append({'source':move['source'],'target':str(target),'files_accounted':count,'content_sha_verified':hashed,'volatile_evidence_size_mtime_only':metadata_only,'config_changes_verified_against_original_backup':sum(str(target) in k for k in changes),'result':'pass'})
    save('preservation-progress.json',results);print('PRESERVED',move['snapshot'],count,hashed,metadata_only,flush=True)
release=ROOT/'releases/r28';seal=read(release/'_integration/candidate-seal-manifest.json')
for r in seal['files']:assert sha(release/r['path'])==r['sha256'],r['path']
assert sha(release/'_integration/candidate-seal-manifest.json')=='83770091f8051466d5dccb420210cd0567ffef5a1158e8f7ebf5c7667127bfcd'
print('FULL R28 SEAL',len(seal['files']),flush=True)
plan=read(OUT/'dedupe-plan.json');linked=0
for e in plan['entries']:
    p=Path(e['source']);c=Path(e['canonical']);q=Path(e['quarantine']);ps=p.stat();cs=c.stat();qs=q.stat()
    assert ps.st_ino and ps.st_ino==cs.st_ino and ps.st_dev==cs.st_dev
    assert qs.st_ino==e['before_file_id'] and qs.st_mtime_ns==e['before_mtime_ns'] and qs.st_size==e['bytes']
    assert sha(c)==e['sha256'];linked+=1
print('HARDLINK+QUARANTINE',linked,flush=True)
origin=read(OUT/'engine-source-origin.json')
for r in origin['files']:assert sha(ROOT/'engine'/r['path'])==r['sha256']
build=read(ROOT/'engine/_integration/evidence/migration-rebuild-20261028/result.json');assert build['status']=='pass-final-r28'
assert sha(ROOT/'engine/build/client-product-r28/P11.ClientEngine.dll')==sha(release/'build/client-product-r28/P11.ClientEngine.dll')
assert sha(ROOT/'engine/build/client-product-r28/P24.Audio.dll')==sha(release/'build/client-product-r28/P24.Audio.dll')
entry=subprocess.run([os.environ['COMSPEC'],'/d','/c',str(ROOT/'啟動引擎.cmd'),'--check-only'],cwd=OUT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=60)
(OUT/'root-entry.stdout').write_bytes(entry.stdout);(OUT/'root-entry.stderr').write_bytes(entry.stderr);assert entry.returncode==0
entry_result=json.loads(entry.stdout.decode('utf-8-sig'));assert entry_result['revision']=='r28' and not entry_result['uiStarted'];save('root-entry-check.json',entry_result)
old_roots=[r['source'] for r in all_moves if r['snapshot']!='before-r28'];assert len(old_roots)==16 and all(not os.path.lexists(r) for r in old_roots)
assert Path(r'D:\BinEngine').is_junction() and Path(r'D:\BinEngine').resolve()==ROOT.resolve()
assert (ROOT/'engine/assets').is_junction() and (ROOT/'engine/assets').resolve()==(release/'assets').resolve()
assert Path(all_moves[0]['source']).is_junction() and Path(all_moves[0]['source']).resolve()==release.resolve()
# Actual restore of sampled cold DB files; never mount or query this restored data.
archive=ROOT/'recovery/database-cold-snapshots/P24Lab-ClientEngine.zip';probe=ROOT/'recovery/restore-probe';probe.mkdir(exist_ok=True)
original={r['path']:r for r in (json.loads(l) for l in gzip.open(OUT/'before-P24Lab-ClientEngine.jsonl.gz','rt',encoding='utf-8'))}
restored=[]
with zipfile.ZipFile(archive) as z:
    ibd=min((i for i in z.infolist() if i.filename.endswith('.ibd')),key=lambda x:x.file_size).filename
    for name in ['mysql.ini','db-data/auto.cnf',ibd]:
        dest=probe/Path(name);dest.parent.mkdir(parents=True,exist_ok=True)
        with z.open(name) as src,dest.open('wb') as out:shutil.copyfileobj(src,out)
        assert sha(dest)==original[name]['sha256'];restored.append({'entry':name,'output':str(dest),'sha256':sha(dest)})
save('cold-backup-restore-probe.json',{'scope':'file extraction and exact SHA recovery, not logical DB startup/restore','files':restored})
# Record the only rebuild helper fix, keeping the release immutable.
old=(release/'_integration/common.py').read_text(encoding='utf-8-sig');new=(ROOT/'engine/_integration/common.py').read_text(encoding='utf-8-sig')
(OUT/'engine-helper-fix.diff').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='r28/_integration/common.py',tofile='engine/_integration/common.py')),encoding='utf-8')
cfg=read(OUT/'environment-config-validation.json');assert len(cfg['results'])==14 and all(r['exit_code']==0 and r['old_root_absent_from_ini'] for r in cfg['results'])
final={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'pass-content-preservation-build-entry-and-recovery','preservation':results,'formal_r28_full_sha_files':len(seal['files']),'formal_seal_sha256':sha(release/'_integration/candidate-seal-manifest.json'),'hardlink_substitutions':linked,'quarantine_logical_bytes':sum(e['bytes'] for e in plan['entries']),'permanent_deletions':0,'quarantine_bytes_reclaimed':0,'engine_source_vendor_files_verified':len(origin['files']),'engine_product_audio_identical_to_r28':True,'root_entry_check_only':True,'ui_started':False,'services_started':False,'database_config_checks':14,'database_backup_files_restored':len(restored),'original_16_roots_absent':True,'compatibility_junctions':[r'D:\BinEngine',str(ROOT/'engine/assets'),all_moves[0]['source']],'file_id_correction':'Initial DirEntry IDs were zero, not valid identity. All nonvolatile original contents are now verified by SHA; volatile r28 logs/cache retain path mapping, size/mtime and same-volume move receipts, not a full-body identity claim.','hash_distinct_files':len(cache),'hash_distinct_bytes':bytes_read,'disk_free_after_bytes':shutil.disk_usage(ROOT).free,'development_resumed':False}
save('final-verification.json',final);print(json.dumps({k:v for k,v in final.items() if k!='preservation'},ensure_ascii=False),flush=True)
