"""Accepted r34 rebuild/verification. Writes only the mutable canonical engine.
Default verifies input pins. --verify checks the existing product; --build rebuilds once.
"""
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,sys,datetime
from pe_resource import verify_resource
A=Path(__file__).resolve().parent;ROOT=A.parents[1]
CANONICAL_ENGINE=Path('D:\\lineage開發\\TW1608262200研究\\engine')
LOCK_SHA='edc31fcb12204ec4fcabd6e5311e340dd13456eb4cc228d5506dab22b7313aec'
CONTRACT_SHA='37ff68bd2ce504a7dc1f8df8b80a9a9b70068c96d159fc5b1e5df75246c2efd1'
RECEIPT_SHA='ed80a7daa26d6b6aa3f00f12fadfb655d947ad33f76f5969821aaaa1a6cfa237'
def require(ok,msg):
 if not ok:raise RuntimeError(msg)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def resolve(rel):
 p=ROOT/rel;require(not Path(rel).is_absolute() and '..' not in Path(rel).parts,'input path escapes root')
 if ROOT.name=='engine' and Path(rel).parts[0]=='assets':require((ROOT/'assets').resolve()==(ROOT.parent/'releases/r28/assets').resolve() and p.resolve().is_relative_to((ROOT/'assets').resolve()),'read-only asset target drift')
 else:require(p.resolve().is_relative_to(ROOT.resolve()),'input path escapes root')
 return p
def pin(p,row):require(p.is_file() and p.stat().st_size==row['size'] and sha(p)==row['sha256'],'pin drift: '+str(p))
def no_shared_write(p):
 for q in [p,*p.parents]:
  require(not q.is_symlink() and not q.is_junction(),'reparse write path: '+str(q))
  if q==ROOT:break
 if p.exists():
  for q in ([p] if p.is_file() else p.rglob('*')):
   require(not q.is_symlink() and not q.is_junction(),'reparse output: '+str(q))
   if q.is_file():require(q.stat().st_nlink==1,'shared output cannot be rewritten: '+str(q))
def validate():
 require(__debug__,'Python optimization is not permitted')
 if ROOT.name=='engine':require(ROOT.resolve()==CANONICAL_ENGINE.resolve(),'canonical engine path mismatch')
 require(sha(A/'rebuild-lock.json')==LOCK_SHA,'rebuild lock changed')
 lock=read(A/'rebuild-lock.json')
 for row in lock['files']:pin(resolve(row['path']),row)
 require(sha(A/'acceptance/r34-input-contract.json')==CONTRACT_SHA and sha(A/'acceptance/r34-integration-acceptance.json')==RECEIPT_SHA,'accepted supervisor receipt changed')
 receipt=read(A/'acceptance/r34-integration-acceptance.json')
 require(receipt['decision']=='accepted-for-formal-r34-integration' and receipt['candidateId']=='ENGINE-PICKUP-PRESENTATION-IMPL-R1' and receipt['contractSha256']==CONTRACT_SHA,'wrong accepted candidate')
 require(sha(A/'acceptance/source-manifest.json')==receipt['sourceManifestSha256'],'accepted source manifest changed')
 contract=read(A/'acceptance/r34-input-contract.json')
 require(sha(A/'acceptance/resource-material.json')==contract['resourceManifest']['sha256'],'original sealed resource material changed')
 require(sha(A/'acceptance/independent-review-seal.json')==contract['independentReviewSeal']['sha256'],'original independent review seal changed')
 normalized=read(A/'acceptance/normalized-resource.json')
 require(normalized['original']['sha256']==contract['resourceManifest']['sha256'] and normalized['proof']['candidateSealSha256']==receipt['candidateSealSha256'] and normalized['proof']['sourceManifestSha256']==receipt['sourceManifestSha256'],'derived resource provenance mismatch')
 accepted=read(A/'acceptance/source-manifest.json')
 for row in accepted['files']:pin(resolve(row['path']),row)
 for key in ['productDllSha256','audioDllSha256']:require(lock[key]==receipt[key],'identity differs from supervisor acceptance')
 return lock
