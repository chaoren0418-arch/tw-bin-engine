import idc, ida_bytes
CL = q["classes"]
res = {}
def dw(ea): return ida_bytes.get_dword(ea)
for name in CL:
    hits = find_all(name.encode("latin1"), 3)
    info = {"td": [], "vt": []}
    for h in hits:
        td = h - 8
        info["td"].append(hex(td))
        for x in idautils.XrefsTo(td):
            col = x.frm - 12  # COL.pTypeDescriptor at +12
            if dw(col) != 0: continue
            for y in idautils.XrefsTo(col):
                vt = y.frm + 4
                funcs = []
                ea = vt
                while True:
                    t = dw(ea)
                    f = ida_funcs.get_func(t)
                    if not f or f.start_ea != t: break
                    funcs.append(hex(t)); ea += 4
                    if len(funcs) > 80: break
                users = sorted({hex(fstart(z.frm) or 0) for z in idautils.XrefsTo(vt)})
                info["vt"].append({"col": hex(col), "vt": hex(vt), "funcs": funcs, "users": users})
    res[name] = info
    for v in info["vt"]:
        for u in v["users"][:6]:
            if u != "0x0" and u not in rep["functions"]:
                rep["functions"][u] = decomp(int(u, 16))
        for fn in v["funcs"][:q.get("vt_decomp", 0)]:
            if fn not in rep["functions"]:
                rep["functions"][fn] = decomp(int(fn, 16))
rep["classes"] = res
