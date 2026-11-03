"""Create the sole writable source tree from the accepted immutable r28 input set."""
import shutil,json,hashlib,datetime
from pathlib import Path
OUT=Path(__file__).parent;ROOT=OUT.parents[2];release=ROOT/'releases/r28';engine=ROOT/'engine'
assert release.is_dir() and not engine.exists()
exclude={'bin','obj','__pycache__','.dotnet','.nuget','tmp','runtime','runs'}
def ignore(folder,names):return [n for n in names if n in exclude]
engine.mkdir()
for name in ['source','audio','web','vendor','launcher','_integration']:
    shutil.copytree(release/name,engine/name,ignore=ignore)
for name in ['global.json','NuGet.Config','build-product.ps1','launch-product.ps1','啟動新版客戶端.cmd','新版客戶端使用說明.md']:shutil.copyfile(release/name,engine/name)
rows=[]
for name in ['source','audio','web','vendor']:
    for p in (engine/name).rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(engine)
        with p.open('rb') as f:a=hashlib.file_digest(f,'sha256').hexdigest()
        with (release/rel).open('rb') as f:b=hashlib.file_digest(f,'sha256').hexdigest()
        assert a==b;rows.append({'path':rel.as_posix(),'sha256':a,'bytes':p.stat().st_size})
(OUT/'engine-source-origin.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':str(release),'target':str(engine),'files':rows,'assets':'read-only baseline directory junction to releases/r28/assets; future changes require versioned asset promotion','new_gameplay_changes':False},ensure_ascii=False,indent=2),encoding='utf-8')
print('engine copied',len(rows),'source/vendor files; assets junction must be created before rebuild')
