"""Cross-sample comparison of run_rea.py output. Usage: py -3 -B rea_compare.py <out-dir> (imports run_rea.py from this directory, so not -I)"""
import json
import re
import sys
from pathlib import Path

NAMES = ["tw16", "tw13u", "lito_oep", "lin", "linr"]
PATHS = {}


def norm(out, name, command):
    data = json.loads((out / f"{name}.{command}.json").read_bytes())
    PATHS[name] = data.get("subject", {}).get("local_path", PATHS.get(name))
    return data.get("normalized_result") or {}


def key(part):
    return part.get("id") if part.get("kind") == "id" else part.get("name") or part.get("value")


def resources(out, name):
    table = {}
    for row in norm(out, name, "inspect-pe-resources").get("resources", []):
        payload = row.get("payload") or {}
        table[(key(row["type"]), key(row["name"]), key(row["language"]))] = payload
    return table


def version_text(path, payload):
    location = payload.get("location") or {}
    offset, size = location.get("offset"), location.get("bytes")
    if offset is None or not size:
        return None
    with open(path, "rb") as handle:
        handle.seek(offset)
        raw = handle.read(size)
    words = re.findall(r"[ -~一-鿿]{3,}", raw.decode("utf-16-le", "ignore"))
    return [w for w in words if w not in ("VS_VERSION_INFO", "StringFileInfo", "VarFileInfo", "Translation")]


def main():
    out = Path(sys.argv[1])
    tables = {name: resources(out, name) for name in NAMES}
    print("payload row sample", json.dumps(next(iter(tables["tw16"].values())), ensure_ascii=False)[:200])
    base = tables["tw16"]
    for name in NAMES[1:]:
        other = tables[name]
        shared = base.keys() & other.keys()
        same = [k for k in shared if base[k].get("sha256") and base[k].get("sha256") == other[k].get("sha256")]
        diff = sorted(str(k) for k in shared if k not in same)
        print(f"tw16 vs {name}: tw16={len(base)} other={len(other)} shared_keys={len(shared)} same_payload={len(same)} diff={diff[:12]}")
    print("-- lin only / tw16 only", sorted(map(str, tables["lin"].keys() - base.keys()))[:6],
          sorted(map(str, base.keys() - tables["lin"].keys()))[:6])
    for name in NAMES:
        for k, payload in tables[name].items():
            if k[0] == 16:
                print("version", name, k, version_text(_native(name), payload))
            if k[0] == 5:
                print("dialog", name, k, payload.get("bytes"), (payload.get("sha256") or "")[:12])
    print("-- signers (lin)")
    for entry in norm(out, "lin", "inspect-pe-certificates").get("entries", []):
        pkcs7 = entry.get("pkcs7") or {}
        print(" keys", list(pkcs7))
        for cert in (pkcs7.get("certificates") or [])[:6]:
            print("  ", {k: str(cert.get(k))[:90] for k in ("subject", "issuer", "not_before", "not_after") if k in cert})
    print("-- Miles quick API per sample")
    for name in NAMES:
        rows = norm(out, name, "inspect-pe-imports").get("imports", [])
        found = {}
        for row in rows:
            if "mss" in row["name"].lower():
                for fn in row.get("functions", []):
                    if fn.get("name"):
                        found.setdefault(row["name"].lower(), set()).add(fn["name"])
        print(name, {k: len(v) for k, v in found.items()},
              "quick_play" if any("_AIL_quick_play@8" in v for v in found.values()) else "no quick_play in table")


def _native(name):
    from run_rea import SAMPLES
    return SAMPLES[name]


if __name__ == "__main__":
    main()
