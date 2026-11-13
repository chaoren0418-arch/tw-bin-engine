"""T035 tracked-content gate; not a portable ZIP/source-provenance certification.

T471 (#1056): the B15 exception list is closed. The only entry allowed is the known limit L1
(engine/source/OriginalExpTable.cs, user ruling L1=A, T476/#1072); adding any other B15 exception,
or dropping its recorded reason, fails. `--self-test` runs the offline unit tests for this gate.
"""
from pathlib import Path
import argparse
import hashlib
import json
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
POLICY = Path(__file__).with_name('game-assets-allowlist.json')
# T463 (#1048): the two pickup JSON resources are no longer embedded; the preparer's "pickup" recipe writes them below assets/pickup.
# T464 (#1049): ground-catalog.json removed (L2 = X); its four SPR pins live in GroundCatalog.Pinned.
# T471 (#1056): this is the full remaining EmbeddedResource inventory (one WAV SHA pin table, no pixels, no timing table).
RESOURCES = {
    ('engine/audio/P24.Audio.csproj', 'Resources/combat-sfx-pins.json', 'P24.Audio.CombatSfxPins.json'),
}
# T471 (#1056): the closed set of B15 exceptions. Each one is a ruled known limit, not a pending removal.
# Adding a path to b15Exceptions without adding it here (a code change reviewed in a PR) is refused.
KNOWN_LIMITS = {
    'engine/source/OriginalExpTable.cs': 'L1',
}
# Directories that must not hold pixels or timing-table resources (product embedded resources were removed in T463/T464).
RESOURCE_DIRS = ('engine/source/Resources/', 'engine/audio/Resources/')
TIMING_KEYS = {'timing', 'timings', 'frametiming', 'frametimings', 'timeline', 'timelines', 'timetable',
               'delays', 'framedelays', 'framedelay', 'durations', 'framedurations', 'durationsms', 'framems'}


def magic(data):
    if data.startswith(b'\x89PNG\r\n\x1a\n'): return 'PNG'
    if data.startswith((b'GIF87a', b'GIF89a')): return 'GIF'
    if data.startswith(b'BM'): return 'BMP'
    if data.startswith(b'RIFF') and data[8:12] == b'WAVE': return 'WAV'
    if data.startswith(b'\x1f\x8b'): return 'gzip'
    if data.startswith((b'PK\x03\x04', b'PK\x05\x06', b'PK\x07\x08')): return 'zip'
    return None


def norm(key):
    return key.casefold().replace('_', '').replace('-', '')


def image_json(value):
    if isinstance(value, dict):
        normalized = {norm(k): v for k, v in value.items()}
        declared = any(isinstance(normalized.get(k), str) and 'rgb555' in normalized[k].casefold()
                       for k in ('format', 'encoding', 'pixelformat', 'colorformat'))
        if declared and any(isinstance(normalized.get(k), (list, dict, str)) and normalized[k]
                            for k in ('data', 'image', 'body', 'values', 'buffer', 'payload')):
            return 'RGB555 declared image body'
        for key, item in value.items():
            name = norm(key)
            if name == 'pixels' and isinstance(item, list): return 'pixels array'
            if 'rgb555' in name and isinstance(item, (list, dict)) and item: return 'RGB555 body'
            if 'rgb555' in name and isinstance(item, str) and len(item) > 64: return 'RGB555 encoded body'
            reason = image_json(item)
            if reason: return reason
    elif isinstance(value, list):
        for item in value:
            reason = image_json(item)
            if reason: return reason
    return None


def timing_json(value):
    """Frame/animation timing tables (client-derived sequences); only enforced for resource payloads."""
    if isinstance(value, dict):
        for key, item in value.items():
            if norm(key) in TIMING_KEYS and isinstance(item, (list, dict)) and item: return f'timing table ({key})'
            reason = timing_json(item)
            if reason: return reason
    elif isinstance(value, list):
        for item in value:
            reason = timing_json(item)
            if reason: return reason
    return None


def load_json(data):
    try:
        return json.loads(data)
    except (ValueError, UnicodeError):
        return None


def inspect(path, data, policy):
    digest = hashlib.sha256(data).hexdigest()
    # Every exception is pinned, even when its modified content would no longer match a detector.
    for group in ('b15Exceptions', 'binaryExceptions'):
        if path in policy[group]:
            return None if digest == policy[group][path] else 'allowlist SHA-256 mismatch'
    kind = magic(data)
    if kind and not path.startswith(('docs/demo/', 'docs/native-baseline/')): return kind + ' magic'
    return image_json(load_json(data))


def resource_payload(path, data):
    """T471: resource payloads (embedded or under a Resources/ dir) may not be pixels or timing tables."""
    if magic(data): return f'{magic(data)} resource payload'
    value = load_json(data)
    reason = image_json(value) or timing_json(value)
    return f'{reason} in resource payload' if reason else None


