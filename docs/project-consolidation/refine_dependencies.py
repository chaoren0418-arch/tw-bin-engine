"""Drop URI-suffix false matches by re-reading only prior matching lines."""
import json,re,collections
from pathlib import Path
from inventory import OUT
p=OUT/'path-dependencies.json';d=json.loads(p.read_text(encoding='utf-8'))
pattern=re.compile(r'(?<![A-Za-z0-9])[A-Za-z]:[\\/](?:[^\s\x22\x27<>|;,\)\]\}]+)')
byfile=collections.defaultdict(set)
for h in d['hits']:byfile[h['file']].add(h['line'])
hits=[]
for f,nums in byfile.items():
    lines=Path(f).read_text(encoding='utf-8-sig').splitlines()
    for n in sorted(nums):
        matches=sorted(set(pattern.findall(lines[n-1])))
        if matches:hits.append({'file':f,'line':n,'paths':matches})
counts=collections.Counter()
for h in hits:
    for m in h['paths']:
        norm=m.replace('\\\\','/').replace('\\','/');counts['/'.join(norm.split('/')[:2])]+=1
d['hits']=hits;d['target_counts']=dict(counts.most_common());d['scope']+='; URI-suffix matches filtered using preceding-character guard; candidates may be path fragments when spaces or string concatenation occur; use file+line for complete interpretation'
p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
print('Refined',len(hits),'hits',len(byfile),'files')
