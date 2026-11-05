"""Lexical container checks for dev rebuild.resolve().

engine/assets is a junction/symlink to releases/r28 (outside engine/).
Path.resolve() follows that link and the old is_relative_to(ROOT.resolve())
check refused assets/integrity.json (Issue #7). These tests pin the lexical
behaviour: a symlink out of ROOT is allowed; ../ and absolute paths are not.

Temporary files stay under repo scratch/ (gitignored), not system TEMP.
"""
from __future__ import annotations

import os
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent  # engine/_integration/dev
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import rebuild  # noqa: E402

REPO = HERE.parents[2]  # repo root
SCRATCH = REPO / 'scratch' / 'issue-31-rebuild-paths'


def _link_assets(engine: Path, outside: Path) -> None:
    dest = engine / 'assets'
    try:
        dest.symlink_to(outside, target_is_directory=True)
        return
    except OSError:
        if os.name != 'nt':
            raise
    completed = subprocess.run(
        ['cmd', '/c', 'mklink', '/J', str(dest), str(outside)],
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        raise OSError(completed.stderr or completed.stdout or 'mklink /J failed')


class ResolvePathTests(unittest.TestCase):
    def test_assets_symlink_outside_root_is_allowed(self):
        SCRATCH.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='assets-junction-', dir=str(SCRATCH)) as tmp:
            tmp_root = Path(tmp)
            engine = tmp_root / 'engine'
            engine.mkdir()
            outside = tmp_root / 'outside-assets'
            outside.mkdir()
            (outside / 'integrity.json').write_text('{}', encoding='utf-8')
            _link_assets(engine, outside)

            with mock.patch.object(rebuild, 'ROOT', engine):
                p = rebuild.resolve('assets/integrity.json')

            self.assertEqual(p, Path(os.path.normpath(engine / 'assets' / 'integrity.json')))
            self.assertTrue(p.is_relative_to(engine))
            resolved = p.resolve()
            self.assertTrue(resolved.is_relative_to(outside.resolve()))
            self.assertFalse(resolved.is_relative_to(engine.resolve()))

    def test_parent_traversal_rejected(self):
        with self.assertRaises(RuntimeError) as ctx:
            rebuild.resolve('../x')
        self.assertIn('input path escapes root: ../x', str(ctx.exception))

    def test_nested_parent_traversal_rejected(self):
        with self.assertRaises(RuntimeError) as ctx:
            rebuild.resolve('assets/../../secret')
        self.assertIn('input path escapes root:', str(ctx.exception))

    def test_absolute_path_rejected(self):
        abs_path = str(Path.cwd() / 'escaped.bin')
        self.assertTrue(Path(abs_path).is_absolute())
        with self.assertRaises(RuntimeError) as ctx:
            rebuild.resolve(abs_path)
        self.assertIn('input path escapes root: ' + abs_path, str(ctx.exception))


class UpdateLockTests(unittest.TestCase):
    def setUp(self):
        SCRATCH.mkdir(parents=True, exist_ok=True)
        self.tmp = tempfile.TemporaryDirectory(prefix='dev-lock-', dir=str(SCRATCH))
        self.addCleanup(self.tmp.cleanup)
        self.repo = Path(self.tmp.name)
        self.engine = self.repo / 'engine'
        self.dev = self.engine / '_integration' / 'dev'
        self.dev.mkdir(parents=True)
        (self.repo / 'project-manifest.json').write_text('{}', encoding='utf-8')
        for rel in ('rebuild.py', 'product-build.ps1', 'pe_resource.py',
                    'launcher-source/Launcher.csproj', 'launcher-source/Program.cs'):
            dest = self.dev / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(HERE / rel, dest)
        for rel in rebuild.BUILD_INPUTS:
            dest = self.engine / rel
            if not dest.exists():
                dest.write_text('{}\n', encoding='utf-8')
        # Deliberately create files out of lexical order, including nested resources.
        for rel in ('web/z.js', 'source/Resources/map.json', 'audio/A.cs',
                    'web/icons/a.png', 'source/Z.cs', 'source/App.csproj',
                    'web/a.css', 'web/index.html'):
            dest = self.engine / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(b'fixture\n')
        self.lock = self.dev / 'rebuild-lock.json'

    def cli(self, *args, code=0):
        result = subprocess.run([sys.executable, '-B', '-X', 'utf8',
                                 str(self.dev / 'rebuild.py'), *args],
                                cwd=self.repo, capture_output=True, text=True, encoding='utf-8')
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return result

    def test_deterministic_update_detects_edits_and_recovers(self):
        result = self.cli('--update-lock')
        self.assertEqual(json.loads(result.stdout)['status'], 'dev-inputs-verified')
        self.assertTrue(json.loads(result.stdout)['writes'])
        first = self.lock.read_bytes()
        self.cli('--update-lock')
        self.assertEqual(first, self.lock.read_bytes())
        self.assertNotIn(b'\r', first)
        self.assertTrue(first.endswith(b'\n'))
        lock = json.loads(first)
        self.assertEqual(lock['sourceCount'], 8)
        paths = [row['path'] for row in lock['files']]
        self.assertEqual(paths, sorted(paths))
        self.assertEqual(len(paths), 17)
        self.assertFalse(json.loads(self.cli().stdout)['writes'])
        # Same size, different hash: size checks alone cannot detect this drift.
        (self.engine / 'source/Z.cs').write_bytes(b'changed\n')
        self.assertIn('pin drift:', self.cli(code=2).stderr)
        self.assertEqual(first, self.lock.read_bytes())
        self.cli('--update-lock')
        self.assertNotEqual(first, self.lock.read_bytes())
        self.cli()

    def test_additions_deletions_and_generated_file_exclusions(self):
        self.cli('--update-lock')
        first = self.lock.read_bytes()
        for rel in ('source/obj/Debug/Generated.cs', 'audio/bin/output.json',
                    'web/build/cache.js', 'source/readme.md',
                    '_integration/dev/launcher-source/obj/Generated.cs'):
            dest = self.engine / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(b'ignored\n')
        self.cli()
        self.cli('--update-lock')
        self.assertEqual(first, self.lock.read_bytes())
        for rel in ('source/New.cs', '_integration/dev/launcher-source/Extra.cs'):
            with self.subTest(path=rel):
                dest = self.engine / rel
                dest.write_bytes(b'new\n')
                self.assertIn('inventory drift', self.cli(code=2).stderr)
                self.cli('--update-lock')
                self.cli()
                self.assertIn(rel, [r['path'] for r in json.loads(self.lock.read_bytes())['files']])
                dest.unlink()
                self.assertIn('inventory drift', self.cli(code=2).stderr)
                self.cli('--update-lock')
                self.cli()
                self.assertEqual(first, self.lock.read_bytes())

    def test_missing_required_input_preserves_lock(self):
        self.cli('--update-lock')
        first = self.lock.read_bytes()
        (self.engine / 'global.json').unlink()
        self.assertIn('missing input file', self.cli('--update-lock', code=2).stderr)
        self.assertEqual(first, self.lock.read_bytes())


if __name__ == '__main__':
    unittest.main()
