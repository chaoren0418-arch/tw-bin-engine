"""T472 (#1057) portable zip: explicit allowlist packaging + content review.

Collects only the files named in this script (never a recursive copy of engine/, never through a
junction/symlink), writes a deterministic zip and a file list (path, size, SHA-256) below
engine/build/portable/, then re-opens the zip and reviews every entry by name, magic bytes and
(for .NET assemblies) embedded ManifestResources read with engine/_integration/dev/pe_resource.py.

  python -B -X utf8 engine/tools/package-portable.py              package + review (needs a dev build)
  python -B -X utf8 engine/tools/package-portable.py --review ZIP review an existing zip only
  python -B -X utf8 engine/tools/package-portable.py --self-test  offline unit tests (synthetic trees)

Never uploads or publishes anything; a GitHub Release is a separate, user-approved task.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import importlib.util
import io
import json
import os
import re
import stat
import struct
import sys
import zipfile

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'engine' / '_integration' / 'dev'))
import pe_resource  # noqa: E402  (existing PE/CLR metadata reader; never loads the DLL)

_spec = importlib.util.spec_from_file_location('check_no_game_assets', Path(__file__).with_name('check-no-game-assets.py'))
gate = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(gate)

OUT = 'engine/build/portable'
ZIP_NAME = 'tw-bin-engine-portable.zip'
LIST_NAME = 'tw-bin-engine-portable.files.json'
PRODUCT = 'engine/build/client-product-dev'
MANIFEST = 'engine/build/product-dev-manifest.json'

# ---- explicit allowlist (zip path -> repo source path). Nothing else is ever packaged. ----
PRODUCT_FILES = (
    'P11.ClientEngine.exe', 'P11.ClientEngine.dll', 'P11.ClientEngine.deps.json', 'P11.ClientEngine.runtimeconfig.json',
    'P24.Audio.dll',
    'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll',
    'runtimes/win-x64/native/WebView2Loader.dll',
)
LAUNCHER_FILES = ('P24.Launcher.exe', 'P24.Launcher.dll', 'P24.Launcher.deps.json', 'P24.Launcher.runtimeconfig.json')
WEB_FILES = ('index.html', 'panel.css', 'panel.js', 'login.js', 'interaction.js', 'bonus-stats.js', 'private-shop.js', 'trade.js')
# Portable README/entry are user-facing templates (the repo-root README/啟動引擎.cmd are the developer/r33 entry).
TEMPLATES = {'README.md': 'engine/tools/portable/README.md', '啟動引擎.cmd': 'engine/tools/portable/啟動引擎.cmd'}
# "Necessary docs" (license / usage / troubleshooting text). Empty until such a doc passes content review;
# README.md above carries the usage steps. Entries are repo paths and are packaged at the same path.
DOCS = ()


def allowlist():
    """[(zip path, repo-relative source path)] in a fixed order; the product manifest is generated separately."""
    rows = [(z, s) for z, s in TEMPLATES.items()]
    rows += [(f'engine/launcher/{n}', f'engine/launcher/{n}') for n in LAUNCHER_FILES]
    rows += [(f'{PRODUCT}/{n}', f'{PRODUCT}/{n}') for n in PRODUCT_FILES]
    rows += [(f'engine/web/{n}', f'engine/web/{n}') for n in WEB_FILES]
    rows += [(d, d) for d in DOCS]
    return rows


def expected_zip_paths():
    return {z for z, _ in allowlist()} | {MANIFEST}


# ---- review policy ----
FORBIDDEN_EXT = {
    # images / audio / video
    '.png', '.gif', '.bmp', '.jpg', '.jpeg', '.tga', '.webp', '.ico', '.dds', '.wav', '.mp3', '.ogg', '.flac', '.wma', '.avi', '.mp4',
    # client containers and formats
    '.pak', '.idx', '.spr', '.spz', '.tbt', '.til', '.s32', '.seg', '.lac', '.map', '.tbl', '.img', '.smp', '.shadow',
    # debug / source / tooling / data
    '.pdb', '.cs', '.csproj', '.props', '.targets', '.sln', '.py', '.pyc', '.ps1', '.sql', '.jar', '.class', '.bin', '.idb', '.i64',
    '.id0', '.id1', '.nam', '.til2', '.db', '.sqlite', '.zip', '.7z', '.rar', '.gz', '.cab', '.nupkg', '.resx', '.xml',
}
FORBIDDEN_SEGMENTS = {'assets', 'icons', 'ground', 'sound', 'map', 'maps', 'sprite', 'tile', 'text', 'vendor', 'source', 'runtime',
                      'releases', 'scratch', 'evidence', 'research', '.nuget', 'obj', 'history', 'recovery', 'environments'}
MAGICS = (
    (b'\x89PNG\r\n\x1a\n', 'PNG'), (b'GIF87a', 'GIF'), (b'GIF89a', 'GIF'), (b'\xff\xd8\xff', 'JPEG'), (b'RIFF', 'RIFF/WAV'),
    (b'OggS', 'Ogg'), (b'ID3', 'MP3'), (b'fLaC', 'FLAC'), (b'PK\x03\x04', 'zip'), (b'PK\x05\x06', 'zip'), (b'\x1f\x8b', 'gzip'),
    (b"7z\xbc\xaf'\x1c", '7z'), (b'Rar!', 'RAR'), (b'Microsoft C/C++ MSF 7.00', 'PDB'), (b'BSJB', 'portable PDB/metadata'),
    (b'\xca\xfe\xba\xbe', 'Java class'), (b'IDA0', 'IDA database'), (b'IDA1', 'IDA database'), (b'IDA2', 'IDA database'),
    (b'SQLite format 3', 'SQLite'), (b'MSCF', 'CAB'),
)
EMBEDDED_SIGNATURES = ((b'\x89PNG\r\n\x1a\n', 'PNG'), (b'GIF89a', 'GIF'), (b'GIF87a', 'GIF'), (b'WAVEfmt ', 'WAV'),
                       (b'OggS\x00', 'Ogg'), (b'Microsoft C/C++ MSF 7.00', 'PDB'))
DATA_URI = re.compile(rb'data:(image|audio|video|application/octet-stream)[^,]{0,64},', re.I)
BASE64_RUN = re.compile(rb'[A-Za-z0-9+/]{2048,}={0,2}')
SQL = re.compile(rb'\b(CREATE\s+TABLE|INSERT\s+INTO|DROP\s+TABLE|ALTER\s+TABLE)\b', re.I)
# The only managed resources a packaged assembly may carry (T471 closed list) and pinned third-party ones.
ALLOWED_RESOURCES = {('P24.Audio.dll', 'P24.Audio.CombatSfxPins.json')}
PINNED_THIRD_PARTY = {
    # Microsoft WebView2 1.0.4258.31 WinForms toolbox bitmap (Visual Studio designer icon; not game content).
    ('Microsoft.Web.WebView2.WinForms.dll', 'Microsoft.Web.WebView2.WinForms.Microsoft.Web.WebView2.WinForm.WebView2.bmp'):
        '0d0e717fa6a97d2cd2b581280de71738d91f055c167effd449e77f9688a20d64',
}
assert {n for _, n in ALLOWED_RESOURCES} == {name for _, _, name in gate.RESOURCES}, 'resource policy drifted from check-no-game-assets'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def content_magic(data):
    for sig, kind in MAGICS:
        if data.startswith(sig):
            return kind
    return None


def name_errors(name):
    errors = []
    path = PurePosixPath(name)
    if '\\' in name or name.startswith('/') or re.match(r'^[A-Za-z]:', name) or '..' in path.parts or name.endswith('/'):
        return [f'{name}: unsafe zip path']
    for part in path.parts[:-1]:
        if part.casefold() in FORBIDDEN_SEGMENTS:
            errors.append(f'{name}: forbidden directory segment "{part}"')
    for suffix in path.suffixes:
        if suffix.casefold() in FORBIDDEN_EXT:
            errors.append(f'{name}: forbidden extension {suffix}')
    return errors


def resource_errors(name, data):
    base = PurePosixPath(name).name
    errors = []
    try:
        resources = pe_resource.manifest_resources(data)
    except (ValueError, IndexError, KeyError, struct.error, UnicodeError) as ex:
        return [f'{name}: cannot read PE/CLR metadata ({ex}); refusing an unreviewable binary']
    for row in resources or []:
        logical = row['logicalName']
        label = f'{name}: embedded resource {logical}'
        if not row['embedded']:
            errors.append(f'{label}: linked (non-embedded) resource')
            continue
        payload = row['payload']
        pin = PINNED_THIRD_PARTY.get((base, logical))
        if pin is not None:
            if sha(payload) != pin:
                errors.append(f'{label}: pinned third-party resource SHA-256 mismatch')
            continue
        if (base, logical) not in ALLOWED_RESOURCES:
            errors.append(f'{label}: not on the embedded-resource allowlist')
        errors += [f'{label}: {e}' for e in name_errors('r/' + logical) if 'extension' in e]
        kind = content_magic(payload)
        if kind:
            errors.append(f'{label}: {kind} payload')
        for sig, kind in EMBEDDED_SIGNATURES:
            if sig in payload:
                errors.append(f'{label}: contains {kind} signature')
                break
        reason = gate.resource_payload(logical, payload)
        if reason:
            errors.append(f'{label}: {reason}')
    return errors


def text_errors(name, data):
    if b'\0' in data:
        return [f'{name}: binary content under a text name']
    try:
        data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return [f'{name}: not UTF-8 text']
    errors = []
    if DATA_URI.search(data):
        errors.append(f'{name}: embedded data: URI (image/audio/binary)')
    if BASE64_RUN.search(data):
        errors.append(f'{name}: long base64 run (possible embedded binary)')
    if SQL.search(data):
        errors.append(f'{name}: SQL statements')
    if name.endswith('.json'):
        value = gate.load_json(data)
        if value is None:
            errors.append(f'{name}: invalid JSON')
        else:
            reason = gate.image_json(value) or gate.timing_json(value)
            if reason:
                errors.append(f'{name}: {reason}')
    return errors


def inspect_entry(name, data):
    errors = name_errors(name)
    is_pe_name = PurePosixPath(name).suffix.casefold() in ('.exe', '.dll')
    if data.startswith(b'MZ'):
        if not is_pe_name:
            errors.append(f'{name}: PE executable under a non-PE name')
        errors += resource_errors(name, data)
        return errors
    if is_pe_name:
        errors.append(f'{name}: .exe/.dll without a PE header')
    kind = content_magic(data)
    if kind:
        errors.append(f'{name}: {kind} magic')
        return errors
    return errors + text_errors(name, data)


def manifest_errors(files):
    """The packaged product manifest must pin exactly the packaged engine program/web files."""
    errors = []
    raw = files.get(MANIFEST)
    if raw is None:
        return [f'{MANIFEST}: missing']
    try:
        manifest = json.loads(raw)
        rows = {r['path']: r for r in manifest['files']}
    except (ValueError, KeyError, TypeError) as ex:
        return [f'{MANIFEST}: unreadable ({ex})']
    pinned = {f'engine/{p}' for p in rows}
    packaged = {z for z in files if z.startswith((PRODUCT + '/', 'engine/web/'))}
    for z in sorted(packaged - pinned):
        errors.append(f'{z}: packaged but not pinned in the product manifest')
    for z in sorted(pinned - packaged):
        errors.append(f'{z}: pinned in the product manifest but not packaged')
    for z in sorted(pinned & packaged):
        row = rows[z[len('engine/'):]]
        if row.get('size') != len(files[z]) or row.get('sha256') != sha(files[z]):
            errors.append(f'{z}: size/SHA-256 differs from the product manifest')
    for need in ('build/client-product-dev/P11.ClientEngine.exe', 'build/client-product-dev/P11.ClientEngine.dll',
                 'build/client-product-dev/P11.ClientEngine.runtimeconfig.json',
                 'build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll'):
        if need not in rows:
            errors.append(f'{MANIFEST}: launcher-required pin {need} missing')
    return errors


def review_zip(zip_path):
    """Re-open the zip and review every entry; returns (errors, files list)."""
    errors, files, listing = [], {}, []
    expected = expected_zip_paths()
    with zipfile.ZipFile(zip_path) as zf:
        for info in zf.infolist():
            name = info.filename
            if info.is_dir():
                errors.append(f'{name}: directory entry')
                continue
            if name in files:
                errors.append(f'{name}: duplicate zip entry')
                continue
            mode = (info.external_attr >> 16) & 0o170000
            if mode and mode != stat.S_IFREG:
                errors.append(f'{name}: not a regular file (symlink/special) in zip')
            if name not in expected:
                errors.append(f'{name}: not on the portable allowlist')
            data = zf.read(info)
            files[name] = data
            listing.append(dict(path=name, size=len(data), sha256=sha(data)))
            errors += inspect_entry(name, data)
    for missing in sorted(expected - set(files)):
        errors.append(f'{missing}: allowlisted file missing from zip')
    errors += manifest_errors(files)
    return errors, listing


# ---- collection (never follows a junction/symlink, never walks engine/) ----
def is_link(path):
    st = path.lstat()
    return stat.S_ISLNK(st.st_mode) or bool(getattr(st, 'st_file_attributes', 0) & 0x400)


def safe_read(root, rel):
    path = root / rel
    current = path
    while True:
        if current == root:
            break
        if current.exists() or os.path.lexists(current):
            if is_link(current):
                raise ValueError(f'{rel}: path goes through a junction/symlink ({current.relative_to(root).as_posix()})')
        current = current.parent
        if current == current.parent:
            raise ValueError(f'{rel}: outside the repository')
    st = path.lstat()
    if not stat.S_ISREG(st.st_mode):
        raise ValueError(f'{rel}: not a regular file')
    return path.read_bytes()


def excluded_build_files(root):
    """Report (not package) build outputs left out by the allowlist, without following links."""
    allowed = {f'{PRODUCT}/{n}' for n in PRODUCT_FILES} | {f'engine/launcher/{n}' for n in LAUNCHER_FILES}
    rows = []
    for base in (PRODUCT, 'engine/launcher'):
        stack = [root / base]
        while stack:
            d = stack.pop()
            if not d.is_dir() or is_link(d):
                continue
            with os.scandir(d) as it:
                for e in it:
                    p = Path(e.path)
                    rel = p.relative_to(root).as_posix()
                    if e.is_dir(follow_symlinks=False) and not is_link(p):
                        stack.append(p)
                    elif rel not in allowed:
                        suffix = p.suffix.casefold()
                        reason = {'.pdb': 'PDB', '.xml': 'API doc XML'}.get(suffix)
                        if reason is None and '/runtimes/' in rel:
                            reason = 'non-x64 WebView2 loader'
                        rows.append(dict(path=rel, size=e.stat(follow_symlinks=False).st_size, reason=reason or 'not on allowlist'))
    return sorted(rows, key=lambda r: r['path'])


def collect(root):
    """Returns (entries {zip path: bytes}, excluded rows, errors)."""
    errors, entries = [], {}
    for zip_path, rel in allowlist():
        try:
            entries[zip_path] = safe_read(root, rel)
        except (OSError, ValueError) as ex:
            errors.append(str(ex) if isinstance(ex, ValueError) else f'{rel}: {type(ex).__name__}: {ex}')
    try:
        source = json.loads(safe_read(root, MANIFEST))
        rows = {r['path']: r for r in source['files']}
    except (OSError, ValueError, KeyError, TypeError) as ex:
        return entries, [], errors + [f'{MANIFEST}: {type(ex).__name__}: {ex}']
    excluded = excluded_build_files(root)
    files = []
    for zip_path, data in entries.items():
        if not zip_path.startswith((PRODUCT + '/', 'engine/web/')):
            continue
        rel = zip_path[len('engine/'):]
        row = rows.get(rel)
        if row is None:
            errors.append(f'{zip_path}: not pinned in the build product manifest')
        elif row.get('size') != len(data) or row.get('sha256') != sha(data):
            errors.append(f'{zip_path}: differs from the build product manifest (stale or modified build output)')
        files.append(dict(path=rel, size=len(data), sha256=sha(data)))
    packaged = {f['path'] for f in files}
    scanned = {r['path'] for r in excluded}  # build files already reported with their specific reason
    for rel in sorted(set(rows) - packaged):
        if f'engine/{rel}' in scanned:
            continue
        excluded.append(dict(path=f'engine/{rel}', size=rows[rel].get('size'), reason='manifest row outside allowlist (dropped from portable manifest)'))
    portable = {k: v for k, v in source.items() if k != 'files'}
    portable['files'] = files
    entries[MANIFEST] = (json.dumps(portable, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    return entries, excluded, errors


def write_zip(entries, path):
    """Deterministic: sorted names, fixed timestamp, fixed permissions."""
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for name in sorted(entries):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            zf.writestr(info, entries[name])
    path.write_bytes(buffer.getvalue())
    return buffer.getvalue()


def package(root, out_dir):
    entries, excluded, errors = collect(root)
    result = dict(schema='tw-portable-zip-files-v1', status='refused', zip=None, zipSha256=None, files=[], fileCount=0, totalSize=0,
                  excludedBuildFiles=excluded, errors=errors, published=False)
    if not errors:
        zip_path = out_dir / ZIP_NAME
        blob = write_zip(entries, zip_path)
        review, listing = review_zip(zip_path)
        result.update(zip=zip_path.name, zipSize=len(blob), zipSha256=sha(blob), files=listing, fileCount=len(listing),
                      totalSize=sum(f['size'] for f in listing), errors=review, status='pass' if not review else 'refused')
        if review:
            zip_path.unlink()  # a refused zip is never left behind as if it were deliverable
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / LIST_NAME).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--review', type=Path, help='review an existing zip only')
    parser.add_argument('--self-test', action='store_true', help='run the offline unit tests on synthetic trees')
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    if args.review:
        errors, listing = review_zip(args.review)
        summary = dict(status='pass' if not errors else 'refused', fileCount=len(listing), totalSize=sum(f['size'] for f in listing), errors=len(errors))
    elif template_errors(REPO):  # README/entry drifted from the program: refuse before anything is packaged
        errors, summary = template_errors(REPO), dict(status='refused', zip=None, published=False)
    else:
        result = package(REPO, REPO / OUT)
        errors = result['errors']
        summary = dict(status=result['status'], zip=f"{OUT}/{result['zip']}" if result['zip'] and not errors else None,
                       fileList=f'{OUT}/{LIST_NAME}', fileCount=result['fileCount'], totalSize=result['totalSize'],
                       zipSize=result.get('zipSize'), zipSha256=result['zipSha256'], excludedBuildFiles=len(result['excludedBuildFiles']),
                       errors=len(errors), published=False)
        for row in result['files']:
            print(f"FILE {row['size']:>10} {row['sha256']} {row['path']}")
    for error in errors:
        print('REFUSED: ' + error)
    print(json.dumps(summary, ensure_ascii=False))
    return 1 if errors else 0


# ---- portable README / entry templates (T473) ----
# Program text the README may quote: the launcher and the preparation command/core it reports from.
MESSAGE_SOURCES = ('engine/_integration/dev/launcher-source/Program.cs', 'engine/source/AssetPrep/*.cs')
CLIENT_ROOT_LINE = re.compile(r'^set "TW_CLIENT_ROOT=([^"]+)"$', re.I)
CJK = re.compile(r'[㐀-鿿＀-￯]')


def template_errors(repo):
    """The README quotes program messages verbatim (`…` marks a value filled in at run time) and the entry has
    exactly one TW_CLIENT_ROOT setting. Reads the repo templates and C# sources only; runs nothing."""
    errors = []
    readme = (repo / TEMPLATES['README.md']).read_text('utf-8')
    raw = (repo / TEMPLATES['啟動引擎.cmd']).read_bytes()
    corpus = '\n'.join(p.read_text('utf-8-sig') for pattern in MESSAGE_SOURCES for p in sorted(repo.glob(pattern)))
    corpus = corpus.replace('\\"', '"')
    if not raw.isascii():
        errors.append('啟動引擎.cmd: must be ASCII (cmd.exe reads it in the console code page)')
    if raw.replace(b'\r\n', b'').count(b'\n') or raw.replace(b'\r\n', b'').count(b'\r'):
        errors.append('啟動引擎.cmd: line endings must be CRLF')
    cmd = raw.decode('ascii', 'replace').splitlines()
    roots = [line for line in cmd if CLIENT_ROOT_LINE.match(line.strip())]
    assigned = [line for line in cmd if re.match(r'^\s*set\s+"?TW_CLIENT_ROOT=', line, re.I)]
    if len(roots) != 1 or len(assigned) != 1:
        errors.append(f'啟動引擎.cmd: expected exactly one set "TW_CLIENT_ROOT=..." line, found {len(assigned)}')
    for need in ('set "TW_ENGINE_ROOT=%~dp0engine"', '"%TW_ENGINE_ROOT%\\launcher\\P24.Launcher.exe" %*', 'exit /b %TW_EXIT%'):
        if need not in (line.strip() for line in cmd):
            errors.append(f'啟動引擎.cmd: missing line {need}')
    if 'P24.Launcher.exe' not in LAUNCHER_FILES:
        errors.append('啟動引擎.cmd: launcher exe is not packaged')
    if roots and roots[0].strip() not in readme:
        errors.append(f'README.md: does not show the entry line {roots[0].strip()}')
    if 'set \\"TW_CLIENT_ROOT=' not in (repo / 'engine/source/AssetPrep/AssetPreparationState.cs').read_text('utf-8-sig'):
        errors.append('AssetPreparationState.ClientRootHint no longer points at the set "TW_CLIENT_ROOT=..." line')
    for span in re.findall(r'`([^`\n]+)`', readme):
        if span.startswith('[tw-bin-engine]'):
            if not any(span in line for line in cmd):
                errors.append(f'README.md: entry message not in 啟動引擎.cmd: {span}')
        elif CJK.search(span) and span not in TEMPLATES:
            missing = [piece.strip() for piece in span.split('…') if piece.strip() and piece.strip() not in corpus]
            if missing:
                errors.append(f'README.md: quoted message not found in program sources: {span} (missing {missing})')
    return errors


