# Issue #47 唯讀重現程式

以下程式保存完整核驗邏輯；只讀明列來源。需 Python 3.12 與既有 capstone，不會自動安裝依賴。
將下方唯一 Python 區塊存為 scratch/issue-47/verify.py，再依報告命令執行。
預設重算後比對 work/issue-47/evidence.json；--emit 僅印出 JSON，若需保留暫存輸出請放 scratch。
禁止改成呼叫歷史 collector 或讀取原 BIN；缺合法既有證據時應失敗並回報。

```python
"""#47 靜態資料重現器；只讀明列來源，不讀 BIN、assets junction 或封存。

預設將重算結果與同目錄 evidence.json 比較；--emit 僅寫 stdout。
不播放音檔、不呼叫任何歷史 collector/verifier。
"""
import argparse
import hashlib
import io
import json
import re
import struct
import wave
from pathlib import Path

TXT_SHA = '28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8'
NATIVE_SHA = 'e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def collect(repo, client, prior):
    pins = []

    def read(root, rel, role):
        p = root / rel
        assert not p.is_symlink(), p
        b = p.read_bytes()
        pins.append(dict(root=role, path=rel, size=len(b), sha256=sha(b)))
        return b

    raw = read(client, 'TW13081901.txt', 'client')
    assert sha(raw) == TXT_SHA, '來源 TXT 版本不符；拒絕重新資格化'
    profiles = json.loads(read(repo, 'work/engine-pickup-asset-source-r1/profile-qualification.json', 'repo'))['profiles']
    melee = sorted([p['gfx'], p['weapon']] for p in profiles if p['weapon'] != 20)
    blocks = {}
    starts = list(re.finditer(rb'(?m)^#(\d+)\s', raw))
    for i, m in enumerate(starts):
        blocks[int(m[1])] = (m.start(), raw[m.start():starts[i+1].start() if i+1 < len(starts) else len(raw)])
    entries, actions, candidates = [], [], []
    requested = {}
    for gfx, weapon in melee:
        requested.setdefault(gfx, []).extend([(weapon+1, weapon, None), (weapon+2, weapon, None)])
    for skill, gfx in [(1, 11722), (3, 221), (4, 167), (8, 2165)]:
        requested[gfx] = [(0, None, skill)]
    requested[219] = [(0, None, None)]
    # < 的對照僅保留既有 gfx0 walk/get，不把 206/478/479 當成聲音 ID。
    requested[0] += [(0, 0, None), (15, 0, None)]
    for gfx, targets in sorted(requested.items()):
        offset, block = blocks[gfx]
        header = block.splitlines()[0].decode('ascii')
        alias = re.search(r'=([0-9]+)', header)
        entry = dict(gfx=gfx, header=header, sprite=int(alias[1]) if alias else gfx,
                     fileOffset=hex(offset), size=len(block), rawSha256=sha(block),
                     normalizedSha256=sha(block.replace(b'\r\r\n', b'\r\n')),
                     linkedEffects=[m[0].decode('ascii') for m in re.finditer(rb'109\.effect\([^)]*\)', block)])
        prior_name = f'work/engine-pickup-asset-source-r1/metadata/entry-{gfx}.txt'
        if (prior / prior_name).is_file():
            old = read(prior, prior_name, 'prior')
            entry['historicalEntrySha256'] = sha(old)
            entry['historicalEntryExactMatch'] = old == block.replace(b'\r\r\n', b'\r\n')
        entries.append(entry)
        for action, weapon, skill in targets:
            matches = list(re.finditer(rb'(?m)^[\t ]*' + str(action).encode() + rb'\.([^\r\n(]+)\(([^\r\n)]*)\)', block))
            assert len(matches) == 1, (gfx, action, len(matches))
            m = matches[0]
            body = m[2]
            head, tokens_raw = body.split(b',', 1)
            directional, count = map(int, head.split())
            tokens = list(re.finditer(rb'\S+', tokens_raw))
            assert count == len(tokens), (gfx, action, count, len(tokens))
            row = dict(gfx=gfx, sprite=entry['sprite'], weapon=weapon, skill=skill,
                       action=action, label=m[1].decode('ascii'), directional=directional,
                       tokenCount=count, lineFileOffset=hex(offset+m.start()), events=[])
            for index, tok in enumerate(tokens):
                frame = re.fullmatch(rb'(-?\d+)\.(-?\d+):(-?\d+)(.*)', tok[0])
                assert frame, (gfx, action, tok[0])
                suffix = frame[4].decode('ascii')
                if not suffix:
                    continue
                token_offset = offset + m.start(2) + body.index(b',') + 1 + tok.start()
                assert raw[token_offset:token_offset+len(tok[0])] == tok[0]
                ev = dict(sequenceIndex=index, frameKey=f'{int(frame[1])}:{int(frame[2])}',
                          ticks=int(frame[3]), rawToken=tok[0].decode('ascii'),
                          rawEvents=suffix, fileOffset=hex(token_offset),
                          bracketCandidates=[int(x) for x in re.findall(r'\[(\d+)', suffix)],
                          angleValues=[int(x) for x in re.findall(r'<(\d+)', suffix)],
                          exclamation='!' in suffix)
                row['events'].append(ev)
                if ev['bracketCandidates']:
                    candidates.append(dict(gfx=gfx, sprite=entry['sprite'], weapon=weapon,
                                           skill=skill, action=action, **ev, sourceSha256=TXT_SHA,
                                           qualified=False, enabled=False, dispatchPolicy=None,
                                           blockers=['文字符號至 native 欄位映射未證明', 'TXT 與 native BIN 版本配對未證明',
                                                     '該動作事件呼叫與生命週期未完整證明']))
            actions.append(row)
    wavs = []
    for sid in sorted({sid for c in candidates for sid in c['bracketCandidates']}):
        rel = f'Sound/{sid}.wav'
        if not (client / rel).is_file():
            wavs.append(dict(id=sid, path=rel, exists=False))
            continue
        b = read(client, rel, 'client')
        assert b[:4] == b'RIFF' and b[8:12] == b'WAVE'
        chunks, pos = [], 12
        while pos + 8 <= len(b):
            size = struct.unpack_from('<I', b, pos+4)[0]
            assert pos+8+size <= len(b)
            chunks.append(dict(tag=b[pos:pos+4].decode('ascii'), headerFileOffset=hex(pos),
                               payloadFileOffset=hex(pos+8), size=size))
            pos += 8 + size + size % 2
        assert struct.unpack_from('<I', b, 4)[0] + 8 == len(b)
        with wave.open(io.BytesIO(b), 'rb') as w:
            fmt = dict(channels=w.getnchannels(), sampleRate=w.getframerate(), bits=w.getsampwidth()*8,
                       frameCount=w.getnframes(), compression=w.getcomptype())
        wavs.append(dict(id=sid, path=rel, exists=True, size=len(b), sha256=sha(b), format=fmt,
                         chunks=chunks, playbackVerified=False))
    audit = json.loads(read(repo, 'work/engine-pickup-scheduler-source-r1/native-byte-audit.json', 'repo'))
    imports = json.loads(read(repo, 'work/engine-pickup-scheduler-source-r1/imports.json', 'repo'))
    rand_import = next(i for i in imports if i['name'] == 'rand')
    assert rand_import == dict(dll='MSVCR110.dll', name='rand', iatVA='0x114b668')
    source_pins = json.loads(read(repo, 'work/engine-pickup-scheduler-source-r1/source-pins.json', 'repo'))
    assert source_pins[0]['sha256'] == NATIVE_SHA
    native = []
    from capstone import Cs, CS_ARCH_X86, CS_MODE_32
    disassembler = Cs(CS_ARCH_X86, CS_MODE_32)
    # 只解碼既有 JSON 中的 bytes，完全不開啟原 BIN。
    ranges = {'0xa3ef40': [(0xa3f9cc, 0xa3fa7e), (0xa3fb91, 0xa3fcbe)],
              '0xa43560': [(0xa4358c, 0xa435a0), (0xa437d8, 0xa43826), (0xa4388f, 0xa438a7)]}
    for va, wanted in ranges.items():
        rel = f'work/engine-slow-pickup-source-r1/native/{va}.json'
        b = read(prior, rel, 'prior')
        declared = next(p for p in source_pins if p['path'].replace('\\', '/').endswith(rel))
        assert sha(b) == declared['sha256'], rel
        obj = json.loads(b)
        chunk = next(c for c in obj['chunks'] if c['va'] == va)
        data = bytes.fromhex(chunk['bytes'])
        pin = next(c for c in audit['chunks'] if c['VA'] == va)
        assert sha(data) == chunk['sha256'] == pin['sha256'] and len(data) == pin['size']
        base = int(va, 16)
        ins = list(disassembler.disasm(data, base))
        assert sum(i.size for i in ins) == len(data)
        if va == '0xa3ef40':
            assert next(i for i in ins if i.address == 0xa3fa37).bytes.hex() == 'ff1568b61401'
        excerpts = []
        for start, end in wanted:
            selected = [i for i in ins if start <= i.address < end]
            assert selected and selected[0].address == start, hex(start)
            end = selected[-1].address + selected[-1].size
            excerpts.append(dict(VA=hex(start), endVAExclusive=hex(end), RVA=hex(start-0x400000),
                                 fileOffset=hex(int(pin['fileOffset'],16)+start-base),
                                 sha256=sha(data[start-base:end-base]),
                                 instructions=[dict(VA=hex(i.address), bytes=i.bytes.hex(),
                                                    instruction=f'{i.mnemonic} {i.op_str}') for i in selected]))
        native.append(dict(artifact=rel, artifactSha256=sha(b), claimedBinSha256=NATIVE_SHA,
                           originalBinReverified=False, chunk={k:pin[k] for k in ['VA','RVA','fileOffset','size','sha256']},
                           excerpts=excerpts,
                           importedNames=[x for x in obj['instructions'] if 'rand' in x['line']]))
    return dict(schema='issue-47-static-evidence-v1', sourceVersionPaired=False,
                status='blocked-native-contract', scopeMeleeProfiles=melee, entries=entries,
                actions=actions, candidates=candidates, qualifiedBindings=[], wavPins=wavs,
                nativeEvidence=native, historicalRandImport=rand_import, sourcePins=pins,
                limitations=['完整存取未驗證；未讀正式 BIN、封存、assets junction、機密設定或 REPORT.md',
                             'native 證據為既有 JSON 的 bytes 自洽與歷史 hash 核對，不是本輪原 BIN 重驗',
                             '未播放、未操作程序、未連 DB／服務；零 runtime checks'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--prior', type=Path, required=True)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--emit', action='store_true')
    args = parser.parse_args()
    data = collect(args.repo, args.client, args.prior)
    if args.emit:
        print(json.dumps(data, ensure_ascii=False, indent=2))
    else:
        expected = json.loads((args.repo / 'work/issue-47/evidence.json').read_text(encoding='utf-8'))
        assert data == expected, '重算結果與已交付證據不同'
        assert not data['qualifiedBindings'] and all(not x['enabled'] for x in data['candidates'])
        print(json.dumps(dict(verified=True, candidates=len(data['candidates']), wavPins=len(data['wavPins']),
                              qualifiedBindings=0, nativeChunks=len(data['nativeEvidence']), runtimeChecks=0)))


if __name__ == '__main__':
    main()
```
