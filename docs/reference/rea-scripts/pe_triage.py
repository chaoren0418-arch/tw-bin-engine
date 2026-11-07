"""Read-only PE triage: headers, sections, entropy, imports, protector markers."""
import hashlib, json, math, sys, collections, pathlib
import pefile

MARKERS = [b"Themida", b"WinLicense", b".themida", b".winlice", b"Oreans", b".vmp", b"UPX!", b"ASPack", b".boot", b".SCY"]


def entropy(b):
    if not b:
        return 0.0
    c = collections.Counter(b)
    n = len(b)
    return -sum(v / n * math.log2(v / n) for v in c.values())


def triage(path):
    data = pathlib.Path(path).read_bytes()
    pe = pefile.PE(data=data, fast_load=True)
    pe.parse_data_directories(directories=[
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_TLS"],
    ])
    oh = pe.OPTIONAL_HEADER
    ep = oh.AddressOfEntryPoint
    secs = []
    ep_sec = None
    for s in pe.sections:
        name = s.Name.rstrip(b"\0").decode("latin1")
        raw = data[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
        secs.append(dict(name=name, rva=hex(s.VirtualAddress), vsize=hex(s.Misc_VirtualSize),
                         raw=hex(s.SizeOfRawData), entropy=round(entropy(raw), 4),
                         chars=hex(s.Characteristics)))
        if s.VirtualAddress <= ep < s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData):
            ep_sec = name
    imports = {}
    for d in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        imports.setdefault(d.dll.decode("latin1").lower(), 0)
        imports[d.dll.decode("latin1").lower()] += len(d.imports)
    return dict(
        path=str(path), size=len(data), sha256=hashlib.sha256(data).hexdigest(),
        machine=hex(pe.FILE_HEADER.Machine), timestamp=pe.FILE_HEADER.TimeDateStamp,
        imagebase=hex(oh.ImageBase), entry_rva=hex(ep), entry_section=ep_sec,
        size_of_image=hex(oh.SizeOfImage), dllchar=hex(oh.DllCharacteristics),
        overlay=(len(data) - pe.get_overlay_data_start_offset()) if pe.get_overlay_data_start_offset() else 0,
        sections=secs, imports=imports, import_total=sum(imports.values()),
        whole_entropy=round(entropy(data), 4),
        markers={m.decode(): data.count(m) for m in MARKERS},
        lineage_strings={m.decode(): data.count(m) for m in
                         [b"Lineage", b"sprite", b".spr", b".pak", b".s32", b".seg", b"list.spr", b"sprite_frame", b".wav", b".idx", b"RunTime Expired"]},
    )


if __name__ == "__main__":
    out = [triage(p) for p in sys.argv[2:]]
    pathlib.Path(sys.argv[1]).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    for r in out:
        print(r["path"], r["size"], r["sha256"][:16], "ts", r["timestamp"], "ep", r["entry_rva"], r["entry_section"], "ent", r["whole_entropy"], "imports", r["import_total"], "overlay", r["overlay"])
        for s in r["sections"]:
            print("  ", s)
        print("  dlls", list(r["imports"].items())[:20])
        print("  markers", r["markers"])
        print("  strings", r["lineage_strings"])
