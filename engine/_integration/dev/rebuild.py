"""dev canonical source lock, build and verification.

Modes
  (default)   verify that every pinned input file (source/audio/web/build inputs) matches rebuild-lock.json
  --update-lock    rescan inputs, write a deterministic dev lock, then verify it
  --build     build the Windows product + launcher once (.NET SDK from engine/global.json).
              PRs do not modify rebuild-lock.json (#646): when the committed lock is stale, --build
              warns and builds against a temporary in-memory lock scanned from the current inputs
              (the committed lock is never written). Missing inputs / path escapes still refuse.
  --build --strict  old behaviour: refuse unless the committed lock matches (used on main)
  --verify    strict: verify pins and the existing build/client-product-dev output against receipt.json
  --lock-out PATH   also write a freshly scanned lock to PATH (not the committed lock); CI artifact
  --write-receipt   after a successful --build, record DLL / manifest identities into receipt.json

Lock policy (#646): the lock is regenerated with --update-lock only after merge to main
(by the orchestrator); main CI runs the strict check and uploads a candidate lock when stale.

Differences from r33/r34
  * No supervisor acceptance ceremony, no CANONICAL path check: the repo root is derived from this file.
  * Product DLL identity is not pre-pinned in the lock; it is recorded by --write-receipt from a real build
    (CI cross-builds on ubuntu-latest) and later builds are compared against that receipt.
  * Works on any OS for pin verification; non-Windows builds target win-x64 explicitly.
    Running the product still requires Windows (WinForms / WebView2); --build never runs it.
"""
from pathlib import Path
import argparse, hashlib, json, os, platform, subprocess, sys, datetime

A = Path(__file__).resolve().parent          # engine/_integration/dev
ROOT = A.parents[1]                          # engine
REPO = ROOT.parent                           # repo root
LOCK = A / 'rebuild-lock.json'
RECEIPT = A / 'receipt.json'
PRODUCT = 'build/client-product-dev'
MANIFEST = 'build/product-dev-manifest.json'

# Same input categories as the frozen r35 lock (181 source files + 9 build inputs).
# Generated files must not enter the lock after a local build.
SOURCE_ROOTS = ('source', 'audio', 'web')
SOURCE_SUFFIXES = {'.cs', '.csproj', '.json', '.html', '.js', '.css', '.png'}
EXCLUDED_DIRS = {'bin', 'obj', 'build', 'tmp', '.git', '.vs', '__pycache__',
                 '.dotnet', '.nuget', 'vendor'}
BUILD_INPUTS = ('global.json', 'NuGet.Config', '啟動新版客戶端.cmd',
                'build-product.ps1', 'launch-product.ps1',
                '_integration/dev/product-build.ps1', '_integration/dev/pe_resource.py')


def require(ok, msg):
    if not ok:
        raise RuntimeError(msg)


def sha(p: Path) -> str:
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def read(p: Path):
    return json.loads(p.read_text('utf-8-sig'))


def resolve(rel: str) -> Path:
    require(not Path(rel).is_absolute() and '..' not in Path(rel).parts, 'input path escapes root: ' + rel)
    # Lexical containment only. engine/assets is a junction to releases/r28; Path.resolve()
    # follows it out of ROOT and would refuse the legitimate pin of assets/integrity.json
    # (Issue #7: REFUSED: input path escapes root: assets/integrity.json).
    p = Path(os.path.normpath(ROOT / rel))
    require(p.is_relative_to(ROOT), 'input path escapes root: ' + rel)
    return p


def pin(p: Path, row):
    require(p.is_file(), 'missing pinned file: ' + str(p))
    require(p.stat().st_size == row['size'] and sha(p) == row['sha256'], 'pin drift: ' + str(p))


def check_root():
    require(ROOT.name == 'engine' and (REPO / 'project-manifest.json').is_file(), 'run inside <repo>/engine/_integration/dev')