def policy_errors(policy, tracked):
    """B15 exception policy: closed to KNOWN_LIMITS, each with a reason, each still tracked."""
    errors = []
    b15 = policy.get('b15Exceptions', {})
    reasons = policy.get('b15ExceptionReasons', {})
    for path in sorted(set(b15) - set(KNOWN_LIMITS)):
        errors.append(f'{path}: new B15 exception forbidden (T471: list is closed to known limits {sorted(KNOWN_LIMITS)})')
    for path in sorted(b15):
        if not reasons.get(path): errors.append(f'{path}: B15 exception without a recorded reason')
    for path in sorted(set(reasons) - set(b15)): errors.append(f'{path}: reason recorded for a path that is not a B15 exception')
    for path in sorted(set(b15) | set(policy.get('binaryExceptions', {}))):
        if path not in tracked: errors.append(f'{path}: pinned exception missing from tracked inventory')
    return errors


def known_limit_lines(policy):
    reasons = policy.get('b15ExceptionReasons', {})
    return [f'B15-KNOWN-LIMIT {KNOWN_LIMITS[p]}: {p}: {reasons.get(p, "")}'
            for p in sorted(policy.get('b15Exceptions', {})) if p in KNOWN_LIMITS]


def embedded_resources(path, data):
    xml = ET.fromstring(data)
    for node in xml.iter():
        if node.tag.rsplit('}', 1)[-1] == 'EmbeddedResource':
            name = node.get('LogicalName') or next((n.text for n in node if n.tag.rsplit('}', 1)[-1] == 'LogicalName'), None)
            yield (path, node.get('Include'), name)


def run(root, policy, paths, read):
    """Core gate; `read(path) -> bytes` lets the self-test feed a synthetic tree."""
    errors = []
    resources = set()
    tracked = set(paths)
    for path in paths:
        try:
            data = read(path)
            reason = inspect(path, data, policy)
            if reason: errors.append(f'{path}: {reason}')
            if path.startswith(RESOURCE_DIRS):
                reason = resource_payload(path, data)
                if reason: errors.append(f'{path}: {reason}')
            suffix = Path(path).suffix.casefold()
            if suffix in ('.csproj', '.props', '.targets'):
                for entry in embedded_resources(path, data):
                    _, include, name = entry
                    if entry not in RESOURCES or entry in resources: errors.append(f'{path}: unexpected/duplicate EmbeddedResource {name}')
                    resources.add(entry)
                    target = (Path(path).parent / (include or '')).as_posix()
                    if include and target in tracked:
                        reason = resource_payload(target, read(target))
                        if reason: errors.append(f'{path}: EmbeddedResource {name} -> {target}: {reason}')
            if suffix == '.resx': errors.append(f'{path}: implicit EmbeddedResource forbidden')
        except (OSError, ValueError, ET.ParseError) as ex:
            errors.append(f'{path}: {type(ex).__name__}: {ex}')
    errors += policy_errors(policy, tracked)
    return errors, resources


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe', type=Path, help='Also inspect one untracked scratch probe; never exempts tracked files')
    parser.add_argument('--self-test', action='store_true', help='Run the offline unit tests for this gate and exit')
    args = parser.parse_args()
    if args.self_test: return self_test()
    policy = json.loads(POLICY.read_text('utf-8'))
    paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode('utf-8').split('\0')[:-1]

    def read(path):
        file = ROOT / path
        for ancestor in [file, *file.parents]:
            if ancestor == ROOT: break
            st = ancestor.lstat()
            if stat.S_ISLNK(st.st_mode) or getattr(st, 'st_file_attributes', 0) & 0x400:
                raise ValueError('tracked reparse point/symlink')
        return file.read_bytes()

    errors, resources = run(ROOT, policy, paths, read)
    if args.probe:
        probe = args.probe.resolve()
        if not probe.is_relative_to(ROOT / 'scratch'): errors.append('probe must be inside repo scratch/')
        else:
            reason = inspect(probe.relative_to(ROOT).as_posix(), probe.read_bytes(), policy)
            if reason: errors.append(f'{probe.relative_to(ROOT).as_posix()}: {reason}')
    # Remaining B15 entries are ruled known limits: always printed, never a silent pass.
    for line in known_limit_lines(policy): print(line)
    for error in errors: print('REFUSED: ' + error)
    print(json.dumps({'status': 'blocked' if errors else 'pass', 'trackedFiles': len(paths), 'errors': len(errors),
                      'embeddedResourceDeclarations': len(resources), 'b15Exceptions': len(policy['b15Exceptions']),
                      'b15KnownLimits': sorted(p for p in policy['b15Exceptions'] if p in KNOWN_LIMITS)}))
    return 1 if errors else 0


