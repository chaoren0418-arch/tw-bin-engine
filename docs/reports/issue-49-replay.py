"""重播靜態來源核對；不執行 handler，不代表實際封包或遊戲測試。"""
import argparse
import hashlib
import json
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def decode(data):
    try:
        return data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return data.decode('cp950')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, help='保存既有 work 證據的專案根目錄；僅讀明列檔案')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2] / 'work' / 'issue-49'
    contract = json.loads((root / 'contract.json').read_text(encoding='utf-8'))
    fixtures = json.loads((root / 'fixtures.json').read_text(encoding='utf-8'))
    assert contract['qualified'] is False
    assert contract['adoptedTemplateRows'] == []
    assert fixtures['liveCapturedFixtures'] == []
    assert len(contract['templateGaps']) == 7
    rows = {row['id']: row for row in contract['adoptedProtocolRows']}
    assert len(rows) == len(contract['adoptedProtocolRows'])
    for row in rows.values():
        assert sha(row['text'].encode('utf-8')) == row['excerptSha256'], row['id']
        assert row['source'] in contract['sources']
    # 固定來源分支錨點；不是模擬或執行伺服器。
    anchors = {
        'C112-fields': ['readD:()I', '2147483647', '198: ifgt', '205: return'],
        'C112-nonitem': ['218: instanceof', 'L1ItemInstance', '221: ifeq', '265: return'],
        'C25-equipped-sealed': ['isEquipped:()Z', 'isSealed:()Z', '398: return', '434: return'],
        'C112-source-owner': ['getCount:()J', 'getItemOwnerId:()I', '376: return', '415: return'],
        'S87-header': ['long 2000000000l', 'java/lang/Math.min:(JJ)J'],
        'S120': ['10: bipush        120', 'writeD:(I)V'],
    }
    for case in fixtures['cases']:
        text = rows[case['row']]['text']
        assert all(anchor in text for anchor in anchors[case['row']]), case['id']
    source_files = 0
    if args.source_root:
        source_root = args.source_root.resolve()
        for source in contract['sources'].values():
            for path_key, hash_key in [('path', 'sha256'), ('classPath', 'classSha256'), ('collectionPath', 'collectionSha256')]:
                path = (source_root / source[path_key]).resolve()
                assert path.is_relative_to(source_root), path
                assert sha(path.read_bytes()) == source[hash_key], source[path_key]
                source_files += 1
        for row in rows.values():
            source = contract['sources'][row['source']]
            lines = decode((source_root / source['path']).read_bytes()).splitlines()
            first, last = row['sourceLine']
            assert '\n'.join(lines[first-1:last])+'\n' == row['text'], row['id']
    print(json.dumps({'status': 'pass', 'scope': 'static-evidence-only', 'protocolRows': len(rows),
                      'replayedCases': len(fixtures['cases']), 'sourceHashChecks': source_files,
                      'adoptedTemplateRows': 0, 'liveCapturedFixtures': 0, 'qualified': False}))


if __name__ == '__main__':
    main()