def scan_tree(base: Path, suffixes):
    require(base.is_dir(), 'missing input directory: ' + str(base))
    files = []
    for directory, dirs, names in os.walk(base):
        dirs[:] = sorted(d for d in dirs if d.lower() not in EXCLUDED_DIRS)
        for name in sorted(names):
            p = Path(directory) / name
            if p.suffix.lower() in suffixes:
                files.append(p.relative_to(ROOT).as_posix())
    return files


def input_paths():
    paths = list(BUILD_INPUTS)
    for name in SOURCE_ROOTS:
        paths.extend(scan_tree(ROOT / name, SOURCE_SUFFIXES))
    paths.extend(scan_tree(A / 'launcher-source', {'.cs', '.csproj'}))
    # These two launcher inputs are required even when regenerating a lock.
    paths.extend(('_integration/dev/launcher-source/Launcher.csproj',
                  '_integration/dev/launcher-source/Program.cs'))
    return sorted(set(paths))


def scan_lock():
    """Scan current inputs into a lock dict (same content --update-lock writes)."""
    check_root()
    files = []
    for rel in input_paths():
        p = resolve(rel)
        require(p.is_file(), 'missing input file: ' + str(p))
        files.append(dict(path=rel, size=p.stat().st_size, sha256=sha(p)))
    return dict(schema='dev-rebuild-lock-v1', revision='dev', basedOn='r35',
                sourceCount=sum(r['path'].split('/')[0] in SOURCE_ROOTS for r in files), files=files)


def write_lock(lock, path: Path):
    require(not path.is_symlink() and (not path.exists() or path.stat().st_nlink == 1),
            'refusing to overwrite a shared lock: ' + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(lock, ensure_ascii=False, indent=1) + '\n', encoding='utf-8', newline='\n')


def update_lock():
    write_lock(scan_lock(), LOCK)


def lock_drift(old_files, new_files):
    old = {r['path']: r for r in old_files}
    new = {r['path']: r for r in new_files}
    return dict(added=sorted(new.keys() - old.keys()), removed=sorted(old.keys() - new.keys()),
                changed=sorted(p for p in new.keys() & old.keys()
                               if (new[p]['size'], new[p]['sha256']) != (old[p]['size'], old[p]['sha256'])))


def validate_or_temporary():
    """--build without --strict: PRs do not touch rebuild-lock.json (#646).

    If the committed lock is stale, build against an in-memory lock scanned from the
    current inputs instead. The committed lock is never written here; missing required
    inputs or path escapes still refuse (scan_lock raises).
    """
    try:
        return validate(), None
    except (RuntimeError, OSError, ValueError, KeyError, TypeError) as ex:  # stale, missing or corrupt lock
        reason = f'{type(ex).__name__}: {ex}'
    lock = scan_lock()
    try:
        old = [r for r in read(LOCK)['files'] if {'path', 'size', 'sha256'} <= r.keys()]
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        old = []
    drift = dict(reason=reason, **lock_drift(old, lock['files']))
    print('WARN: rebuild-lock.json is stale for these inputs; building with a temporary lock (not written). '
          'The lock is regenerated after merge to main (#646). ' + json.dumps(drift, ensure_ascii=False),
          file=sys.stderr, flush=True)
    return lock, drift


def validate():
    check_root()
    lock = read(LOCK)
    require(lock['schema'] == 'dev-rebuild-lock-v1' and lock['revision'] == 'dev', 'unexpected lock schema/revision')
    expected = input_paths()
    actual = [row['path'] for row in lock['files']]
    require(actual == expected, 'input inventory drift; run --update-lock after reviewing source additions/removals')
    for row in lock['files']:
        pin(resolve(row['path']), row)
    src = [r for r in lock['files'] if r['path'].split('/')[0] in SOURCE_ROOTS]
    require(len(src) == lock['sourceCount'], f"sourceCount {lock['sourceCount']} != pinned source files {len(src)}")
    return lock


