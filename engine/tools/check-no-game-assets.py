"""T035 tracked-content gate; not a portable ZIP/source-provenance certification."""
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
RESOURCES = {
    ('engine/source/OfflineClient.csproj', 'Resources/pickup-own-base15-gfx0-weapon0.json', 'P06.Resources.PickupOwnBase15Gfx0Weapon0.json'),
    ('engine/source/OfflineClient.csproj', 'Resources/pickup-own-base15-profile-extension.json', 'P06.Resources.PickupOwnBase15ProfileExtension.json'),
    ('engine/source/OfflineClient.csproj', 'Resources/ground-catalog.json', 'P06.Resources.GroundCatalog.json'),
    ('engine/audio/P24.Audio.csproj', 'Resources/combat-sfx-pins.json', 'P24.Audio.CombatSfxPins.json'),
}


def magic(data):
    if data.startswith(b'\x89PNG\r\n\x1a\n'): return 'PNG'
    if data.startswith((b'GIF87a', b'GIF89a')): return 'GIF'
    if data.startswith(b'BM'): return 'BMP'
    if data.startswith(b'RIFF') and data[8:12] == b'WAVE': return 'WAV'
    if data.startswith(b'\x1f\x8b'): return 'gzip'
    if data.startswith((b'PK\x03\x04', b'PK\x05\x06', b'PK\x07\x08')): return 'zip'
    return None


def image_json(value):
    if isinstance(value, dict):
        normalized = {k.casefold().replace('_', '').replace('-', ''): v for k, v in value.items()}
        declared = any(isinstance(normalized.get(k), str) and 'rgb555' in normalized[k].casefold()
                       for k in ('format', 'encoding', 'pixelformat', 'colorformat'))
        if declared and any(isinstance(normalized.get(k), (list, dict, str)) and normalized[k]
                            for k in ('data', 'image', 'body', 'values', 'buffer', 'payload')):
            return 'RGB555 declared image body'
        for key, item in value.items():
            name = key.casefold().replace('_', '').replace('-', '')
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


def inspect(path, data, policy):
    digest = hashlib.sha256(data).hexdigest()
    # Every exception is pinned, even when its modified content would no longer match a detector.
    for group in ('b15Exceptions', 'binaryExceptions'):
        if path in policy[group]:
            return None if digest == policy[group][path] else 'allowlist SHA-256 mismatch'
    kind = magic(data)
    if kind and not path.startswith(('docs/demo/', 'docs/native-baseline/')): return kind + ' magic'
    try:
        value = json.loads(data)
    except (ValueError, UnicodeError):
        value = None
    return image_json(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--probe', type=Path, help='Also inspect one untracked scratch probe; never exempts tracked files')
    args = parser.parse_args()
    policy = json.loads(POLICY.read_text('utf-8'))
    paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode('utf-8').split('\0')[:-1]
    errors = []
    resources = set()
    for path in paths:
        file = ROOT / path
        try:
            for ancestor in [file, *file.parents]:
                if ancestor == ROOT: break
                st = ancestor.lstat()
                if stat.S_ISLNK(st.st_mode) or getattr(st, 'st_file_attributes', 0) & 0x400:
                    raise ValueError('tracked reparse point/symlink')
            data = file.read_bytes()
            reason = inspect(path, data, policy)
            if reason: errors.append(f'{path}: {reason}')
            if file.suffix.casefold() in ('.csproj', '.props', '.targets'):
                xml = ET.fromstring(data)
                for node in xml.iter():
                    if node.tag.rsplit('}', 1)[-1] == 'EmbeddedResource':
                        name = node.get('LogicalName') or next((n.text for n in node if n.tag.rsplit('}', 1)[-1] == 'LogicalName'), None)
                        entry = (path, node.get('Include'), name)
                        if entry not in RESOURCES or entry in resources: errors.append(f'{path}: unexpected/duplicate EmbeddedResource {name}')
                        resources.add(entry)
            if file.suffix.casefold() == '.resx': errors.append(f'{path}: implicit EmbeddedResource forbidden')
        except (OSError, ValueError, ET.ParseError) as ex:
            errors.append(f'{path}: {type(ex).__name__}: {ex}')
    for path in set(policy['b15Exceptions']) | set(policy['binaryExceptions']):
        if path not in paths: errors.append(f'{path}: pinned exception missing from tracked inventory')
    if resources != RESOURCES: errors.append('EmbeddedResource inventory differs from the four approved declarations')
    if args.probe:
        probe = args.probe.resolve()
        if not probe.is_relative_to(ROOT / 'scratch'): errors.append('probe must be inside repo scratch/')
        else:
            reason = inspect(probe.relative_to(ROOT).as_posix(), probe.read_bytes(), policy)
            if reason: errors.append(f'{probe.relative_to(ROOT).as_posix()}: {reason}')
    for error in errors: print('REFUSED: ' + error)
    print(json.dumps({'status': 'blocked' if errors else 'pass', 'trackedFiles': len(paths), 'errors': len(errors), 'embeddedResourceDeclarations': len(resources)}))
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
