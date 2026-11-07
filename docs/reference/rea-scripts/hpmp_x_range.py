for a,b in q["ranges"]:
    a=int(a,16); b=int(b,16)
    for fs in idautils.Functions(a, b):
        h=hex(fs)
        if h not in rep["functions"]:
            rep["functions"][h]=decomp(fs)