# ---- self-test (synthetic trees only; written below repo scratch/) ----
def synth_pe(resources=None):
    """Minimal PE32+ that pe_resource can parse. resources=None -> native PE (no CLI header)."""
    section = bytearray()
    va = 0x2000
    if resources is not None:
        strings = bytearray(b'\0')
        name_index = []
        for name, _ in resources:
            name_index.append(len(strings))
            strings += name.encode('utf-8') + b'\0'
        while len(strings) % 4:
            strings += b'\0'
        blob, offsets = bytearray(), []
        for _, payload in resources:
            offsets.append(len(blob))
            blob += struct.pack('<I', len(payload)) + payload
            while len(blob) % 8:
                blob += b'\0'
        tables = bytearray(struct.pack('<IBBBBQQ', 0, 2, 0, 0, 1, 1 << 40, 0))
        tables += struct.pack('<I', len(resources))
        for off, idx in zip(offsets, name_index):
            tables += struct.pack('<IIHH', off, 1, idx, 0)
        while len(tables) % 4:
            tables += b'\0'
        version = b'v4.0.30319\0\0'
        root_size = 16 + len(version) + 4 + 12 + 20
        metadata = bytearray(b'BSJB' + struct.pack('<HHII', 1, 1, 0, len(version)) + version + struct.pack('<HH', 0, 2))
        metadata += struct.pack('<II', root_size, len(tables)) + b'#~\0\0'
        metadata += struct.pack('<II', root_size + len(tables), len(strings)) + b'#Strings\0\0\0\0'
        metadata += tables + strings
        cli_size = 72
        meta_rva = va + cli_size
        res_rva = meta_rva + len(metadata)
        cli = struct.pack('<IHHIIIIII', cli_size, 2, 5, meta_rva, len(metadata), 1, 0, res_rva if blob else 0, len(blob))
        section += cli.ljust(cli_size, b'\0') + metadata + blob
    section += b'\0' * 16
    raw_size = (len(section) + 0x1ff) & ~0x1ff
    pe_off = 0x80
    head = bytearray(0x200)
    head[0:2] = b'MZ'
    struct.pack_into('<I', head, 60, pe_off)
    head[pe_off:pe_off + 4] = b'PE\0\0'
    struct.pack_into('<HHIIIHH', head, pe_off + 4, 0x8664, 1, 0, 0, 0, 240, 0x22)
    opt = pe_off + 24
    struct.pack_into('<H', head, opt, 0x20b)
    struct.pack_into('<I', head, opt + 108, 16)
    if resources is not None:
        struct.pack_into('<II', head, opt + 112 + 14 * 8, va, 72)
    sec = opt + 240
    head[sec:sec + 8] = b'.text\0\0\0'
    struct.pack_into('<IIII', head, sec + 8, len(section), va, raw_size, 0x200)
    return bytes(head) + bytes(section).ljust(raw_size, b'\0')


