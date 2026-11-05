"""Compare catalog against the pinned #49 Git blob and #48 report, without DB access."""
import hashlib, json, re, subprocess
from pathlib import Path

repo=Path(__file__).resolve().parents[4]
raw=subprocess.check_output(['git','show','ddc2267:work/issue-49/contract.json'],cwd=repo)
contract=json.loads(raw)
catalog=json.loads((repo/'engine/source/Resources/ground-catalog.json').read_text('utf-8'))
report=(repo/'docs/reports/issue-48.md').read_text('utf-8').splitlines()
assert contract['qualified'] is True
assert len(catalog['rows'])==len(contract['adoptedTemplateRows'])==7
for row in catalog['rows']:
    source=next(r for r in contract['adoptedTemplateRows'] if r['id']==row['id'])
    for key,value in source['fields'].items():assert row[key]==value,(row['id'],key)
    assert row['source']['contractSha256']==hashlib.sha256(raw).hexdigest()
    assert row['source']['rowSha256']==source['source']['rowSha256']
    assert row['source']['location']==source['source']['location']
    name=str(row['groundGfx'])+'-0.spr'
    payload=next((s.split('|') for s in report if s.startswith('| `'+name+'` |')),None)
    frame=next((s.split('|') for s in report if s.startswith('| `'+name+'`') and '[' in s),None)
    if row['sprite'] is None:
        assert payload is None and row['disabledReason']
        continue
    pin=row['sprite'];assert payload and frame and pin['entry']==name and pin['frame']==0
    assert pin['indexRecord']==int(re.search(r'\d+',payload[2])[0])
    assert pin['offset']==int(re.search(r'0x[0-9a-f]+',payload[3])[0],16)
    assert pin['size']==int(payload[4]) and pin['payloadSha256']==payload[5].strip().strip('`')
    assert pin['headerOffset']==int(re.search(r'0x[0-9a-f]+',frame[2])[0],16)
    assert pin['bounds']==json.loads(frame[3].strip().strip('`'))
    assert pin['rgbaSha256']==frame[4].strip().strip('`')
fixture=subprocess.check_output(['git','show','ddc2267:work/issue-49/fixtures.json'],cwd=repo)
assert fixture==(repo/'work/issue-51/fixtures-49.json').read_bytes()
print('PASS: 7 template rows, 4 sprite pins, 3 disabled rows, byte-exact #49 fixtures')
