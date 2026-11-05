"""離線核對模板來源、JAR 映射與靜態 payload；不連 DB 或執行 handler。"""
import argparse
import hashlib
import json
import re
import struct
import zipfile
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


def canonical_sha(value):
    return sha(json.dumps(value, ensure_ascii=False, sort_keys=True,
                          separators=(',', ':')).encode('utf-8'))


def check_supplemental(contract, source_root):
    """核對內嵌 javap 摘錄；有本機來源時另核對 JAR 與 class bytes。"""
    evidence = contract.get('supplementalJarEvidence')
    if not evidence:
        return {}, 0
    rows = {row['id']: row for row in evidence['rows']}
    require(len(rows) == len(evidence['rows']), 'duplicate supplemental evidence')
    for row in rows.values():
        require(sha(row['text'].encode('utf-8')) == row['excerptSha256'], row['id'] + ': SHA')
        # switch 的 case 值不是方法內指令 offset。
        offsets = [int(n) for n in re.findall(r'^\s+(\d+):\s+[a-z][a-z0-9_]*\b', row['text'], re.M)]
        require([min(offsets), max(offsets)] == row['bytecodeOffset'], row['id'] + ': offsets')
    checks = 0
    if source_root:
        root = source_root.resolve()
        path = (root / evidence['path']).resolve()
        require(path.is_relative_to(root), 'JAR source escapes project')
        require(sha(path.read_bytes()) == evidence['sha256'], 'current JAR SHA')
        checks += 1
        with zipfile.ZipFile(path) as jar:
            for row in rows.values():
                require(sha(jar.read(row['jarEntry'])) == row['classSha256'], row['id'] + ': class SHA')
                checks += 1
    return rows, checks


def template_fields(raw):
    use_types = {'none': -1, 'normal': 0, 'food': 38, 'arrow': -2,
                 'hpr': -7, 'power': -10, 'home': 9}
    require(raw['use_type'] in use_types, 'unverified use_type')
    require(raw['item_id'] not in range(80027, 80031),
            'packet desc requires a different conversion branch')
    use_type = use_types[raw['use_type']]
    catalysts = {40318: 166, 40319: 569, 40321: 837, 49156: 3606, 49157: 3605, 49158: 3674}
    desc = raw['itemdesc_id'] or catalysts.get(raw['item_id'], 0)
    return {'templateId': raw['item_id'], 'name': raw['name'], 'nameToken': raw['name_id'],
            'groundGfx': raw['grdgfx'], 'inventoryIcon': raw['invgfx'],
            'inventoryDesc': desc, 'templateUseType': use_type,
            'wireUseType': use_type & 255, 'stackable': raw['stackable'] == 1,
            'tradable': raw['trade'] == 0, 'cantDelete': raw['cant_delete'] == 1,
            'templateBless': raw['bless'],
            'packetBless': {0: 1, 1: 0}.get(raw['bless'], raw['bless']),
            'droppable': 'unknown', 'equippable': 'unknown',
            'bindingRestrictions': 'unknown', 'storageCountMax': 'unknown'}


def check_templates(contract, evidence):
    templates = contract['adoptedTemplateRows']
    if not templates:
        return {}
    db = contract['dbEvidence']
    require(db['identity']['server_uuid'] == 'e3443e40-d090-11f1-8040-4cedfb679789', 'DB UUID')
    require(db['identity']['port'] == 43324, 'DB port')
    datadir = db['identity']['datadir'].replace('\\', '/').lower().rstrip('/')
    require(datadir.startswith('d:/binengine/environments/p24/'), 'DB datadir')
    queries = {q['id']: q for q in db['queries']}
    for q in queries.values():
        require(q['sql'].lstrip().upper().startswith('SELECT '), 'non-SELECT evidence')
        require(canonical_sha(q['rows']) == q['resultSha256'], 'query result SHA')
        require(q['sql'] == db['templateQuerySql'], 'unexpected template query')
    result = {}
    for row in templates:
        require(row['id'] not in result, 'duplicate template')
        source = row['source']
        query = queries[source['queryId']]
        raw = row['rawColumns']
        require(row['id'] in {'gold','meat','arrow','healing','green','white','home'}, 'unscoped template')
        require(raw['item_id'] == {'gold':40308,'meat':40056,'arrow':40743,'healing':40010,
                                   'green':40013,'white':40012,'home':40079}[row['id']], 'template identity')
        require(raw in query['rows'], row['id'] + ': not in query result')
        require(source['sql'] == query['sql'], row['id'] + ': SQL provenance')
        require(source['location'] == 'db:' + db['schema'] + '.etcitem', 'template table')
        require(source['primaryKey'] == {'item_id': raw['item_id']}, 'template primary key')
        require(source['columns'] == list(raw), 'template column provenance')
        require(source['rowSha256'] == canonical_sha(raw), 'template row SHA')
        require(row['fields'] == template_fields(raw), row['id'] + ': derived fields')
        for field, mapping in row['fieldEvidence'].items():
            require(field in row['fields'], 'unknown mapped field')
            require(mapping['rows'], 'empty field mapping')
            for ref in mapping['rows']:
                require(ref in evidence, ref + ': missing mapping')
                require(mapping['sha256'][ref] == evidence[ref]['excerptSha256'], ref + ': mapping pin')
        require({'templateId', 'groundGfx', 'inventoryIcon', 'nameToken', 'inventoryDesc',
                 'wireUseType', 'stackable', 'tradable', 'cantDelete'} <= set(row['fieldEvidence']),
                row['id'] + ': incomplete field provenance')
        result[row['id']] = row
    return result


