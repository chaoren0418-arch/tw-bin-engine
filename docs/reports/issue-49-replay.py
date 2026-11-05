"""核對來源與靜態分支推導；不執行 handler，也不是封包 bytes 解碼器。"""
import argparse
import hashlib
import json
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    # 避免 python -O 關掉驗收檢查。
    if not condition:
        raise ValueError(message)


def decode(data):
    try:
        return data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return data.decode('cp950')


def evaluate(case):
    """僅評估明列 bytecode 區間；continue 不表示整個 handler 接受。"""
    operation, value = case['operation'], case['input']
    if operation == 'request-count':
        count = value['readD']
        require(type(count) is int and -(2**31) <= count < 2**31, 'readD must be Java int')
        count = max(0, min(count, 2147483647))
        return {'normalizedCount': count, 'result': 'return' if count <= 0 else 'continue'}
    if operation == 'pickup-kind':
        return {'result': 'continue' if value['isL1ItemInstance'] else 'return'}
    if operation == 'pickup-count-owner':
        if value['available'] <= 0:
            return {'result': 'return', 'offset': 353}
        if value['requested'] <= 0 or value['requested'] > value['available']:
            return {'result': 'return', 'offset': 376}
        if value['ownerId'] != 0 and value['ownerId'] != value['actorId']:
            return {'result': 'return', 'offset': 415, 'messageId': 623}
        return {'result': 'continue', 'offset': 416}
    if operation == 'drop-equipped-sealed':
        if value['equipped']:
            return {'result': 'return', 'offset': 398, 'messageId': 125}
        if value['sealed']:
            return {'result': 'return', 'offset': 434, 'messageId': 210}
        return {'result': 'continue', 'offset': 435}
    if operation == 'display-count':
        require(0 < value['count'] < 2**63, 'fixture count must be positive Java long')
        return {'writeD': min(value['count'], 2000000000)}
    if operation == 'remove-fields':
        require(value['kind'] in ('ground', 'inventory'), 'unknown removal kind')
        return {'calls': [['C', 120 if value['kind'] == 'ground' else 57], ['D', value['objectId']]]}
    if operation == 'template-booleans':
        return {'tradable': value['trade'] == 0, 'cantDelete': value['cant_delete'] == 1,
                'stackable': value['stackable'] == 1}
    raise ValueError('unsupported operation: ' + operation)


OPERATION_ROWS = {
    'request-count': ({'C25-fields'}, {'C112-fields'}),
    'pickup-kind': ({'C112-nonitem'},),
    'pickup-count-owner': ({'C112-source-owner'},),
    'drop-equipped-sealed': ({'C25-equipped-sealed'},),
    'display-count': ({'S87-header', 'S15-header', 'S24'},),
    'remove-fields': ({'S120', 'S57'},),
    'template-booleans': ({'etc-trade', 'etc-cant-delete', 'etc-stackable'},),
}


