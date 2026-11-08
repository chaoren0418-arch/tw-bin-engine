"""Read-only (#533): summarise a UI XML layout: each Window (name, X/Y, Width/Height, Base/FrameSurf, Activate)
and its direct-ish controls with position / surf / event. Usage: t226_xmlsum.py file.xml [max_controls]"""
import re, sys
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding="utf-8")
p = sys.argv[1]
lim = int(sys.argv[2]) if len(sys.argv) > 2 else 40
raw = open(p, "rb").read().decode("utf-8", "replace")
raw = raw[raw.find("<", raw.find("?>") + 2 if "?>" in raw else 0):]
root = ET.fromstring(raw)
A = lambda e, *ks: " ".join(f"{k}={e.get(k)}" for k in ks if e.get(k) is not None)
print(f"root={root.tag} {A(root, 'Toggle')}")
for w in root.iter("Window"):
    print(f"Window {w.get('Name')} {A(w, 'X', 'Y', 'Width', 'Height', 'BaseSurf', 'FrameSurf', 'Moveable', 'Activate', 'Popup', 'KeyboardEvent')}")
    n = 0
    for c in w.iter():
        if c is w or c.tag in ("Tooltip",):
            continue
        ev = c.get("MouseEvent") or c.get("KeyboardEvent")
        if c.tag in ("Button", "CheckButton", "Edit", "MultiEdit", "List", "ScrollBar", "ItemIcon", "Control", "ComboBox", "Tab", "Label", "Image", "Window", "Grid", "ItemIconGrid") and (ev or c.tag in ("Edit", "MultiEdit", "List", "Control")):
            n += 1
            if n <= lim:
                print(f"   {c.tag} {c.get('Name')} {A(c, 'X', 'Y', 'Width', 'Height', 'BaseSurf', 'Surf', 'LimitText', 'Row', 'Column', 'Caption', 'String')} ev={ev}")
    if n > lim:
        print(f"   ... {n - lim} more")
