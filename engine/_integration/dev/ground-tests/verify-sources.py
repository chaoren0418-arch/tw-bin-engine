"""Compare the in-code ground SPR pins (GroundCatalog.Pinned) against the #48 report, without DB access.

T464 (#1049): the server-template rows (ground-catalog.json) are gone; only content-free SPR pins
(entry, idx record, PAK File Offset, size, payload/RGBA SHA-256, frame header offset, bounds) remain.
"""
import json, re, subprocess
from pathlib import Path

repo=Path(__file__).resolve().parents[4]
source=(repo/'engine/source/GroundCatalog.cs').read_text('utf-8')
report=(repo/'docs/reports/issue-48.md').read_text('utf-8').splitlines()
pin_re=re.compile(r'new\("s87-gfx-(\d+)",(\d+),new\("([^"]+)",(\d+),(\d+)L,(\d+),"([0-9a-f]{64})",(\d+),(\d+),\[(-?\d+),(-?\d+),(-?\d+),(-?\d+)\],"([0-9a-f]{64})","docs/reports/issue-48\.md"\)\)')
pins=pin_re.findall(source)
assert len(pins)==4,len(pins)
assert not re.search(r'templateId|etcitem|rowSha256|wireUseType|inventoryBlesses',source,re.I),'server template values must not be in code'
for gid,gfx,entry,record,offset,size,payload_sha,frame_no,header,b0,b1,b2,b3,rgba in pins:
    assert gid==gfx and entry==gfx+'-0.spr' and frame_no=='0'
    payload=next((s.split('|') for s in report if s.startswith('| `'+entry+'` |')),None)
    frame=next((s.split('|') for s in report if s.startswith('| `'+entry+'`') and '[' in s),None)
    assert payload and frame,entry
    assert int(record)==int(re.search(r'\d+',payload[2])[0])
    assert int(offset)==int(re.search(r'0x[0-9a-f]+',payload[3])[0],16)
    assert int(size)==int(payload[4]) and payload_sha==payload[5].strip().strip('`')
    assert int(header)==int(re.search(r'0x[0-9a-f]+',frame[2])[0],16)
    assert [int(b0),int(b1),int(b2),int(b3)]==json.loads(frame[3].strip().strip('`'))
    assert rgba==frame[4].strip().strip('`')
fixture=subprocess.check_output(['git','show','ddc2267:work/issue-49/fixtures.json'],cwd=repo)
assert fixture==(repo/'work/issue-51/fixtures-49.json').read_bytes()
print('PASS: 4 in-code SPR pins match #48, no server template fields in code, byte-exact #49 fixtures')