def replay(contract, fixtures, source_root=None):
    sources = {**contract['sources'], **contract.get('mappingSources', {})}
    all_rows = contract['adoptedProtocolRows'] + contract.get('adoptedMappingRows', [])
    rows = {row['id']: row for row in all_rows}
    require(len(rows) == len(all_rows), 'duplicate evidence row')
    for row in rows.values():
        require(sha(row['text'].encode('utf-8')) == row['excerptSha256'], row['id'])
        require(row['source'] in sources, row['id'] + ': unknown source')
        require(len(row['text'].splitlines()) == row['sourceLine'][1] - row['sourceLine'][0] + 1,
                row['id'] + ': line range')
    cases = fixtures['cases']
    require(len({case['id'] for case in cases}) == len(cases), 'duplicate case')
    for case in cases:
        require(case['kind'] == 'static-semantic-vector', 'unsupported fixture kind')
        require(set(case['rows']) in OPERATION_ROWS[case['operation']], case['id'] + ': evidence rows')
        require(case['preconditions'], case['id'] + ': missing scope preconditions')
        for row_id in case['rows']:
            require(case['evidenceSha256'][row_id] == rows[row_id]['excerptSha256'], case['id'] + ': pin')
        actual = evaluate(case)
        require(actual == case['expect'], f"{case['id']}: expected {case['expect']!r}, actual {actual!r}")
    source_checks = 0
    if source_root:
        source_root = source_root.resolve()

        def read_pin(path_text, expected):
            path = (source_root / path_text).resolve()
            require(path.is_relative_to(source_root), 'source path escapes root')
            require(path.stat().st_nlink == 1, 'hard-linked source refused: ' + path_text)
            raw = path.read_bytes()
            require(sha(raw) == expected, 'source SHA mismatch: ' + path_text)
            return raw

        for source in sources.values():
            read_pin(source['path'], source['sha256'])
            source_checks += 1
            if 'classPath' in source:
                read_pin(source['classPath'], source['classSha256'])
                read_pin(source['collectionPath'], source['collectionSha256'])
                source_checks += 2
            else:
                # 新來源僅驗歷史 javap 與索引；沒有重新核對 JAR/class。
                index = json.loads(decode(read_pin(source['indexPath'], source['indexSha256'])))
                pins = json.loads(decode(read_pin(source['jarPinPath'], source['jarPinSha256'])))
                source_checks += 2
                match = [entry for entry in index if entry['class'] == source['className']]
                require(len(match) == 1, 'historical class index')
                require(match[0]['javapSha256'] == source['sha256'], 'historical javap pin')
                require(match[0]['classSha256'] == source['historicalClassSha256'], 'historical class pin')
                require(pins['files'][0]['sha256'] == contract['historicalJarSha256'], 'historical JAR pin')
        for row in rows.values():
            source = sources[row['source']]
            lines = decode((source_root / source['path']).read_bytes()).splitlines()
            first, last = row['sourceLine']
            require('\n'.join(lines[first-1:last]) + '\n' == row['text'], row['id'] + ': source lines')
    # 不再把「必須永遠缺件」當成通過條件；新增資料必須經適用的驗證器。
    blockers = []
    templates = contract['adoptedTemplateRows']
    if contract['templateGaps'] or not templates:
        blockers.append('template-rows-and-getter-conversions')
    if templates:
        blockers.append('adopted-template-field-evidence-validator-required')
    if not fixtures.get('packetFixtures'):
        blockers.append('packet-bytes-and-encoding-source')
    else:
        blockers.append('packet-fixture-decoder-required')
    if fixtures['liveCapturedFixtures']:
        blockers.append('live-capture-provenance-validator-required')
    require(not contract['qualified'] or not blockers, 'qualified=true contradicts unresolved evidence')
    return {'status': 'partial' if blockers else 'qualified', 'evidenceChecks': 'pass',
            'scope': 'static-excerpts-and-semantic-vectors-only',
            'protocolRows': len(contract['adoptedProtocolRows']),
            'mappingRows': len(contract.get('adoptedMappingRows', [])),
            'semanticCases': len(cases), 'packetCasesReplayed': 0, 'sourceHashChecks': source_checks,
            'adoptedTemplateRows': len(templates), 'liveCapturedFixtures': len(fixtures['liveCapturedFixtures']),
            'qualified': contract['qualified'] and not blockers, 'blockers': blockers}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, help='既有 work 證據根目錄；僅讀明列檔案')
    parser.add_argument('--require-qualified', action='store_true', help='整體缺件時退出 2，供驗收閘門使用')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2] / 'work' / 'issue-49'
    contract = json.loads((root / 'contract.json').read_text(encoding='utf-8'))
    fixtures = json.loads((root / 'fixtures.json').read_text(encoding='utf-8'))
    result = replay(contract, fixtures, args.source_root)
    print(json.dumps(result, ensure_ascii=False))
    return 2 if args.require_qualified and not result['qualified'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
