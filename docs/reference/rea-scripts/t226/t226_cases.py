"""Read-only (#533): split a Hex-Rays pseudocode file by top-level `case` labels of a given switch and list,
per case, the window globals / html ids / helper calls it touches. Usage: t226_cases.py file.c switch_line indent"""
import re, sys
sys.stdout.reconfigure(encoding="utf-8")
path, sw_line, indent = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
G = {
    "dword_153FCC8": "PledgeUI", "dword_13DC544": "PledgeNoticeUI", "dword_1540854": "PolymorphUI",
    "dword_153C790": "SpecialPolymorphUI", "dword_1542440": "SpellUI", "dword_14EBEC0": "RenewalInventoryUI",
    "dword_14EBF48": "RenewalWareHouseUI", "dword_14EBEEC": "PetWarehouse", "dword_14EBE4C": "AddWarehouse",
    "dword_153F25C": "CCookWindow", "dword_153CD88": "TelTablesWindow#1", "dword_153CD90": "TelTablesWindow#2",
    "dword_153CD98": "TelTablesWindow#3", "dword_13CD834": "HyperText", "dword_1542870": "FishingUI",
    "dword_1540860": "FishObtainUI", "dword_154085C": "UserObtainUI", "dword_14EBF4C": "ResetStatUI",
    "dword_14EBED8": "RenewalMailUI", "dword_13A96EC": "AgitWindow", "dword_13A96F0": "AgitMapWindow",
    "dword_13AA1E8": "AgitAuctionUI", "dword_1543334": "PKWindow", "dword_153F250": "InputWindow0",
    "dword_153F254": "InputWindow1", "dword_15442A8": "SiegeUI", "dword_13AA8D8": "QuestUI",
    "dword_13AA8D4": "QuestProgressUI", "dword_1540824": "BookmarkMemoryUI", "dword_15433B4": "StatInfoUI",
    "dword_15433B8": "RenewalStatusUI", "dword_1541D50": "LinHelperUI", "dword_13CD1A4": "FightPlaceUI",
    "dword_1540850": "SmithUI", "dword_15401FC": "StoryBookUI", "dword_1540200": "StoryPaperUI",
    "dword_16568A4": "ZoneNameNotice", "dword_13AAD9C": "PetSummonUI", "dword_13D43E4": "OptionUI",
    "dword_13DC558": "PopUpUI", "dword_13DC554": "PopupMsgUI", "dword_1542958": "QuickMotionUI",
    "dword_1540858": "SummonUI", "dword_13CDF64": "InventoryWindow", "dword_1541DF8": "SpellWindow",
}
lines = open(path, encoding="utf-8").read().splitlines()
pad = " " * indent
cur, out = None, {}
depth_end = None
for i, l in enumerate(lines[sw_line:], sw_line + 1):
    m = re.match(rf"^{pad}(case (0x[0-9A-Fa-f]+|\d+)u?|default):", l)
    if m:
        lab = m.group(2) or "default"
        cur = int(lab, 16) if lab.startswith("0x") else (int(lab) if lab != "default" else lab)
        out.setdefault(cur, set())
        continue
    if re.match(rf"^{' ' * (indent - 2)}\}}", l):
        break
    if cur is None:
        continue
    for g, n in G.items():
        if g in l:
            out[cur].add(n)
    for s in re.findall(r'"([A-Za-z_][A-Za-z0-9_ ]{2,30})"', l):
        if s not in ("s", "c", "d", "h", "sc", "cs", "dd"):
            out[cur].add('"' + s + '"')
groups, prev = [], None
for k in out:
    print(f"{k if isinstance(k, str) else f'{k} (0x{k:X})'}\t{' '.join(sorted(out[k]))}")
