from common import *
# One-time evidence recovery only. No build, tests, launcher, or native operation is executed here.
output=A/'evidence/build01';check=output/'checks';accepted=read(A/'source-lineage.json')['accepted']
names=['sdk','product-build','launcher-build','same-cmd-check-only','host-build','host-checks','client-extract','client-build','client-checks','audio-extract','audio-settings-build','audio-settings-checks','web-checks']
commands=[read(output/(n+'.command.json')) for n in names];assert all(r['exitCode']==0 for r in commands)
dll=OUT/'build/client-product-r28/P11.ClientEngine.dll';audio=OUT/'build/client-product-r28/P24.Audio.dll';identity=sha(dll);assert identity==accepted['productDllSha256'] and sha(audio)==accepted['audioDllSha256']
for name,cases,checks in [('host-result.json',18,56),('client-regression-result.json',4,21),('audio-settings-result.json',7,39)]:
 r=read(check/'evidence'/name);assert r['status']=='pass' and r['cases']==cases and r['assertions']==checks and r['productDllSha256']==identity
for name in ['host','client','audio-settings']:assert sha(output/'helpers'/name/'P11.ClientEngine.dll')==identity
web=read(check/'evidence/web-controls-result.json');assert web['checks']==35 and web['sourceSha256']==sha(OUT/'web/panel.js')
for name in ['product-build','launcher-build','host-build','client-build','audio-settings-build']:
 log=(output/(name+'.stdout.log')).read_text('utf-8-sig',errors='replace');assert ('0 個警告' in log or '0 Warning(s)' in log) and ('0 個錯誤' in log or '0 Error(s)' in log)
manifestHash=sha(OUT/'build/product-r28-manifest.json');entry=read(output/'entry-check.json');assert entry['revision']=='r28' and entry['manifestSha256']==manifestHash and not entry['uiStarted'] and entry['assetFilesCheckedForPresenceAndSize']==13957
save(output/'receipt-recovery.json',dict(reason='UTF-8 build output followed by legacy-encoded final PowerShell informational line; strict decoder failed after all checks already succeeded.',action='Preserve raw stdout; decode with replacement only for informational tail; revalidate existing zero warning/error summary and zero command exits.',rebuild=False,retest=False))
save(output/'result.json',dict(status='pass-final-r28',productDllSha256=identity,audioDllSha256=sha(audio),productManifestSha256=manifestHash,launcherDllSha256=sha(OUT/'launcher/P24.Launcher.dll'),assetCount=13957,hostCases=22,hostChecks=77,audioSettingsCases=7,audioSettingsChecks=39,domChecks=35,totalChecks=151,warnings=0,errors=0,productBuilds=1,historicalSuitesRerun=False,GUI=False,COM=False,playback=False,network=False,services=False,database=False,commands=commands))
print('PASS recovered final receipt from successful commands/results; no checks rerun')
