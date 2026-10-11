"""Check visible first-frame colors; canonical palette accuracy needs visual review."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent
PACKAGE = ROOT / 'catalog-package'
catalog = json.loads((PACKAGE / 'catalog.json').read_text('utf-8'))
def colors(path):
    with Image.open(path) as im:
        im.seek(0)
        rgba = im.convert('RGBA')
        return {pixel[:3] for pixel in rgba.getdata() if pixel[3]}, rgba.size
assert colors(PACKAGE / 'assets/normal/133.gif')[0] != colors(PACKAGE / 'assets/shiny/133.gif')[0]
rows = []
for species in catalog['species']:
    assets = species['assets']
    if not all(assets[v]['animated'] for v in ('normal', 'shiny')):
        continue
    normal, shiny = [PACKAGE / assets[v]['path'] for v in ('normal', 'shiny')]
    nc, ns = colors(normal)
    sc, ss = colors(shiny)
    rows.append({'id': species['id'], 'name': species['species']['identifier'],
        'bytesDiffer': hashlib.sha256(normal.read_bytes()).digest() != hashlib.sha256(shiny.read_bytes()).digest(),
        'visibleFirstFrameColorsDiffer': nc != sc, 'sameDimensions': ns == ss})
assert len(rows) == 865
report = {'pairs': len(rows), 'differentFiles': sum(r['bytesDiffer'] for r in rows),
    'differentVisibleFirstFrameColors': sum(r['visibleFirstFrameColorsDiffer'] for r in rows),
    'review': [r for r in rows if not r['visibleFirstFrameColorsDiffer'] or not r['sameDimensions']],
    'limitation': 'Visible first-frame color sets compared, not canonical palette correctness or every animation frame.', 'rows': rows}
(ROOT / 'shiny-pair-audit.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: v for k, v in report.items() if k != 'rows'}))
