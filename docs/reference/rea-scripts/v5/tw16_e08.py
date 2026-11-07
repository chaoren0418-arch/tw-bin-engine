n=0
for fs in idautils.Functions(0xECC000, 0xEDC000):
    t = dec(fs); n+=1
P("decompiled", n)