def synth_tree(root):
    """A clean synthetic repo: allowlisted program files, a build manifest, plus build files to exclude."""
    def put(rel, data):
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
    for zip_path, rel in allowlist():
        name = PurePosixPath(rel).name
        if name.endswith('.exe') or name == 'WebView2Loader.dll':
            data = synth_pe(None)
        elif name == 'P24.Audio.dll':
            data = synth_pe([('P24.Audio.CombatSfxPins.json', b'{"wavPins": [{"id": 97, "sha256": "00"}]}')])
        elif name.endswith('.dll'):
            data = synth_pe([])
        elif name.endswith('.json'):
            data = b'{"runtimeOptions": {"tfm": "net10.0"}}\n'
        else:
            data = f'/* synthetic {name} */\n'.encode('utf-8')
        put(rel, data)
    put(f'{PRODUCT}/P11.ClientEngine.pdb', b'Microsoft C/C++ MSF 7.00\r\n\x1aDS\0\0\0')
    put(f'{PRODUCT}/Microsoft.Web.WebView2.Core.xml', b'<doc/>')
    put(f'{PRODUCT}/runtimes/win-x86/native/WebView2Loader.dll', synth_pe(None))
    write_build_manifest(root)


def write_build_manifest(root):
    rows = []
    for base in (PRODUCT, 'engine/web'):
        for p in sorted((root / base).rglob('*')):
            if p.is_file():
                rows.append(dict(path=p.relative_to(root / 'engine').as_posix(), size=p.stat().st_size, sha256=sha(p.read_bytes())))
    rows.append(dict(path='assets/integrity.json', size=1, sha256='00'))
    manifest = dict(schema='p24-normal-product-v1', entry='build/client-product-dev/P11.ClientEngine.exe', arguments=['--normal-product'],
                    profile='p24-real-jar-d287-client2', port=42061, autoLogin=False, files=rows)
    (root / MANIFEST).write_text(json.dumps(manifest, indent=2), 'utf-8')


