import json,subprocess,datetime
from pathlib import Path
OUT=Path(__file__).parent
moves=[json.loads(l) for l in (OUT/'moves-external.jsonl').read_text(encoding='utf-8-sig').splitlines() if l.strip()]
results=[]
for move in moves:
    if move['state']!='moved':continue
    root=Path(move['target']);ini=root/'mysql.ini'
    if not ini.exists():continue
    name=Path(move['source']).name;log=OUT/(name+'-validate-config-ascii.log')
    canonical=OUT.parents[2];alias=Path(r'D:\BinEngine')
    runtime=alias/root.relative_to(canonical)
    cmd=[str(runtime/'mysql/bin/mysqld.exe'),'--defaults-file='+str(runtime/'mysql.ini'),'--validate-config','--log-error='+str(alias/log.relative_to(canonical))]
    p=subprocess.run(cmd,cwd=root,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=30)
    (OUT/(name+'-validate-ascii.stdout')).write_bytes(p.stdout);(OUT/(name+'-validate-ascii.stderr')).write_bytes(p.stderr)
    r={'root':str(root),'exit_code':p.returncode,'mode':'validate-config; no storage engine or server operational startup','log':str(log),'old_root_absent_from_ini':move['source'].replace('\\','/') not in ini.read_text(encoding='utf-8-sig')}
    results.append(r);(OUT/'environment-config-validation.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'reference':'https://dev.mysql.com/doc/refman/8.0/en/server-configuration-validation.html','results':results},ensure_ascii=False,indent=2),encoding='utf-8');print(name,p.returncode,flush=True)
    if p.returncode:print(p.stderr.decode('utf-8',errors='replace')[-1200:]);raise SystemExit(p.returncode)
