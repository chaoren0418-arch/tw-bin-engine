import re, sys
d = open(sys.argv[1], "rb").read()
for m in re.finditer(rb"[ -~]{4,}", d):
    s = m.group().decode()
    if re.search(sys.argv[2], s):
        print(hex(m.start()), s)
