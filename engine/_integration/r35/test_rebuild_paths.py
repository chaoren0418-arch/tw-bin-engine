"""Lexical container checks for r35 rebuild.resolve().

engine/assets is a junction/symlink to releases/r28 (outside engine/).
Path.resolve() follows that link and the old is_relative_to(ROOT.resolve())
check refused assets/integrity.json (Issue #7). These tests pin the lexical
behaviour: a symlink out of ROOT is allowed; ../ and absolute paths are not.
"""
from __future__ import annotations

import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import rebuild  # noqa: E402


class ResolvePathTests(unittest.TestCase):
    def test_assets_symlink_outside_root_is_allowed(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp_root = Path(tmp)
            engine = tmp_root / 'engine'
            engine.mkdir()
            outside = tmp_root / 'outside-assets'
            outside.mkdir()
            (outside / 'integrity.json').write_text('{}', encoding='utf-8')
            (engine / 'assets').symlink_to(outside, target_is_directory=True)

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


if __name__ == '__main__':
    unittest.main()
