"""Run the existing meat/arrow lifecycle checks against this worktree's product DLL.

Only the test host's AppContext base path is redirected to the original engine,
so its fixed ../../assets readers stay read-only; no assets junction is created.
"""
import argparse, hashlib, os, subprocess
from pathlib import Path
from xml.sax.saxutils import escape

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--original', type=Path, required=True)
a = p.parse_args()
repo = Path(__file__).resolve().parents[4]
source = a.original / 'work/engine-ground-lifecycle-r1/checks/Program.cs'
raw = source.read_bytes()
assert hashlib.sha256(raw).hexdigest() == 'f3ac34a5b40a9d50ecc55cca6bc57163b2e21f7d32e3ba591f97a19b631beb84'
scratch = repo / 'scratch/issue-51/legacy'
scratch.mkdir(parents=True, exist_ok=True)
text = raw.decode('utf-8-sig')
text = text.replace('static async Task Main(string[] args){', 'static async Task Main(string[] args){AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.GetFullPath(args[2])+Path.DirectorySeparatorChar);', 1)
(scratch / 'Program.cs').write_text(text, encoding='utf-8')
dll = repo / 'engine/build/client-product-dev/P11.ClientEngine.dll'
(scratch / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="P11.ClientEngine"><HintPath>'+escape(str(dll))+'</HintPath></Reference></ItemGroup></Project>', encoding='utf-8')
env = dict(os.environ)
env.update(DOTNET_CLI_HOME=str(repo/'scratch/issue-51/build/.dotnet'),NUGET_PACKAGES=str(repo/'engine/.nuget'),TEMP=str(repo/'scratch/issue-51/build/tmp'),TMP=str(repo/'scratch/issue-51/build/tmp'))
output = repo / 'engine/build/issue-51-legacy'
subprocess.run(['dotnet','build',str(scratch/'Checks.csproj'),'-c','Release','-o',str(output),'--nologo','-p:NuGetAudit=false','-p:UseSharedCompilation=false'],check=True,env=env)
for mode in ['candidate','extra']:
    evidence=repo/'scratch/issue-51'/('legacy-'+mode)
    evidence.mkdir(parents=True,exist_ok=True)
    subprocess.run(['dotnet',str(output/'Checks.dll'),str(evidence),mode,str(a.original/'engine/build/offline-checks')],check=True,env=env)
