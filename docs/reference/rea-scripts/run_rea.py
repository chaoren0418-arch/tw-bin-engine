"""Run REA's provider-free PE inspections against the #417 samples.

Usage: python -I run_rea.py <rea-copy-dir> <out-dir> [name=path ...]

REA must be built in a copy under scratch/, never in D:\\lineage開發\\tools\\rea-main.
Targets are only read. Every environment path REA or Node could write to is
redirected under <out-dir>/../env-node so nothing lands outside the project.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

L = r"D:\lineage開發"
SAMPLES = {
    "tw16": rf"{L}\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin",
    "tw13u": rf"{L}\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe",
    "lito_oep": rf"{L}\3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe",
    "lin": rf"{L}\登入器bin資源\Lin.bin",
    "linr": rf"{L}\登入器bin資源\LinR.bin拆解\LinR.bin",
    "lito_packed": rf"{L}\0-客戶端\LiTo.bin",
}
BIG = ["--max-file-bytes", str(512 * 1024 * 1024)]
COMMANDS = {
    "inspect-pe-imports": BIG + ["--max-functions", "65536", "--max-dlls", "1024"],
    "inspect-binary-hardening": ["--max-file-bytes", str(512 * 1024 * 1024)],
    "inspect-pe-certificates": BIG,
    "inspect-pe-resources": BIG + ["--max-entries", "65536"],
    "inspect-pe-load-config": BIG,
    "inspect-native-exports": BIG,
    "inspect-byte-entropy": ["--block-size", "65536"],
}


def main() -> int:
    copy, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
    samples = dict(SAMPLES)
    samples.update(a.split("=", 1) for a in sys.argv[3:])
    out.mkdir(parents=True, exist_ok=True)
    envd = out.parent / "env-node"
    env = dict(os.environ)
    for key, sub in {"TEMP": "tmp", "TMP": "tmp", "LOCALAPPDATA": "local",
                     "APPDATA": "roaming", "USERPROFILE": "home", "HOME": "home"}.items():
        (envd / sub).mkdir(parents=True, exist_ok=True)
        env[key] = str(envd / sub)
    tool = str(copy / "scripts" / "rea.mjs")
    for name, path in samples.items():
        if not Path(path).is_file():
            print(f"{name:9} MISSING {path}")
            continue
        for command, extra in COMMANDS.items():
            run = subprocess.run(["node", tool, command, path, *extra, "--format", "json"],
                                 capture_output=True, env=env, timeout=280)
            target = out / f"{name}.{command}.json"
            target.write_bytes(run.stdout or run.stderr)
            message = ""
            if run.returncode:
                try:
                    message = json.loads(run.stdout or run.stderr).get("message", "")[:120]
                except ValueError:
                    message = (run.stdout or run.stderr)[:120].decode("utf-8", "replace")
            print(f"{name:9} {command:26} rc={run.returncode} bytes={target.stat().st_size} {message}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
