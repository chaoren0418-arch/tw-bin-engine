"""Summarise run_rea.py JSON output. Usage: python -I rea_summary.py <out-dir>"""
import json
import sys
from collections import Counter
from pathlib import Path

ORDER = ["tw16", "tw13u", "lito_oep", "lito_packed", "lin", "linr"]


def load(out, name, command):
    path = out / f"{name}.{command}.json"
    if not path.exists():
        return None
    data = json.loads(path.read_bytes())
    return data.get("normalized_result") or {"error": data.get("details", data).get("issues", data.get("message"))}


def imports(data):
    print(" imports", data.get("coverage"), "imphash", (data.get("imphash") or {}).get("md5"))
    print("  issues", str(data.get("issues"))[:200])
    rows = data.get("imports", [])
    if rows:
        print("  row keys", list(rows[0]))
    for row in rows:
        dll = row.get("dll") or row.get("name")
        funcs = row.get("functions") or row.get("entries") or []
        names = [str(f.get("name") or f.get("function") or f"#{f.get('ordinal')}") for f in funcs]
        tag = ""
        if any(k in str(dll).lower() for k in ("mss", "ail", "binkw", "ws2", "wsock", "ddraw", "d3d", "dsound", "dinput")):
            tag = " ** " + ",".join(names[:40])
        print(f"  {dll}: {len(names)}{tag}")


def resources(data):
    if "error" in data:
        print(" resources ERROR", data["error"])
        return
    rows = data.get("resources", [])
    print(" resources", data.get("coverage"))
    if rows:
        print("  row keys", list(rows[0]), json.dumps(rows[0], ensure_ascii=False)[:300])
    kinds = Counter()
    size = Counter()
    for row in rows:
        kind = str(row.get("type") if not isinstance(row.get("type"), dict) else
                   row["type"].get("name") or row["type"].get("id"))
        kinds[kind] += 1
        size[kind] += int(row.get("size") or row.get("data_size") or 0)
    print("  kinds", [(k, n, size[k]) for k, n in kinds.most_common(20)])


def main():
    out = Path(sys.argv[1])
    for name in ORDER:
        print("==", name)
        data = load(out, name, "inspect-pe-imports")
        if data:
            imports(data)
        data = load(out, name, "inspect-pe-resources")
        if data:
            resources(data)
        data = load(out, name, "inspect-pe-load-config")
        if data:
            tls = data.get("tls")
            print(" tls", json.dumps(tls, ensure_ascii=False)[:260] if tls else None,
                  "examined", data.get("examined_callbacks"))
        data = load(out, name, "inspect-pe-certificates")
        if data:
            print(" cert", data.get("coverage") or data.get("error"))
            for entry in data.get("entries", [])[:2]:
                text = json.dumps(entry, ensure_ascii=False)
                print("   ", text[:420])
        data = load(out, name, "inspect-native-exports")
        if data:
            print(" exports", data.get("coverage"), [e.get("name") for e in data.get("exports", [])][:12])
        data = load(out, name, "inspect-binary-hardening")
        if data:
            for image in data.get("images", [])[:1]:
                print(" hardening", json.dumps(image, ensure_ascii=False)[:520])
        data = load(out, name, "inspect-byte-entropy")
        if data:
            runs = data.get("runs", [])
            print(" entropy", data.get("file_entropy"), json.dumps(runs, ensure_ascii=False)[:360])


if __name__ == "__main__":
    main()