def serialize_calls(calls):
    """明示 ASCII／無字串過濾前提；僅 payload，不含 transport framing。"""
    out = bytearray()
    for kind, value in calls:
        if kind == 'S':
            out.extend(b'' if value is None else value.encode('ascii'))
            out.append(0)
        else:
            width = {'C': 1, 'H': 2, 'D': 4}[kind]
            out.extend((value & ((1 << (8 * width)) - 1)).to_bytes(width, 'little'))
    return bytes(out)


def packet_calls(case, row):
    state, fields = case['input'], row['fields']
    count = state['count']
    require(0 < count < 2**63, 'positive long count required')
    require(state['objectId'] != fields['templateId'], 'fixture must distinguish instance/template')
    name = fields['nameToken'] + (f' ({count})' if count > 1 else '')
    displayed = min(count, 2000000000)
    if case['packet'] == 'S87':
        require(fields['templateId'] not in (20383, 40006, 40007, 40008, 40009, 41235, 41236, 140006, 140008),
                'unsupported special ground name')
        require(state['lighting'] is False, 'lighting branch not covered')
        return [['C', 87], ['H', state['x']], ['H', state['y']], ['D', state['objectId']],
                ['H', fields['groundGfx']], ['C', 0], ['C', 0], ['C', 0], ['C', 0],
                ['D', displayed], ['H', 0], ['S', name], ['S', None], ['C', 0], ['D', 0],
                ['S', None], ['S', None], ['C', 0], ['C', 255], ['C', 0], ['C', 0],
                ['C', 0], ['C', 255], ['C', 255]]
    require(case['packet'] == 'S15', 'unsupported response opcode')
    require(count < 1000000000, 'RangeLong inventory name branch not covered')
    require(fields['templateUseType'] in (-1, 0, 38), 'inventory name branch not covered')
    for flag in ('identified', 'sealed', 'equipped', 'equippedTemp', 'protected'):
        require(state[flag] is False, 'unsupported inventory state: ' + flag)
    require(state['chargeCount'] == 0 and state['safeEnchant'] == 0, 'explicit base fixture state')
    flags = (0 if fields['tradable'] else 2) | (4 if fields['cantDelete'] else 0)
    return [['C', 15], ['D', state['objectId']], ['H', fields['inventoryDesc']],
            ['C', fields['wireUseType']], ['C', 0], ['H', fields['inventoryIcon']],
            ['C', fields['packetBless']], ['D', displayed], ['C', flags], ['S', name],
            ['C', 0], ['C', 10], ['H', 0], ['D', 0], ['D', 0]]


