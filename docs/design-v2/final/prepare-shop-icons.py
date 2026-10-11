"""Technical atlas slicing and fitting only; preserves generated alpha and source art."""
import json
from pathlib import Path
from PIL import Image, PngImagePlugin

ROOT = Path(__file__).resolve().parent / 'assets/shop'
source = Image.open(ROOT / 'source-atlas.png').convert('RGBA')
assert source.getextrema()[3][0] == 0, 'Atlas must have genuine transparency'
ids = ['treats', 'care-booster', 'lucky-charm', 'evolution-stone',
       'offspring-charm', 'xp-potion', 'cheri', 'oran',
       'aspear', 'pecha', 'chesto', 'rawst']
prompt = (ROOT / 'prompt.txt').read_text(encoding='utf-8')
metadata = PngImagePlugin.PngInfo()
metadata.add_itxt('generation_prompt', prompt)
metadata.add_text('asset_operation', 'Technical crop, uniform fit, transparent padding; no repaint')
report = []
# Generated padding is not a mathematical grid; cuts follow the inspected empty gutters.
x_edges = [0, 402, 752, 1090, source.width]
y_edges = [0, 386, 723, source.height]
for i, item_id in enumerate(ids):
    col, row = i % 4, i // 4
    rect = (x_edges[col], y_edges[row], x_edges[col + 1], y_edges[row + 1])
    cell = source.crop(rect)
    # Use visible alpha to bound each object, then preserve the original antialiased edge.
    bbox = cell.getchannel('A').point(lambda a: 255 if a > 32 else 0).getbbox()
    assert bbox and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < cell.width and bbox[3] < cell.height, (item_id, bbox)
    bbox = (max(0, bbox[0] - 2), max(0, bbox[1] - 2), min(cell.width, bbox[2] + 2), min(cell.height, bbox[3] + 2))
    cropped = cell.crop(bbox)
    cropped.thumbnail((144, 144), Image.Resampling.LANCZOS)
    icon = Image.new('RGBA', (160, 160))
    icon.alpha_composite(cropped, ((160 - cropped.width) // 2, (160 - cropped.height) // 2))
    icon.save(ROOT / (item_id + '.png'), pnginfo=metadata)
    assert icon.getchannel('A').getbbox() and icon.getpixel((0, 0))[3] == 0
    report.append({'id': item_id, 'file': item_id + '.png', 'sourceCell': list(rect), 'sourceBounds': list(bbox)})
assert len(report) == 12 and len(set(ids)) == 12
preview = Image.new('RGBA', (640, 480), '#f8f4e9')
for i, item_id in enumerate(ids):
    preview.alpha_composite(Image.open(ROOT / (item_id + '.png')).convert('RGBA'), ((i % 4) * 160, (i // 4) * 160))
preview.save(ROOT / 'preview.png', pnginfo=metadata)
(ROOT / 'manifest.json').write_text(json.dumps({'sourceSize': list(source.size), 'iconSize': [160, 160], 'items': report}, indent=2) + '\n', encoding='utf-8')
print('12 transparent shop icons exported; no artwork repainted')
