"""#646: PRs do not modify rebuild-lock.json; --build tolerates a stale lock.

Covers the temporary-lock path of rebuild.py without running dotnet:
  * validate_or_temporary() falls back to an in-memory scan and never writes the lock
  * default mode, --verify and --build --strict stay strict
  * --lock-out writes a candidate lock byte-identical to --update-lock, never the committed one
Fixtures reuse test_rebuild_paths.UpdateLockTests (temporary repo under scratch/).
"""
from __future__ import annotations

import contextlib
import io
import json
import sys
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import rebuild  # noqa: E402
import test_rebuild_paths as fixture  # noqa: E402


class TemporaryLockTests(unittest.TestCase):
    setUp = fixture.UpdateLockTests.setUp
    cli = fixture.UpdateLockTests.cli

    @contextlib.contextmanager
    def patched(self):
        with mock.patch.multiple(rebuild, ROOT=self.engine, A=self.dev, REPO=self.repo,
                                 LOCK=self.dev / 'rebuild-lock.json'):
            yield

    def temporary(self):
        err = io.StringIO()
        with self.patched(), contextlib.redirect_stderr(err):
            lock, drift = rebuild.validate_or_temporary()
        return lock, drift, err.getvalue()

    def test_current_lock_is_used_as_is(self):
        self.cli('--update-lock')
        lock, drift, err = self.temporary()
        self.assertIsNone(drift)
        self.assertEqual(err, '')
        self.assertEqual(lock, json.loads(self.lock.read_bytes()))

    def test_stale_lock_builds_with_temporary_lock_and_is_not_written(self):
        self.cli('--update-lock')
        first = self.lock.read_bytes()
        (self.engine / 'source/Z.cs').write_bytes(b'changed\n')       # same size, new hash
        (self.engine / 'source/New.cs').write_bytes(b'new\n')         # inventory addition
        (self.engine / 'web/a.css').unlink()                          # inventory removal
        lock, drift, err = self.temporary()
        self.assertEqual(first, self.lock.read_bytes())
        self.assertIn('WARN: rebuild-lock.json is stale', err)
        self.assertEqual(drift['added'], ['source/New.cs'])
        self.assertEqual(drift['removed'], ['web/a.css'])
        self.assertEqual(drift['changed'], ['source/Z.cs'])
        self.assertIn('inventory drift', drift['reason'])
        with self.patched():
            self.assertEqual(lock, rebuild.scan_lock())
        self.assertEqual(lock['sourceCount'], 8)
        # The strict modes still refuse the same tree.
        self.assertIn('inventory drift', self.cli(code=2).stderr)
        self.assertIn('inventory drift', self.cli('--verify', code=2).stderr)
        self.assertIn('inventory drift', self.cli('--build', '--strict', code=2).stderr)
        self.assertEqual(first, self.lock.read_bytes())

    def test_missing_or_corrupt_lock_falls_back_to_scan(self):
        self.cli('--update-lock')
        self.lock.write_text('not json', encoding='utf-8')
        lock, drift, _ = self.temporary()
        self.assertEqual(len(drift['added']), len(lock['files']))
        self.assertEqual(self.lock.read_text(encoding='utf-8'), 'not json')
        self.lock.unlink()
        lock, drift, _ = self.temporary()
        self.assertEqual(len(drift['added']), len(lock['files']))
        self.assertFalse(self.lock.exists())

    def test_missing_required_input_still_refuses(self):
        self.cli('--update-lock')
        (self.engine / 'global.json').unlink()
        with self.patched(), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(RuntimeError) as ctx:
                rebuild.validate_or_temporary()
        self.assertIn('missing input file', str(ctx.exception))

    def test_lock_out_matches_update_lock_and_leaves_committed_lock(self):
        self.cli('--update-lock')
        reference = self.lock.read_bytes()
        out = self.repo / 'scratch' / 'candidate' / 'rebuild-lock.json'
        result = json.loads(self.cli('--lock-out', str(out)).stdout)
        self.assertEqual(result['status'], 'dev-inputs-verified')
        self.assertEqual(result['lock'], 'committed')
        self.assertEqual(out.read_bytes(), reference)
        (self.engine / 'source/Z.cs').write_bytes(b'changed\n')
        # Strict default mode refuses before writing a candidate.
        out.unlink()
        self.cli('--lock-out', str(out), code=2)
        self.assertFalse(out.exists())
        self.assertEqual(self.lock.read_bytes(), reference)
        self.cli('--update-lock')
        regenerated = self.lock.read_bytes()
        self.lock.write_bytes(reference)
        with self.patched(), contextlib.redirect_stderr(io.StringIO()):
            lock, drift = rebuild.validate_or_temporary()
            rebuild.write_lock(lock, out)
        self.assertIsNotNone(drift)
        self.assertEqual(out.read_bytes(), regenerated)
        self.assertEqual(self.lock.read_bytes(), reference)

    def test_argument_guards(self):
        self.cli('--update-lock')
        first = self.lock.read_bytes()
        self.assertIn('--strict only applies to --build', self.cli('--strict', code=2).stderr)
        self.assertIn('--strict only applies to --build', self.cli('--verify', '--strict', code=2).stderr)
        self.assertIn('must not target the committed lock',
                      self.cli('--lock-out', str(self.lock), code=2).stderr)
        self.assertIn('must not target the committed lock',
                      self.cli('--update-lock', '--lock-out', str(self.repo / 'x.json'), code=2).stderr)
        self.assertEqual(first, self.lock.read_bytes())


if __name__ == '__main__':
    unittest.main()