def make_link(link, target):
    """Directory junction on Windows (what the repo actually uses), symlink elsewhere."""
    if os.name == 'nt':
        import _winapi
        _winapi.CreateJunction(str(target), str(link))
        return 'junction'
    os.symlink(target, link, target_is_directory=True)
    return 'symlink'


def self_test():
    import shutil
    import tempfile
    import unittest

    scratch = REPO / 'scratch'
    scratch.mkdir(exist_ok=True)
    png = b'\x89PNG\r\n\x1a\n\0\0\0\rIHDR' + b'\0' * 16
    pixels = json.dumps({'gfx': 1, 'pixels': [[0, 0, 31744]]}).encode()

    class Portable(unittest.TestCase):
        def setUp(self):
            self.tmp = Path(tempfile.mkdtemp(prefix='portable-', dir=scratch))
            self.root = self.tmp / 'repo'
            synth_tree(self.root)

        def tearDown(self):
            for p in list(self.tmp.rglob('*')):  # remove links first so cleanup can never follow them
                if os.path.lexists(p) and is_link(p):
                    # Directory junction/symlink on Windows needs rmdir (never recursive); file links use unlink.
                    os.rmdir(p) if os.name == 'nt' and p.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_DIRECTORY else os.unlink(p)
            shutil.rmtree(self.tmp)

        def package(self):
            return package(self.root, self.tmp / 'out')

        def zip_with(self, extra=None, replace=None):
            """Hand-made zip: a clean package, then entries added/replaced (simulates a tampered or wrong zip)."""
            entries, _, errors = collect(self.root)
            self.assertEqual(errors, [])
            entries.update(replace or {})
            path = self.tmp / 'tampered.zip'
            write_zip({**entries, **(extra or {})}, path)
            return review_zip(path)[0]

        def assertRefused(self, errors, *needles):
            self.assertTrue(errors, 'expected refusal')
            for needle in needles:
                self.assertTrue(any(needle in e for e in errors), (needle, errors))

        def test_pe_resource_reads_synthetic_assembly(self):
            rows = pe_resource.manifest_resources(synth_pe([('A.json', b'{}'), ('B.bin', b'xyz')]))
            self.assertEqual([(r['logicalName'], r['payload']) for r in rows], [('A.json', b'{}'), ('B.bin', b'xyz')])
            self.assertIsNone(pe_resource.manifest_resources(synth_pe(None)))

        def test_clean_tree_passes_and_lists_files(self):
            result = self.package()
            self.assertEqual(result['errors'], [])
            self.assertEqual(result['status'], 'pass')
            self.assertEqual(result['fileCount'], len(expected_zip_paths()))
            self.assertEqual({f['path'] for f in result['files']}, expected_zip_paths())
            self.assertEqual(result['totalSize'], sum(f['size'] for f in result['files']))
            excluded = {r['path']: r['reason'] for r in result['excludedBuildFiles']}
            self.assertEqual(excluded[f'{PRODUCT}/P11.ClientEngine.pdb'], 'PDB')
            self.assertEqual(excluded[f'{PRODUCT}/runtimes/win-x86/native/WebView2Loader.dll'], 'non-x64 WebView2 loader')
            listed = json.loads((self.tmp / 'out' / LIST_NAME).read_text('utf-8'))
            self.assertEqual(listed['zipSha256'], sha((self.tmp / 'out' / ZIP_NAME).read_bytes()))
            with zipfile.ZipFile(self.tmp / 'out' / ZIP_NAME) as zf:
                manifest = json.loads(zf.read(MANIFEST))
            paths = {r['path'] for r in manifest['files']}
            self.assertNotIn('assets/integrity.json', paths)
            self.assertNotIn('build/client-product-dev/P11.ClientEngine.pdb', paths)
            self.assertIn('build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll', paths)

        def test_zip_is_deterministic(self):
            first = self.package()['zipSha256']
            self.assertEqual(self.package()['zipSha256'], first)

        def test_renamed_png_refused(self):
            for name in ('engine/web/panel.css', 'engine/web/index.html'):
                self.assertRefused(self.zip_with(replace={name: png}), f'{name}: PNG magic')
            self.assertRefused(self.zip_with(extra={'engine/web/logo.js': png}), 'not on the portable allowlist', 'PNG magic')
            # Same through the real collector: a PNG renamed into the build is refused and no zip is kept.
            (self.root / 'engine/web/trade.js').write_bytes(png)
            write_build_manifest(self.root)
            result = self.package()
            self.assertEqual(result['status'], 'refused')
            self.assertRefused(result['errors'], 'engine/web/trade.js: PNG magic')
            self.assertFalse((self.tmp / 'out' / ZIP_NAME).exists())

        def test_renamed_binaries_refused(self):
            cases = {
                'engine/launcher/P24.Launcher.deps.json': (b'Microsoft C/C++ MSF 7.00\r\n\x1aDS', 'PDB magic'),
                'engine/web/login.js': (b'RIFF\0\0\0\0WAVEfmt ', 'RIFF/WAV magic'),
                'engine/web/panel.js': (synth_pe(None), 'PE executable under a non-PE name'),
                'engine/web/trade.js': (b'\x00\x01\x02 sprite frame bytes', 'binary content under a text name'),
                'engine/web/bonus-stats.js': (b'\xca\xfe\xba\xbe\0\0\0\x34', 'Java class magic'),
                'engine/web/interaction.js': (b'IDA1\0\0', 'IDA database magic'),
                f'{PRODUCT}/Microsoft.Web.WebView2.Core.dll': (b'{"not": "a dll"}', '.exe/.dll without a PE header'),
            }
            for name, (data, needle) in cases.items():
                self.assertRefused(self.zip_with(replace={name: data}), f'{name}: {needle}')

        def test_hidden_content_in_text_refused(self):
            css = b'.a{background:url(data:image/png;base64,iVBORw0KGgo=)}'
            self.assertRefused(self.zip_with(replace={'engine/web/panel.css': css}), 'data: URI')
            self.assertRefused(self.zip_with(replace={'engine/web/panel.js': b'var a="' + b'QUJD' * 600 + b'";'}), 'long base64')
            self.assertRefused(self.zip_with(replace={'README.md': b'INSERT INTO characters VALUES (1);'}), 'SQL statements')
            self.assertRefused(self.zip_with(replace={f'{PRODUCT}/P11.ClientEngine.deps.json': pixels}), 'pixels array')

        def test_forbidden_names_refused(self):
            for name, needle in (('engine/build/client-product-dev/P11.ClientEngine.pdb', 'forbidden extension .pdb'),
                                 ('engine/assets/integrity.json', 'forbidden directory segment "assets"'),
                                 ('Sprite/3225-0.spr', 'forbidden directory segment "Sprite"'),
                                 ('engine/web/icons/a.txt', 'forbidden directory segment "icons"'),
                                 ('docs/dump.sql', 'forbidden extension .sql'), ('engine/l1j.jar', 'forbidden extension .jar'),
                                 ('TW1608262200.bin', 'forbidden extension .bin'), ('TW.idb', 'forbidden extension .idb'),
                                 ('engine/source/Program.cs', 'forbidden extension .cs'), ('map/4/7fff8000.s32', 'forbidden extension .s32'),
                                 ('Text.pak', 'forbidden extension .pak'), ('vendor/x.txt', 'forbidden directory segment "vendor"'),
                                 ('../evil.txt', 'unsafe zip path')):
                self.assertRefused(self.zip_with(extra={name: b'x'}), name, needle)

        def test_junction_refused(self):
            web = self.root / 'engine/web'
            real = self.tmp / 'elsewhere-web'
            web.rename(real)
            kind = make_link(web, real)
            entries, _, errors = collect(self.root)
            self.assertRefused(errors, 'engine/web/index.html: path goes through a junction/symlink (engine/web)')
            result = self.package()
            self.assertEqual(result['status'], 'refused', kind)
            self.assertIsNone(result['zip'])

        def test_file_symlink_refused_when_supported(self):
            target = self.root / 'engine/web/index.html'
            moved = self.tmp / 'index.html'
            target.rename(moved)
            try:
                os.symlink(moved, target)
            except OSError:
                self.skipTest('file symlinks need privileges on this host; directory junction case covers Windows')
            self.assertRefused(collect(self.root)[2], 'engine/web/index.html: path goes through a junction/symlink')

        def test_embedded_pixels_json_refused(self):
            dll = f'{PRODUCT}/P11.ClientEngine.dll'
            errors = self.zip_with(replace={dll: synth_pe([('P06.Resources.Icons.json', pixels)])})
            self.assertRefused(errors, f'{dll}: embedded resource P06.Resources.Icons.json: not on the embedded-resource allowlist',
                               'pixels array in resource payload')
            # Even the one allowed resource name may not carry pixels or images.
            audio = f'{PRODUCT}/P24.Audio.dll'
            self.assertRefused(self.zip_with(replace={audio: synth_pe([('P24.Audio.CombatSfxPins.json', pixels)])}), 'pixels array')
            self.assertRefused(self.zip_with(replace={audio: synth_pe([('P24.Audio.CombatSfxPins.json', b'{"a":"' + png + b'"}')])}),
                               'contains PNG signature')
            self.assertRefused(self.zip_with(replace={dll: synth_pe([('P06.Sprite.png', png)])}), 'PNG payload', 'forbidden extension .png')

        def test_third_party_resource_pinned(self):
            dll = f'{PRODUCT}/Microsoft.Web.WebView2.WinForms.dll'
            name = 'Microsoft.Web.WebView2.WinForms.Microsoft.Web.WebView2.WinForm.WebView2.bmp'
            self.assertRefused(self.zip_with(replace={dll: synth_pe([(name, b'BM' + b'\0' * 64)])}), 'pinned third-party resource SHA-256 mismatch')

        def test_build_output_must_match_manifest(self):
            (self.root / f'{PRODUCT}/P24.Audio.dll').write_bytes(synth_pe([]) + b'\0')
            self.assertRefused(collect(self.root)[2], 'differs from the build product manifest')

        def test_packaged_manifest_must_match_zip(self):
            self.assertRefused(self.zip_with(replace={'engine/web/panel.css': b'/* edited */\n'}),
                               'engine/web/panel.css: size/SHA-256 differs from the product manifest')

        def test_missing_allowlisted_file_refused(self):
            (self.root / 'engine/web/trade.js').unlink()
            self.assertRefused(collect(self.root)[2], 'engine/web/trade.js')

        def test_real_templates_match_program(self):
            self.assertEqual(template_errors(REPO), [])

        def test_template_check_catches_drift(self):
            for rel in TEMPLATES.values():
                target = self.root / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((REPO / rel).read_bytes())
            for pattern in MESSAGE_SOURCES:
                for p in REPO.glob(pattern):
                    dest = self.root / p.relative_to(REPO)
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    dest.write_bytes(p.read_bytes())
            self.assertEqual(template_errors(self.root), [])
            readme, cmd = self.root / TEMPLATES['README.md'], self.root / TEMPLATES['啟動引擎.cmd']
            original_readme, original_cmd = readme.read_bytes(), cmd.read_bytes()
            readme.write_bytes(original_readme + '\n| `這句話不在程式裡。` | x | y |\n'.encode('utf-8'))
            self.assertRefused(template_errors(self.root), 'quoted message not found in program sources: 這句話不在程式裡。')
            readme.write_bytes(original_readme)
            cmd.write_bytes(original_cmd + b'set "TW_CLIENT_ROOT=C:\\Other"\r\n')
            self.assertRefused(template_errors(self.root), 'expected exactly one set "TW_CLIENT_ROOT=..." line, found 2')
            cmd.write_bytes(original_cmd.replace(b'\r\n', b'\n'))
            self.assertRefused(template_errors(self.root), 'line endings must be CRLF')
            cmd.write_bytes(original_cmd + 'rem 中文\r\n'.encode('utf-8'))
            self.assertRefused(template_errors(self.root), 'must be ASCII')

        def test_real_policy_matches_design(self):
            self.assertEqual(len(WEB_FILES), 8)
            self.assertTrue(all(not name_errors(z) for z in expected_zip_paths()), [name_errors(z) for z in expected_zip_paths()])

    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Portable))
    return 0 if result.wasSuccessful() else 1


if __name__ == '__main__':
    sys.exit(main())
