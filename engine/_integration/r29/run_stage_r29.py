if not __debug__ or '_validated_output_root' not in globals():
 raise RuntimeError('Use run_next_stage.py with exact accepted materials; direct runner execution refused')
from common import *
if OUT.resolve()!=_validated_output_root.resolve():raise RuntimeError('validated output root mismatch')
import argparse,subprocess,re,sys
parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);args=parser.parse_args();output=args.output.resolve();assert output.is_relative_to(A) and not output.exists();output.mkdir(parents=True);env=env_for(output);commands=[]
accepted=read(A/'accepted-r29.json');assert accepted['decision']=='accepted-for-formal-r29-integration'
def run(name,cmd,runEnv=env,cwd=OUT):
 p=subprocess.run(cmd,cwd=cwd,env=runEnv,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=600)
 (output/(name+'.stdout.log')).write_bytes(p.stdout);(output/(name+'.stderr.log')).write_bytes(p.stderr)
 row=dict(name=name,command=cmd,cwd=str(cwd),exitCode=p.returncode);commands.append(row);save(output/(name+'.command.json'),row)
 print(name,p.returncode,flush=True)
 if p.returncode:print(p.stdout.decode('utf-8-sig',errors='replace')[-5000:]);print(p.stderr.decode('utf-8-sig',errors='replace')[-1000:]);raise RuntimeError(name)
 return p.stdout.decode('utf-8-sig',errors='replace')
assert run('sdk',['dotnet','--version']).strip()=='10.0.300'
save(output/'product-source-pins.json',[dict(path=p.relative_to(OUT).as_posix(),sha256=sha(p)) for folder in ['source','audio','web'] for p in sorted((OUT/folder).rglob('*')) if p.is_file() and not any(n in EXCLUDED for n in p.relative_to(OUT).parts)])
run('product-build',['pwsh','-NoProfile','-File',str(OUT/'build-product.ps1')])
dll=OUT/'build/client-product-r29/P11.ClientEngine.dll';audio=OUT/'build/client-product-r29/P24.Audio.dll';manifest=OUT/'build/product-r29-manifest.json';identity=sha(dll);manifestHash=sha(manifest)
assert identity==accepted['productDllSha256'];assert sha(audio)==accepted['audioDllSha256']
assetCount=len(read(OUT/'assets/integrity.json')['files']);assert assetCount==13957
p=A/'launcher-source/Program.cs';s=p.read_text('utf-8-sig').replace('r28','r29');s,n=re.subn(r'const string ManifestHash="[0-9a-f]{64}";',f'const string ManifestHash="{manifestHash}";',s);assert n==1;s,n=re.subn(r'Require\(assets==\d+,',f'Require(assets=={assetCount},',s);assert n==1;p.write_text(s,'utf-8',newline='\n')
options=['-c','Release','--configfile',str(OUT/'NuGet.Config'),'--nologo','-p:NuGetAudit=false','-p:UseSharedCompilation=false']
run('launcher-build',['dotnet','build',str(A/'launcher-source/Launcher.csproj'),*options,'-o',str(output/'launcher')])
for name in ['P24.Launcher.exe','P24.Launcher.dll','P24.Launcher.deps.json','P24.Launcher.runtimeconfig.json']:
 p=OUT/'launcher'/name;p.parent.mkdir(exist_ok=True);shutil.copyfile(output/'launcher'/name,p)
save(output/'launcher-pins.json',dict(productManifestSha256=manifestHash,entry='build/client-product-r29/P11.ClientEngine.exe',assetCount=assetCount,sourceFiles=[dict(path=p.relative_to(A).as_posix(),sha256=sha(p)) for p in sorted((A/'launcher-source').glob('*')) if p.is_file()],runtimeFiles=[dict(path=p.relative_to(OUT).as_posix(),sha256=sha(p)) for p in sorted((OUT/'launcher').glob('*')) if p.is_file()]))
restricted=os.environ.copy();windows=Path(next(v for k,v in restricted.items() if k.upper()=='SYSTEMROOT'));restricted['PATH']=str(windows/'System32')+';'+str(windows)
for key in list(restricted):
 if key.upper().startswith('DOTNET_ROOT') or key.upper() in ['DOTNET_HOST_PATH','DOTNET_ADDITIONAL_DEPS','DOTNET_SHARED_STORE']:del restricted[key]