def run(name, argv, output: Path, env=None, cwd=None):
    p = subprocess.run(argv, cwd=cwd or ROOT, env=env, capture_output=True, timeout=1800)
    (output / (name + '.stdout')).write_bytes(p.stdout)
    (output / (name + '.stderr')).write_bytes(p.stderr)
    (output / (name + '.command.json')).write_text(json.dumps(dict(name=name, argv=[str(a) for a in argv], exitCode=p.returncode), ensure_ascii=False, indent=2), 'utf-8')
    require(p.returncode == 0, f'{name} failed (exit {p.returncode}); see {output}')
    print(name, p.returncode, flush=True)
    return p.stdout.decode('utf-8-sig', errors='replace')


def build_env(output: Path):
    env = dict(os.environ)
    for k, v in {'TEMP': 'tmp', 'TMP': 'tmp', 'DOTNET_CLI_HOME': '.dotnet', 'NUGET_SCRATCH': 'tmp/nuget'}.items():
        q = output / v
        q.mkdir(parents=True, exist_ok=True)
        env[k] = str(q)
    # #999: a fresh DOTNET_CLI_HOME makes dotnet's first run append <home>\.dotnet\tools to the
    # user PATH (HKCU\Environment); one entry per worktree overflowed PATH on 2026-11-11.
    env.update(DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false', DOTNET_NOLOGO='1',
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_GENERATE_ASPNET_CERTIFICATE='false', DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1')
    # NUGET_PACKAGES: honour the user's (D:\dev-cache\nuget\packages) if set, else engine/.nuget
    env.setdefault('NUGET_PACKAGES', str(ROOT / '.nuget'))
    return env


def identities():
    dll = ROOT / PRODUCT / 'P11.ClientEngine.dll'
    audio = ROOT / PRODUCT / 'P24.Audio.dll'
    manifest = ROOT / MANIFEST
    launcher = ROOT / 'launcher' / 'P24.Launcher.dll'
    for p in (dll, audio, manifest):
        require(p.is_file(), 'missing build output: ' + str(p))
    return dict(productDllSha256=sha(dll), audioDllSha256=sha(audio), productManifestSha256=sha(manifest),
                launcherDllSha256=sha(launcher) if launcher.is_file() else None)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument('--build', action='store_true')
    mode.add_argument('--verify', action='store_true')
    mode.add_argument('--update-lock', action='store_true')
    ap.add_argument('--strict', action='store_true',
                    help='with --build: refuse when rebuild-lock.json is stale (main / pre-#646 behaviour)')
    ap.add_argument('--lock-out', type=Path,
                    help='also write a freshly scanned lock to this path (never the committed lock); CI uploads it as an artifact')
    ap.add_argument('--write-receipt', action='store_true')
    ap.add_argument('--configfile', default=str(ROOT / 'NuGet.Config'), help='NuGet config for restore (CI passes .github/nuget.ci.config)')
    ap.add_argument('--output', type=Path)
    args = ap.parse_args()
    args.configfile = str(Path(args.configfile).resolve())  # subprocesses run with cwd=engine; resolve relative to caller cwd
    if args.strict and not args.build:
        ap.error('--strict only applies to --build (default and --verify are always strict)')
    if args.lock_out is not None:
        args.lock_out = args.lock_out.resolve()
        if args.update_lock or args.lock_out == LOCK.resolve():
            ap.error('--lock-out must not target the committed lock; use --update-lock for that')

    if args.update_lock:
        update_lock()
    drift = None
    if args.build and not args.strict:
        lock, drift = validate_or_temporary()
    else:
        lock = validate()
    if args.lock_out is not None:
        write_lock(lock if drift else scan_lock(), args.lock_out)
    print(json.dumps(dict(status='dev-inputs-verified' if drift is None else 'dev-inputs-temporary-lock',
                          sourceFiles=lock['sourceCount'], pinnedFiles=len(lock['files']), writes=args.update_lock,
                          lock='committed' if drift is None else 'temporary',
                          lockOut=str(args.lock_out) if args.lock_out else None)), flush=True)
    if not args.build and not args.verify:
        return

    output = (args.output or ROOT / '_integration/evidence' / ('dev-' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S'))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    env = build_env(output)

    if args.build:
        target_args = []
        if platform.system() != 'Windows':
            # EnableWindowsTargeting downloads Windows reference packs; the RID also
            # selects Windows .exe apphosts instead of native Linux/macOS executables.
            target_args = ['-p:EnableWindowsTargeting=true', '-r', 'win-x64', '-p:SelfContained=false']
            print('Cross-building Windows target win-x64 (framework-dependent)', flush=True)
            print('SKIP: Windows apphost execution and WinForms/WebView2 runtime checks require Windows; build verification only.', flush=True)
        sdk = run('sdk', ['dotnet', '--version'], output, env).strip()
        want = read(ROOT / 'global.json')['sdk']['version']
        require(sdk == want, f'wrong SDK {sdk}, global.json pins {want}')
        run('product-restore', ['dotnet', 'restore', str(ROOT / 'source/OfflineClient.csproj'), '--configfile', args.configfile] + target_args, output, env)
        run('product-build', ['dotnet', 'build', str(ROOT / 'source/OfflineClient.csproj'), '-c', 'Release', '--no-restore', '-o', str(ROOT / PRODUCT),
                              '--nologo', '-p:NuGetAudit=false', '-p:UseSharedCompilation=false'] + target_args, output, env)
        # product manifest: every runtime file + web + assets/integrity.json with size/sha256
        files = []
        roots = [ROOT / PRODUCT, ROOT / 'web']
        for base in roots:
            for p in sorted(base.rglob('*')):
                if p.is_file():
                    files.append(dict(path=p.relative_to(ROOT).as_posix(), size=p.stat().st_size, sha256=sha(p)))
        integ = ROOT / 'assets' / 'integrity.json'
        if integ.is_file():
            files.append(dict(path='assets/integrity.json', size=integ.stat().st_size, sha256=sha(integ)))
        manifest = dict(schema='p24-normal-product-v1', entry=PRODUCT + '/P11.ClientEngine.exe', arguments=['--normal-product'],
                        profile='p24-real-jar-d287-client2', port=42061, autoLogin=False, files=files)
        (ROOT / MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf-8')
        run('launcher-restore', ['dotnet', 'restore', str(A / 'launcher-source/Launcher.csproj'), '--configfile', args.configfile] + target_args, output, env)
        run('launcher-build', ['dotnet', 'build', str(A / 'launcher-source/Launcher.csproj'), '-c', 'Release', '--no-restore', '--nologo',
                               '-p:NuGetAudit=false', '-p:UseSharedCompilation=false', '-o', str(ROOT / 'launcher')] + target_args, output, env)
        if target_args:
            for exe in (ROOT / PRODUCT / 'P11.ClientEngine.exe', ROOT / 'launcher' / 'P24.Launcher.exe'):
                require(exe.is_file(), 'missing Windows apphost: ' + str(exe))
            print('Windows target win-x64 build succeeded (product + launcher; not executed)', flush=True)

    ids = identities()
    if args.write_receipt:
        RECEIPT.write_text(json.dumps(dict(schema='dev-receipt-v1', builtAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                           sdk=read(ROOT / 'global.json')['sdk']['version'], **ids), indent=2), 'utf-8')
        print('receipt written', RECEIPT)
    elif drift is not None:
        print('SKIP: receipt comparison (inputs differ from the committed lock, so the DLL is expected to differ)', flush=True)
    elif RECEIPT.is_file():
        rec = read(RECEIPT)
        for k in ('productDllSha256', 'audioDllSha256'):
            require(ids[k] == rec[k], f'{k} differs from receipt (deterministic build drift or source changed without re-lock)')
    for row in read(ROOT / MANIFEST)['files']:
        pin(resolve(row['path']), row)
    result = dict(status='pass-dev-build' if args.build else 'pass-dev-verify', sourceFiles=lock['sourceCount'],
                  lock='committed' if drift is None else 'temporary', lockDrift=drift,
                  GUI=False, network=False, database=False, **ids)
    (output / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print('PASS dev', json.dumps(ids), flush=True)


if __name__ == '__main__':
    try:
        main()
    except Exception as ex:
        print('REFUSED: ' + str(ex), file=sys.stderr)
        sys.exit(2)
