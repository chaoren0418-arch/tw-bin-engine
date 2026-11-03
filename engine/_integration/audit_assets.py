from common import *
from datetime import datetime,timezone
old=read(BASE/'assets/integrity.json')['files'];new=read(OUT/'assets/integrity.json')['files'];assert len(old)==13954 and len(new)==13957 and new[:len(old)]==old
actual={p.relative_to(OUT/'assets').as_posix() for p in (OUT/'assets').rglob('*') if p.is_file()};assert actual=={'integrity.json'}|{row['path'] for row in new}
total=0
for row in new:
 p=OUT/'assets'/row['path'];assert p.stat().st_size==row['size'] and sha(p)==row['sha256'],row['path'];total+=row['size']
save(A/'full-assets-verification.json',dict(status='pass',hashedPins=len(new),totalFiles=len(actual),totalPinnedBytes=total,integritySha256=sha(OUT/'assets/integrity.json'),baselinePins=len(old),unchangedPrefix=True,added=read(A/'integrity-update.json')['added'],fullBodyPasses=1,utc=datetime.now(timezone.utc).isoformat()))
print('PASS one full SHA pass, 13957 asset bodies; original 13954 pins unchanged',flush=True)