def main():
 parser=argparse.ArgumentParser(description=__doc__);mode=parser.add_mutually_exclusive_group();mode.add_argument('--build',action='store_true');mode.add_argument('--verify',action='store_true');parser.add_argument('--output',type=Path);args=parser.parse_args()
 lock=validate()
 if not args.build and not args.verify:
  print(json.dumps(dict(status='accepted-r34-inputs',sourceFiles=lock['sourceCount'],productDllSha256=lock['productDllSha256'],writes=False)));return
 require(ROOT==CANONICAL_ENGINE and ROOT.resolve()==CANONICAL_ENGINE.resolve() and (ROOT.parent/'project-manifest.json').is_file(),'Run build/verification in canonical mutable engine; sealed releases are not build workspaces')
 output=(args.output or ROOT/'_integration/evidence'/('r34-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f'))).resolve()
 require(output.parent==(ROOT/'_integration/evidence').resolve() and not output.exists(),'fresh direct evidence child required');no_shared_write(output)
 for rel in ['build/client-product-r34','build/product-r34-manifest.json','source/obj','source/bin','audio/obj','audio/bin','_integration/r34/launcher-source/obj','_integration/r34/launcher-source/bin','.dotnet','.nuget','tmp']:
  no_shared_write(ROOT/rel)
 output.mkdir(parents=True);env={k.upper():v for k,v in os.environ.items()}
 for k,v in {'TEMP':'tmp','TMP':'tmp','APPDATA':'appdata/roaming','LOCALAPPDATA':'appdata/local','DOTNET_CLI_HOME':'.dotnet','NUGET_PACKAGES':'.nuget','NUGET_HTTP_CACHE_PATH':'.http-cache','NUGET_PLUGINS_CACHE_PATH':'.plugin-cache','NUGET_SCRATCH':'tmp/nuget'}.items():
  q=output/v;q.mkdir(parents=True,exist_ok=True);env[k]=str(q)
 env.update(DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_CLI_USE_MSBUILD_SERVER='0',MSBUILDDISABLENODEREUSE='1')
 commands=[]
 def run(name,argv,runenv=env):
  p=subprocess.run(argv,cwd=ROOT,env=runenv,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=600)
  (output/(name+'.stdout')).write_bytes(p.stdout);(output/(name+'.stderr')).write_bytes(p.stderr)
  record=dict(name=name,argv=argv,cwd=str(ROOT),exitCode=p.returncode);commands.append(record);(output/(name+'.command.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2),'utf-8')
  require(p.returncode==0,name+' failed; see '+str(output));print(name,p.returncode,flush=True);return p.stdout.decode('utf-8-sig',errors='replace')
 if args.build:
  require(run('sdk',['dotnet','--version']).strip()=='10.0.300','wrong pinned SDK')
  run('product-build',['pwsh','-NoProfile','-File',str(A/'product-build.ps1')])
 dll=ROOT/'build/client-product-r34/P11.ClientEngine.dll';audio=ROOT/'build/client-product-r34/P24.Audio.dll';manifest=ROOT/'build/product-r34-manifest.json'
 require(sha(dll)==lock['productDllSha256'] and sha(audio)==lock['audioDllSha256'],'built DLL identity mismatch')
 resource=verify_resource(dll)
 (output/'embedded-resource.json').write_text(json.dumps(resource,indent=2),'utf-8')
 require(sha(manifest)==lock['productManifestSha256'],'product manifest differs from tested stage')
 for row in read(manifest)['files']:pin(resolve(row['path']),row)
 if args.build:
  run('launcher-build',['dotnet','build',str(A/'launcher-source/Launcher.csproj'),'-c','Release','--configfile',str(ROOT/'NuGet.Config'),'--nologo','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-o',str(output/'launcher')])
  require(sha(output/'launcher/P24.Launcher.dll')==lock['launcherDllSha256'],'launcher identity mismatch')
  (ROOT/'launcher').mkdir(exist_ok=True)
  for name in ['P24.Launcher.exe','P24.Launcher.dll','P24.Launcher.deps.json','P24.Launcher.runtimeconfig.json']:
   dst=ROOT/'launcher'/name;tmp=dst.with_name(name+'.r34-new');require(not tmp.exists(),'temporary output exists')
   # Independent temporary file and replacement never write through an old hardlink.
   require(not dst.parent.is_junction() and not dst.parent.is_symlink(),'launcher path is reparse')
   shutil.copyfile(output/'launcher'/name,tmp);require(tmp.stat().st_nlink==1,'temporary file not independent');os.replace(tmp,dst)
  for name in ['product-build','launcher-build']:
   log=(output/(name+'.stdout')).read_text('utf-8-sig',errors='replace');require(('0 個警告' in log or '0 Warning(s)' in log) and ('0 個錯誤' in log or '0 Error(s)' in log),'nonzero build warnings/errors')
 restricted=env.copy();windows=Path(next(v for k,v in restricted.items() if k.upper()=='SYSTEMROOT'));restricted['PATH']=str(windows/'System32')+';'+str(windows)
 for key in list(restricted):
  if key.upper().startswith('DOTNET_ROOT') or key.upper() in ['DOTNET_HOST_PATH','DOTNET_ADDITIONAL_DEPS','DOTNET_SHARED_STORE']:del restricted[key]
 command=f'"{windows / "System32/cmd.exe"}" /d /s /c ""{ROOT / "啟動新版客戶端.cmd"}" --check-only"'
 entry=json.loads(run('same-entry-check-only',command,restricted));require(entry['revision']=='r34' and entry['manifestSha256']==lock['productManifestSha256'] and not entry['uiStarted'] and not entry['networkStarted'] and not entry['servicesStarted'],'entry check identity or side effect')
 validate()
 result=dict(status='pass-engine-r34-rebuild' if args.build else 'pass-engine-r34-verification',productDllSha256=sha(dll),audioDllSha256=sha(audio),productManifestSha256=sha(manifest),launcherDllSha256=sha(ROOT/'launcher/P24.Launcher.dll'),sourceFiles=lock['sourceCount'],fullSuiteRerun=False,GUI=False,network=False,database=False,services=False,entry=entry,commands=commands)
 (output/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8');print('PASS accepted r34 identity and check-only',flush=True)
if __name__=='__main__':
 try:main()
 except Exception as ex:print('REFUSED: '+str(ex),file=sys.stderr);sys.exit(2)
