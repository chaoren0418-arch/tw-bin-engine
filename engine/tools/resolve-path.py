"""Resolve historical evidence paths through the project manifest; never edit evidence."""
import sys,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
manifest=json.loads((root/'project-manifest.json').read_text(encoding='utf-8-sig'))
value=sys.argv[1].replace('\\','/').rstrip('/')
aliases=manifest.get('path_aliases',[])
for row in sorted(aliases,key=lambda r:len(r['old']),reverse=True):
    old=row['old'].replace('\\','/').rstrip('/')
    if value.casefold()==old.casefold() or value.casefold().startswith(old.casefold()+'/'):
        value=row['new'].replace('\\','/').rstrip('/')+value[len(old):];break
path=Path(value)
if not path.exists():raise SystemExit('Mapped path does not exist: '+str(path))
print(path)