def self_test():
    import unittest

    exp = 'engine/source/OriginalExpTable.cs'
    exp_bytes = b'// exp table\n'
    csproj = 'engine/audio/P24.Audio.csproj'
    pins = 'engine/audio/Resources/combat-sfx-pins.json'

    def project(extra=''):
        return (f'<Project><ItemGroup><EmbeddedResource Include="Resources/combat-sfx-pins.json" '
                f'LogicalName="P24.Audio.CombatSfxPins.json" />{extra}</ItemGroup></Project>').encode()

    def base():
        files = {exp: exp_bytes, csproj: project(), pins: b'{"wavPins": [{"id": 97, "sha256": "00"}]}',
                 'docs/a.md': b'# ok\n'}
        policy = {'binaryExceptions': {}, 'b15Exceptions': {exp: hashlib.sha256(exp_bytes).hexdigest()},
                  'b15ExceptionReasons': {exp: 'known limit L1'}}
        return files, policy

    def gate(files, policy):
        return run(ROOT, policy, sorted(files), lambda p: files[p])[0]

    class Gate(unittest.TestCase):
        def test_baseline_passes(self):
            self.assertEqual(gate(*base()), [])

        def test_real_policy_is_closed(self):
            policy = json.loads(POLICY.read_text('utf-8'))
            self.assertLessEqual(set(policy['b15Exceptions']), set(KNOWN_LIMITS))

        def test_any_new_b15_exception_fails(self):
            for path, body in (('engine/source/NewTable.cs', b'x'), ('work/x/pixels.json', b'{"pixels": [1]}'),
                               ('engine/source/Resources/t.json', b'{}')):
                files, policy = base()
                files[path] = body
                policy['b15Exceptions'][path] = hashlib.sha256(body).hexdigest()
                policy['b15ExceptionReasons'][path] = 'some reason'
                errors = gate(files, policy)
                self.assertTrue(any('new B15 exception forbidden' in e for e in errors), (path, errors))

        def test_new_exception_without_reason_fails(self):
            files, policy = base()
            files['x.json'] = b'{}'
            policy['b15Exceptions']['x.json'] = hashlib.sha256(b'{}').hexdigest()
            errors = gate(files, policy)
            self.assertTrue(any('new B15 exception forbidden' in e for e in errors))
            self.assertTrue(any('without a recorded reason' in e for e in errors))

        def test_known_limit_needs_reason(self):
            files, policy = base()
            policy['b15ExceptionReasons'] = {}
            self.assertTrue(any('without a recorded reason' in e for e in gate(files, policy)))

        def test_known_limit_is_sha_pinned(self):
            files, policy = base()
            files[exp] = b'// edited\n'
            self.assertTrue(any('allowlist SHA-256 mismatch' in e for e in gate(files, policy)))

        def test_known_limit_printed(self):
            _, policy = base()
            self.assertEqual(known_limit_lines(policy), [f'B15-KNOWN-LIMIT L1: {exp}: known limit L1'])

        def test_dropping_known_limit_is_allowed(self):
            files, policy = base()
            del files[exp]
            policy['b15Exceptions'] = {}
            policy['b15ExceptionReasons'] = {}
            self.assertEqual(gate(files, policy), [])

        def test_pixels_in_resources_dir_fails(self):
            files, _ = base()
            files['engine/source/Resources/icon.json'] = b'{"w": 2, "pixels": [1, 2]}'
            self.assertTrue(any('engine/source/Resources/icon.json' in e for e in gate(files, base()[1])))

        def test_timing_table_in_resources_dir_fails(self):
            files, policy = base()
            files['engine/source/Resources/anim.json'] = b'{"sprite": 6016, "frameTimings": [100, 120]}'
            self.assertTrue(any('timing table' in e for e in gate(files, policy)))

        def test_timing_table_in_embedded_payload_fails(self):
            files, policy = base()
            files[pins] = b'{"wavPins": [], "timeline": [{"t": 0}]}'
            self.assertTrue(any('timing table' in e for e in gate(files, policy)))

        def test_new_embedded_resource_fails(self):
            files, policy = base()
            files['engine/source/OfflineClient.csproj'] = (b'<Project><ItemGroup><EmbeddedResource Include="Resources/a.json" '
                                                           b'LogicalName="X.A.json" /></ItemGroup></Project>')
            files['engine/source/Resources/a.json'] = b'{}'
            self.assertTrue(any('unexpected/duplicate EmbeddedResource X.A.json' in e for e in gate(files, policy)))

        def test_image_magic_in_resources_dir_fails(self):
            files, policy = base()
            files['engine/source/Resources/a.dat'] = b'\x89PNG\r\n\x1a\n...'
            self.assertTrue(any('PNG' in e for e in gate(files, policy)))

        def test_timing_keys_outside_resources_not_flagged(self):
            files, policy = base()
            files['docs/report.json'] = b'{"timings": [1, 2]}'
            self.assertEqual(gate(files, policy), [])

    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Gate))
    return 0 if result.wasSuccessful() else 1


if __name__ == '__main__':
    sys.exit(main())