def check_packet_fixtures(fixtures, templates, evidence):
    cases = fixtures.get('packetFixtures', [])
    require(len({c['id'] for c in cases}) == len(cases), 'duplicate packet fixture')
    for case in cases:
        require(case['derivation'] == 'static', 'fixture is not marked static')
        require(case['preconditions'], 'missing static preconditions')
        required_encoding = ({'clientpackets.ClientBasePacket.readH','clientpackets.ClientBasePacket.readD',
                              'clientpackets.ClientBasePacket.readUnsigned'} if case['packet']=='C112' else
                             {'serverpackets.ServerBasePacket.writeC','serverpackets.ServerBasePacket.writeH',
                              'serverpackets.ServerBasePacket.writeD','serverpackets.ServerBasePacket.writeS'})
        require(required_encoding <= set(case['rows']), 'missing encoding evidence')
        for ref in case['rows']:
            require(ref in evidence and case['evidenceSha256'][ref] == evidence[ref]['excerptSha256'],
                    case['id'] + ': evidence pin')
        if case['packet'] in ('S87', 'S15'):
            row = templates[case['templateRow']]
            require(case['templateRowSha256'] == row['source']['rowSha256'], 'fixture template pin')
            calls = packet_calls(case, row)
            require(calls == case['expect']['calls'], case['id'] + ': calls')
            payload = bytes.fromhex(case['payloadHex'])
            require(serialize_calls(calls) == payload, case['id'] + ': payload bytes')
            # 另由 bytes 讀回關鍵欄位，避免只驗 JSON 自身。
            if case['packet'] == 'S87':
                opcode, x, y, object_id, gfx = struct.unpack_from('<BHHIH', payload)
                decoded = {'opcode': opcode, 'x': x, 'y': y, 'objectId': object_id, 'groundGfx': gfx,
                           'count': struct.unpack_from('<I', payload, 15)[0],
                           'name': payload[21:].split(b'\0', 1)[0].decode('ascii')}
            else:
                opcode, object_id, desc, use_type, charge, icon, bless, count = struct.unpack_from('<BIHBBHBI', payload)
                decoded = {'opcode': opcode, 'objectId': object_id, 'desc': desc, 'useType': use_type,
                           'charge': charge, 'icon': icon, 'bless': bless, 'count': count,
                           'flags': payload[16], 'name': payload[17:].split(b'\0', 1)[0].decode('ascii')}
            require(decoded == case['expect']['decoded'], case['id'] + ': decoded fields')
        else:
            require(case['packet'] == 'C112', 'unsupported request fixture')
            payload = bytes.fromhex(case['payloadHex'])
            require(len(payload) == 13, 'C112 payload size')
            opcode, x, y, object_id, count = struct.unpack('<BHHIi', payload)
            require(opcode == 112, 'C112 opcode')
            require({'x': x, 'y': y, 'objectId': object_id, 'count': count} == case['expect']['decoded'],
                    case['id'] + ': request decoded')
            state = case['input']
            actual = evaluate({'operation': 'pickup-kind', 'input': state})
            if actual['result'] == 'continue':
                actual = evaluate({'operation': 'pickup-count-owner',
                                   'input': dict(state, requested=count)})
            require(actual == case['expect']['outcome'], case['id'] + ': rejection outcome')
    return len(cases)


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
    supplemental, supplemental_checks = check_supplemental(contract, source_root)
    evidence = {**rows, **supplemental}
    templates = check_templates(contract, evidence)
    packet_count = check_packet_fixtures(fixtures, templates, evidence)
    # 依本次 resource 驗收：至少金幣、肉來源完整；unknown 不冒充已驗證。
    blockers = []
    if not {'金幣', '肉'} <= {t['label'] for t in templates.values()}:
        blockers.append('gold-and-meat-template-rows')
    packet_cases = fixtures.get('packetFixtures', [])
    covered = {(p['packet'], p.get('templateRow')) for p in packet_cases}
    if not all((opcode, key) in covered for opcode in ('S87', 'S15') for key in ('gold', 'meat')):
        blockers.append('gold-and-meat-ground-inventory-fixtures')
    if not {'pickup-nonitem', 'pickup-over-available'} <= {p['id'] for p in packet_cases}:
        blockers.append('nonitem-and-rejection-fixtures')
    if templates and not contract['dbEvidence'].get('shutdownVerified'):
        blockers.append('P24Lab-shutdown-verification')
    # 實際擷取不屬本輪驗收；如未來提供，須另外實作其來源驗證。
    if fixtures.get('liveCapturedFixtures'):
        blockers.append('live-capture-provenance-validator-required')
    require(not contract['qualified'] or not blockers, 'qualified=true contradicts unresolved evidence')
    return {'status': 'partial' if blockers else 'qualified', 'evidenceChecks': 'pass',
            'scope': 'static-template-and-payload-contract; not live-capture or end-to-end',
            'protocolRows': len(contract['adoptedProtocolRows']),
            'mappingRows': len(contract.get('adoptedMappingRows', [])),
            'supplementalRows': len(supplemental),
            'semanticCases': len(cases), 'packetCasesReplayed': packet_count,
            'sourceHashChecks': source_checks + supplemental_checks,
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
