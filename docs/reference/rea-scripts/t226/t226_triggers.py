"""Read-only (#533): map functions that reference a window global (ui_ida_query report.json 'xrefs') to the TW16
receive handlers listed in gap-matrix.md section 1/2 (`| op | S_NAME | `sub_X`／FO ...`).
Usage: t226_triggers.py gap-matrix.md report.json [report.json ...]"""
import json, re, sys
sys.stdout.reconfigure(encoding="utf-8")
gm, reps = sys.argv[1], sys.argv[2:]
H = {}
for l in open(gm, encoding="utf-8"):
    m = re.match(r"\|\s*(\d+)\s*\|\s*(S_[A-Z0-9_]+)\s*\|\s*`sub_([0-9A-F]+)`", l)
    if m:
        H[int(m.group(3), 16)] = f"op{m.group(1)} {m.group(2)}"
H[0x9357E0] = H.get(0x9357E0, "S_EVENT(sub_9357E0)")
NAMES = {"0x1540824": "BookmarkMemoryWindowUI", "0x15433B4": "StatInfoUI", "0x154085C": "UserObtainUI",
         "0x1542958": "QuickMotionUI", "0x13D43E4": "OptionUI", "0x13DC558": "PopUpUI", "0x13DC554": "PopupMsgUI",
         "0x1541D50": "LinHelperUI", "0x13CD1A4": "FightPlaceUI", "0x1540850": "SmithUI", "0x15401FC": "StoryBookUI",
         "0x13AA8D8": "QuestUI", "0x15442A8": "SiegeUI", "0x16568A4": "ZoneNameNotice", "0x13AAD9C": "PetSummonUI",
         "0x1542870": "FishingUI", "0x14EBF4C": "ResetStatUI", "0x14EBED8": "RenewalMailUI", "0x13AA1E8": "AgitAuctionUI",
         "0x153D218": "WareHistory(sub_A559D0)", "0x1540200": "StoryPaperUI", "0x13AA8D4": "QuestProgressUI",
         "0x1543334": "PKWindow", "0x153F25C": "CCookWindow", "0x153CD88": "TelTablesWindow", "0x13DC544": "PledgeNoticeUI",
         "0x153FCC8": "PledgeUI", "0x1540854": "PolymorphUI", "0x153C790": "SpecialPolymorphUI"}
seen = {}
for r in reps:
    for g, xs in json.load(open(r, encoding="utf-8")).get("xrefs", {}).items():
        fs = sorted({int(x["func"], 16) for x in xs if x.get("func")})
        seen.setdefault(g.upper().replace("0X", "0x"), set()).update(fs)
for g, fs in seen.items():
    trig = [f"{H[f]}" for f in fs if f in H]
    print(f"{NAMES.get(g, g)}\t{g}\thandlers={trig if trig else '-'}\tfuncs={len(fs)}")
