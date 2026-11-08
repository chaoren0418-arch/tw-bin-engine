"""Read-only (#533): compare event names registered by TW16 XML-window constructors (Hex-Rays output)
with Mouse/KeyboardEvent names used in candidate UI XML files from other local clients.
Usage: t226_evcmp.py <ida_out_dir...> -- <xml_root_dir...>"""
import os, re, sys, hashlib
sys.stdout.reconfigure(encoding="utf-8")
a = sys.argv[1:]
sep = a.index("--")
ida_dirs, xml_dirs = a[:sep], a[sep + 1:]
# class -> (ctor file, xml base names)
MAP = {
    "RenewalInventoryUI": ("009A40D0", ["ReInventory"]),
    "SpellUI": ("00E2E5A0", ["SpellList"]),
    "RenewalWareHouseUI": ("009CB850", ["rewarehouse"]),
    "PledgeUI": ("00B2A0C0", ["RePledgeUI", "PledgeUI", "PledgeUI2"]),
    "RenewalMailUI": ("009AEE70", ["RenewalMailUI"]),
    "FishingUI": ("00E65660", ["FishingUI"]),
    "ResetStatUI": ("009CF150", ["ResetStatUI"]),
    "UserObtainUI/FishObtainUI": ("00C03330", ["FishObtainUI", "UserObtainUI"]),
    "LoginUI": ("00EE8260", ["LoginUI"]),
    "SelectUI": ("00EEBB10", ["SelectUI"]),
    "CreateUI": ("00EE2DF0", ["CreateUI"]),
    "CRenewalStatusUI": ("00EDCBE0", ["ReNStatusUIEx"]),
}
def ctor_events(fn):
    for d in ida_dirs:
        p = os.path.join(d, fn + ".c")
        if os.path.exists(p):
            return set(re.findall(r'sub_B4F[0-9A-F]+\("([^"]+)"', open(p, encoding="utf-8").read()))
    return None
for cls, (fn, bases) in MAP.items():
    ev = ctor_events(fn)
    print(f"## {cls} ctor {fn}: {len(ev) if ev is not None else 'n/a'} events")
    for xd in xml_dirs:
        for b in bases:
            for suf in ("", "-c"):
                p = os.path.join(xd, b + suf + ".xml")
                if not os.path.exists(p):
                    continue
                raw = open(p, "rb").read()
                t = raw.decode("utf-8", "replace")
                xe = set(re.findall(r'(?:Mouse|Keyboard)Event\s*=\s*"([^"]+)"', t)) - {"Dummy"}
                root = re.search(r"<([A-Za-z_]+)[\s>]", t[t.find("?>") + 2 if "?>" in t else 0:])
                hit = len(ev & xe) if ev else 0
                miss = sorted(ev - xe) if ev else []
                print(f"  {os.path.basename(xd)}/{b}{suf}.xml root={root.group(1) if root else '?'} sha={hashlib.sha256(raw).hexdigest()[:12]} "
                      f"xmlEvents={len(xe)} ctorHit={hit}/{len(ev) if ev else 0} ctorMissing={miss[:8]}")
