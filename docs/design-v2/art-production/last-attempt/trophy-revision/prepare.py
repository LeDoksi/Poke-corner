"""Technical crop, uniform fitting, metadata and review layout; no artistic repaint."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin

ROOT = Path(__file__).resolve().parent
jobs = json.loads((ROOT / 'jobs.json').read_text(encoding='utf-8'))
rows = []
for job in jobs:
    path = ROOT / 'sources' / (job['id'] + '.png')
    source = Image.open(path).convert('RGBA')
    assert source.getextrema()[3][0] == 0, job['id']
    source_metadata = PngImagePlugin.PngInfo()
    source_metadata.add_text('impeccable:prompt', job['prompt'])
    source_metadata.add_text('impeccable:prompt-json', json.dumps(job['prompt']))
    source_metadata.add_text('impeccable:origin', json.dumps({'method': 'Builtin imagegen original atlas; metadata-only workspace copy'}))
    source.save(path, pnginfo=source_metadata)
    assert Image.open(path).convert('RGBA').tobytes() == source.tobytes()
    xs = [round(x * source.width) for x in job.get('xCuts', [0, .5, 1])]
    ys = [round(y * source.height) for y in job.get('yCuts', [0, .5, 1])]
    assert len(job['ids']) == 4
    for i, asset_id in enumerate(job['ids']):
        col, row = i % 2, i // 2
        # Exact reviewed gutter cuts are stored because generated rows need not align perfectly.
        rect = tuple(job['cells'][i]) if 'cells' in job else (xs[col], ys[row], xs[col + 1], ys[row + 1])
        cell = source.crop(rect)
        bbox = cell.getchannel('A').point(lambda a: 255 if a > 32 else 0).getbbox()
        assert bbox and 0 < bbox[0] < bbox[2] < cell.width and 0 < bbox[1] < bbox[3] < cell.height, (asset_id, bbox, cell.size)
        bbox = (max(0, bbox[0] - 2), max(0, bbox[1] - 2), min(cell.width, bbox[2] + 2), min(cell.height, bbox[3] + 2))
        image = cell.crop(bbox)
        image.thumbnail((132, 94), Image.Resampling.LANCZOS)
        alpha_bounds = image.getchannel('A').getbbox()
        assert alpha_bounds and image.width <= 132 and image.height <= 94
        foot = [.5, (alpha_bounds[3] - 1) / image.height]
        origin = {'sourceFile': 'sources/' + path.name, 'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                  'sourceCell': rect, 'cropBounds': bbox, 'method': 'Technical crop and uniform LANCZOS fit only; no repaint'}
        metadata = PngImagePlugin.PngInfo()
        metadata.add_text('impeccable:prompt', job['prompt'])
        metadata.add_text('impeccable:prompt-json', json.dumps(job['prompt']))
        metadata.add_text('impeccable:origin', json.dumps(origin))
        target = ROOT / 'assets' / (asset_id + '.png')
        image.save(target, pnginfo=metadata)
        assert Image.open(target).convert('RGBA').tobytes() == image.tobytes()
        rows.append({'id': asset_id, 'output_path': 'assets/' + target.name, 'dimensions': list(image.size),
                     'footAnchor': foot, 'prompt_used': job['prompt'], 'source': origin})
assert len(rows) == len({row['id'] for row in rows}) == 20
(ROOT / 'manifest.json').write_text(json.dumps({'direct': rows, 'originalBankPreserved': True}, indent=2) + '\n', encoding='utf-8')

font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 13)
sheet = Image.new('RGB', (800, 780), '#f8f4e9')
for i, row in enumerate(rows):
    x, y = (i % 4) * 200, (i // 4) * 156
    image = Image.open(ROOT / row['output_path']).convert('RGBA')
    sheet.paste(image, (x + (200 - image.width) // 2, y + 10), image)
    text = row['id'].removeprefix('trophy-').replace('special-', '')
    ImageDraw.Draw(sheet).text((x + 6, y + 118), text, font=font, fill='#26352d')
review_metadata = PngImagePlugin.PngInfo()
review_metadata.add_text('impeccable:prompt', 'Technical contact sheet of20normalized revised trophies, no repaint.\n' + '\n'.join(job['prompt'] for job in jobs))
sheet.save(ROOT / 'review.png', pnginfo=review_metadata)
print('20 revised source trophies normalized; exact saved RGBA and dimensions checked')
