"""Run local resource checks without starting the game, browser, audio or network.
Missing local assets is a reported skip (exit 0 for CI orchestration, never PASS).
Each managed suite itself uses exit 77 when its local fixture is unavailable.
"""
from pathlib import Path
import argparse
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
SUITES = [('host', 'tests', 'HostChecks'), ('client', 'client-tests', 'ClientChecks'), ('audio-settings', 'audio-tests', 'AudioSettingsChecks')]

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--assets-root', type=Path, required=True)
    ap.add_argument('--configfile', type=Path, default=ROOT / 'engine/NuGet.Config')
    args = ap.parse_args()
    assets = args.assets_root.absolute()
    if not (assets / 'maps/effects/6016.json').is_file() or not (assets / 'inventory').is_dir():
        for name in ['icons', *[x[0] for x in SUITES]]:
            print(json.dumps(dict(suite=name, status='skipped', reason='Local assets unavailable', testsExecuted=False)))
        return 0
    if os.name != 'nt':
        print(json.dumps(dict(status='blocked', reason='6016 product suites require Windows', testsExecuted=False)))
        return 2
    output = ROOT / 'scratch/issue-109/local-checks'
    output.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ)
    for key, part in {'TEMP':'tmp','TMP':'tmp','DOTNET_CLI_HOME':'dotnet','NUGET_PACKAGES':'packages','NUGET_HTTP_CACHE_PATH':'http-cache','NUGET_SCRATCH':'nuget-scratch'}.items():
        p=output/part;p.mkdir(exist_ok=True);env[key]=str(p)
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false', DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1')
    def run(name, argv, expected=0):
        result = subprocess.run(list(map(str,argv)), cwd=ROOT/'engine', env=env, capture_output=True)
        (output/(name+'.stdout')).write_bytes(result.stdout)
        (output/(name+'.stderr')).write_bytes(result.stderr)
        print(name, result.returncode, result.stdout.decode('utf-8-sig',errors='replace').strip(),flush=True)
        if result.returncode != expected:
            print(result.stderr.decode('utf-8-sig',errors='replace'));raise RuntimeError(name)
    def build(name, project, extra=()):
        run(name+'-restore',['dotnet','restore',project,'--configfile',args.configfile.absolute(),*extra])
        run(name+'-build',['dotnet','build',project,'-c','Release','--no-restore','-p:UseSharedCompilation=false',*extra])
    probe=output/'icon-probes'
    (probe/'directory-link').mkdir(parents=True,exist_ok=True)
    (probe/'file-link/inventory').mkdir(parents=True,exist_ok=True)
    link=probe/'directory-link/inventory'
    if not link.exists():
        # Paths are arguments, not interpolated shell code; only create a scratch junction.
        run('junction',['powershell','-NoProfile','-Command',"& { param($linkPath,$targetPath) New-Item -ItemType Junction -Path $linkPath -Target $targetPath | Out-Null }",str(link),str(assets/'inventory')])
    filelink=probe/'file-link/inventory/2.png'
    if not filelink.exists():
        run('leaf-junction',['powershell','-NoProfile','-Command',"& { param($linkPath,$targetPath) New-Item -ItemType Junction -Path $linkPath -Target $targetPath | Out-Null }",str(filelink),str(assets/'inventory')])
    icon=ROOT/'engine/_integration/dev/local-resource-tests'
    build('icons',icon/'LocalResourceChecks.csproj')
    dll=icon/'bin/Release/net10.0/LocalResourceChecks.dll'
    run('icons',['dotnet',dll,assets,icon/'icon-pins.json',probe])
    run('icons-missing',['dotnet',dll,output/'absent-assets',icon/'icon-pins.json',probe],77)
    template=ROOT/'engine/_integration/check-template'
    product=ROOT/'engine/build/client-product-dev/P11.ClientEngine.dll'
    for name,folder,assembly in SUITES:
        build(name,template/folder/(assembly+'.csproj'),['-p:ProductDll='+str(product)])
        dll=template/folder/'bin/Release/net10.0-windows'/(assembly+'.dll')
        extra=['--product-receipt',ROOT/'engine/_integration/dev/receipt.json'] if name=='host' else []
        run(name,['dotnet',dll,output,'--assets-root',assets,*extra])
        run(name+'-missing',['dotnet',dll,output,'--assets-root',output/'absent-assets'],77)
    print(json.dumps(dict(status='pass',suites=4,missingAssetCases=4,GUI=False,network=False,playback=False)))
    return 0

if __name__=='__main__':
    try: sys.exit(main())
    except (OSError,RuntimeError) as ex:
        print(json.dumps(dict(status='blocked',reason=str(ex))),file=sys.stderr);sys.exit(2)
