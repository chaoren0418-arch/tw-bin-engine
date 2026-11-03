import json,gzip,collections
from inventory import ROOT,OUT
a=collections.Counter();b=collections.Counter();r28=collections.Counter()
with gzip.open(OUT/'file-metadata.jsonl.gz','rt',encoding='utf-8') as f:
    for line in f:
        r=json.loads(line)
        if r['root']!=str(ROOT):continue
        parts=r['path'].split('/');a['/'.join(parts[:3])]+=r['bytes'];b['/'.join(parts[:5])]+=r['bytes']
        marker='/reference/development/release-integration-r12/delivery/'
        if marker in r['path']:
            tail=r['path'].split(marker,1)[1];r28[tail.split('/')[0] if '/' in tail else '(root files)']+=r['bytes']
(OUT/'size-breakdown.json').write_text(json.dumps({'depth3':a.most_common(),'depth5':b.most_common()},ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'r28-size-breakdown.json').write_text(json.dumps(r28,ensure_ascii=False,indent=2),encoding='utf-8')
print('size summaries ready')
