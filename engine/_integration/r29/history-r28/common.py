from pathlib import Path
import os,json,hashlib,shutil
A=Path(__file__).resolve().parent;OUT=A.parent
# The writable engine is a direct child of the canonical project root.
# Historical integration references are explicit; rebuilding no longer assumes
# the old deeply nested release-integration-r12/delivery directory depth.
PROJECT_ROOT=next((p for p in OUT.parents if (p/'project-manifest.json').is_file()),OUT.parent)
D=PROJECT_ROOT/'P24-原2x2地圖快取與跨區載入/reference/development'
BASE=D/'release-integration-r11/delivery';CAND=D/'r28-6016-host-adapter-r3';SUP=PROJECT_ROOT/'主管/P24-本機主管接管與完整整合-20261028'
EXCLUDED={'bin','obj','__pycache__','.runtime','.dotnet','.nuget','.http-cache','.plugin-cache','tmp','temp','appdata'}
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def save(p,v):
 p.parent.mkdir(parents=True,exist_ok=True)
 with p.open('x',encoding='utf-8') as f:json.dump(v,f,ensure_ascii=False,indent=2);f.write('\n')
def copy_tree(src,dst):
 for root,dirs,files in os.walk(src):
  dirs[:]=[n for n in dirs if n not in EXCLUDED]
  for n in dirs+files:assert not (Path(root)/n).is_junction() and not (Path(root)/n).is_symlink()
 shutil.copytree(src,dst,ignore=lambda _,names:[n for n in names if n in EXCLUDED])
def env_for(root):
 env=os.environ.copy()
 for k,v in {'TEMP':'tmp','TMP':'tmp','APPDATA':'appdata/roaming','LOCALAPPDATA':'appdata/local','DOTNET_CLI_HOME':'.dotnet','NUGET_PACKAGES':'.nuget','NUGET_HTTP_CACHE_PATH':'.http-cache','NUGET_PLUGINS_CACHE_PATH':'.plugin-cache','NUGET_SCRATCH':'tmp/nuget'}.items():
  p=root/v;p.mkdir(parents=True,exist_ok=True);env[k]=str(p)
 env.update(DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_CLI_USE_MSBUILD_SERVER='0',MSBUILDDISABLENODEREUSE='1');return env