command=f'"{windows / "System32/cmd.exe"}" /d /s /c ""{OUT / "啟動新版客戶端.cmd"}" --check-only"'
entry=json.loads(run('same-cmd-check-only',command,restricted,output));assert entry['revision']=='r29' and entry['manifestSha256']==manifestHash and entry['assetFilesCheckedForPresenceAndSize']==assetCount and not entry['uiStarted'] and not entry['powerShellRequired'] and entry['workingDirectory']==str(OUT)
save(output/'entry-check.json',entry);save(output/'entry-restricted-path.json',dict(PATH=restricted['PATH'],command=command,cwd=str(output),ui=False))
# A fresh evidence-only test workspace; helpers reference the actual final product, never a candidate build.
check=output/'checks';copy_tree(A/'check-template',check);(check/'evidence').mkdir();(check/'source').mkdir();(check/'web').mkdir();(check/'assets').mkdir();(check/'build/host').mkdir(parents=True)
for name in ['ClientEffect6016.cs','ClientAudioSettings.cs']:shutil.copyfile(OUT/'source'/name,check/'source'/name)
shutil.copyfile(OUT/'web/panel.js',check/'web/panel.js');shutil.copyfile(OUT/'assets/audio-manifest.json',check/'assets/audio-manifest.json');shutil.copyfile(audio,check/'build/host/P24.Audio.dll')
for name,folder,project,assembly in [('host','tests','HostChecks.csproj','HostChecks.dll'),('client','client-tests','ClientChecks.csproj','ClientChecks.dll'),('audio-settings','audio-tests','AudioSettingsChecks.csproj','AudioSettingsChecks.dll')]:
 if name=='client':run('client-extract',[sys.executable,'-B','-X','utf8',str(check/'prepare_client_checks.py')])
 if name=='audio-settings':run('audio-extract',[sys.executable,'-B','-X','utf8',str(check/'prepare_audio_checks.py')])
 dest=output/'helpers'/name
 run(name+'-build',['dotnet','build',str(check/folder/project),*options,'-p:ProductDll='+str(dll),'-o',str(dest)])
 assert sha(dest/'P11.ClientEngine.dll')==identity
 extra=[str(OUT/'assets')] if name=='host' else []
 run(name+'-checks',['dotnet',str(dest/assembly),str(check),*extra])
run('web-checks',['node',str(check/'audio-tests/web-checks.cjs'),str(check)])
for name,cases,checks in [('host-result.json',18,56),('client-regression-result.json',4,21),('audio-settings-result.json',7,39)]:
 result=read(check/'evidence'/name);assert result['status']=='pass' and result['cases']==cases and result['assertions']==checks and result['productDllSha256']==identity
web=read(check/'evidence/web-controls-result.json');assert web['checks']==35 and web['sourceSha256']==sha(OUT/'web/panel.js')
for name in ['product-build','launcher-build','host-build','client-build','audio-settings-build']:
 log=(output/(name+'.stdout.log')).read_text('utf-8-sig',errors='replace');assert ('0 個警告' in log or '0 Warning(s)' in log) and ('0 個錯誤' in log or '0 Error(s)' in log)
save(output/'result.json',dict(status='pass-final-r29',productDllSha256=identity,audioDllSha256=sha(audio),productManifestSha256=manifestHash,launcherDllSha256=sha(OUT/'launcher/P24.Launcher.dll'),assetCount=assetCount,hostCases=22,hostChecks=77,audioSettingsCases=7,audioSettingsChecks=39,domChecks=35,totalChecks=151,warnings=0,errors=0,productBuilds=1,historicalSuitesRerun=False,GUI=False,COM=False,playback=False,network=False,services=False,database=False,commands=commands))
print('PASS actual final r29 151 checks, deterministic product/audio, fixed same CMD check-only',flush=True)
