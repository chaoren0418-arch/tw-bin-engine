# T402 (#917) Q8-3: count R9-F unhandled HyperText tags in extracted Text pak HTML (read-only, input = PakViewer extract-all dirs).
# Usage: py -3 -I html_tag_census.py <name>=<dir> [<name>=<dir> ...]  -> TSV on stdout, example files per tag.
import re, sys
from pathlib import Path

TAGS = {
    "subHtml": re.compile(rb"<\s*subhtml\b", re.I),
    "time": re.compile(rb"<\s*time\b", re.I),
    "date": re.compile(rb"<\s*date\b", re.I),
    "may": re.compile(rb"<\s*may\b", re.I),
    "img pN": re.compile(rb"<\s*img\b[^>]*\bsrc\s*=\s*\"?p\d+", re.I),
    "var>=1000": re.compile(rb"<\s*var\b[^>]*\bsrc\s*=\s*\"?#?(\d{4,})", re.I),
}

def main():
    print("client\thtml_files\ttag\tfiles\toccurrences\texamples")
    for arg in sys.argv[1:]:
        name, d = arg.split("=", 1)
        files = sorted(p for p in Path(d).iterdir() if p.is_file() and p.suffix.lower() in (".html", ".htm"))
        hits = {k: [0, 0, []] for k in TAGS}
        for p in files:
            b = p.read_bytes()
            for k, rx in TAGS.items():
                n = len(rx.findall(b))
                if n:
                    h = hits[k]; h[0] += 1; h[1] += n
                    if len(h[2]) < 6:
                        h[2].append(p.name)
        for k, (nf, no, ex) in hits.items():
            print(f"{name}\t{len(files)}\t{k}\t{nf}\t{no}\t{','.join(ex)}")

if __name__ == "__main__":
    main()
