"""Large detailed trophy derivatives; no generation, repainting or coarse-grid conversion."""
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, PngImagePlugin

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent.parent
SOURCE = ART / 'trophies'
MASTER = ROOT.parent / 'master.png'
ANCHORS = [(664, 280), (810, 280), (956, 280)]
SLOTS = [(592, 183, 735, 284), (737, 183, 883, 284), (885, 183, 1028, 284)]
LIP_RECT = (590, 281, 1031, 288)
LIMIT = (132, 94)
for name in ('assets', 'previews'):
    (ROOT / name).mkdir(parents=True, exist_ok=True)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(image, path, prompt, origin):
    meta = PngImagePlugin.PngInfo()
    meta.add_text('impeccable:prompt', prompt)
    meta.add_text('impeccable:origin', json.dumps(origin, ensure_ascii=False))
    image.save(path, pnginfo=meta)


master = Image.open(MASTER).convert('RGBA')
assert master.size == (1448, 582)
lip = master.crop(LIP_RECT)
source_rows = json.loads((SOURCE / 'production-manifest.json').read_text(encoding='utf-8'))['produce']
assert len(source_rows) == 63 and len({row['id'] for row in source_rows}) == 63
readability = {
    'species': 'Book covers/open pages, leaf marks and magnifying-glass motifs visible; page micro-marks are decorative.',
    'population': 'Eevee/evolution silhouettes and colors visible; larger groups crowd individual small faces.',
    'catches': 'Pokeball red/white division, surrounding greenery and tier ornament differences visible.',
    'expeditions': 'Mountain snow, trees, tent and summit flag differences visible.',
    'regions': 'Map shape, compass and landscape additions visible; map micro-markings are decorative.',
    'team': 'Backpacks, rolled blankets and increasing travel props visible.',
    'care': 'Heart silhouette, leaves, flower and wreath progression visible.',
    'nursery': 'Egg/nest/chick and greenhouse differences visible.',
    'evolution': 'Branch/leaf/tree/berry progression visible.',
    'shiny': 'Single/multiple star, crescent and star-crown silhouettes visible.',
    'decor': 'House roof, door and increasing garden ornament visible.',
    'special': 'Main special-object motifs visible; semantic inventory names still identify fine distinctions.',
}
rows = []
checks = []
images = {}
for original in source_rows:
    path = SOURCE / original['output_path']
    native = Image.open(path).convert('RGBA')
    assert native.getchannel('A').getextrema()[0] == 0 and native.getchannel('A').getbbox()
    scale = min(LIMIT[0] / native.width, LIMIT[1] / native.height)
    dimensions = (max(1, round(native.width * scale)), max(1, round(native.height * scale)))
    image = native.resize(dimensions, Image.Resampling.LANCZOS)
    source_prompt = (SOURCE / original['prompt_used']).read_text(encoding='utf-8')
    target = ROOT / 'assets' / f"{original['id']}.png"
    origin = {'source': f"trophies/{original['output_path']}", 'sourceSha256': sha(path),
              'originalDimensions': native.size, 'originalRGBASha256': hashlib.sha256(native.tobytes()).hexdigest(),
              'sourceFootAnchor': original['footAnchor'], 'targetDimensions': dimensions,
              'method': 'One uniform-aspect LANCZOS resize directly from original standalone RGBA source; no repainting.',
              'approvedDetailedMaster': '../master.png', 'masterSha256': sha(MASTER),
              'originalPromptFile': f"trophies/{original['prompt_used']}",
              'note': 'Resampling interpolates source colors/alpha; original source RGBA remains unchanged. No480-grid intermediates.'}
    save(image, target, source_prompt, origin)
    decoded = Image.open(target)
    assert decoded.convert('RGBA').tobytes() == image.tobytes()
    assert decoded.info['impeccable:prompt'] == source_prompt
    assert json.loads(decoded.info['impeccable:origin'])['sourceSha256'] == sha(path)
    assert image.width <= LIMIT[0] and image.height <= LIMIT[1]
    assert image.getchannel('A').getextrema()[0] == 0
    images[original['id']] = image
    row = {'id': original['id'], 'source_crop': f"trophies/{original['output_path']}",
           'sourceSha256': sha(path), 'originalRGBASha256': origin['originalRGBASha256'],
           'output_path': f'assets/{target.name}', 'strategy': 'Single LANCZOS uniform fit of original detailed source RGBA',
           'prompt_used': source_prompt, 'dimensions': list(dimensions), 'logicalSize': list(dimensions),
           'footAnchor': original['footAnchor'], 'useSlots': ['trophy-left', 'trophy-center', 'trophy-right'],
           'format': 'png', 'transparency': True, 'deviations': ['Source colors/alpha interpolated by LANCZOS only; fine detail retained at a materially larger displayed size than rejected480-grid derivatives.'],
           'qa_status': 'needs_parent_review', 'readability': readability[original['id'].split('-')[1]],
           'visualDecision': 'User decides final acceptance; observations describe the rendered contact/shelf views.'}
    rows.append(row)
    checks.append({'id': original['id'], 'sourceDimensions': list(native.size), 'targetDimensions': list(dimensions),
                   'alphaBounds1': list(image.getchannel('A').point(lambda value:255 if value > 0 else 0).getbbox()),
                   'alphaBounds128': list(image.getchannel('A').point(lambda value:255 if value >= 128 else 0).getbbox()),
                   'sourceRGBAUnchanged': True, 'embeddedPromptAndOriginChecked': True, 'decodedPixelsExact': True})
assert len(list((ROOT / 'assets').glob('*.png'))) == 63
by_id = {row['id']: row for row in rows}
placements = []


def add_trophy(scene, row, slot):
    image = images[row['id']]
    anchor = ANCHORS[slot]
    position = tuple(round(anchor[i] - image.size[i] * row['footAnchor'][i]) for i in range(2))
    bbox = image.getchannel('A').getbbox()
    bounds = [position[0] + bbox[0], position[1] + bbox[1], position[0] + bbox[2], position[1] + bbox[3]]
    # The physical base meets the shelf/contact edge below the open niche.
    slot_bounds = SLOTS[slot]
    assert slot_bounds[0] <= bounds[0] < bounds[2] <= slot_bounds[2] and slot_bounds[1] <= bounds[1] < bounds[3] <= slot_bounds[3], (row['id'], bounds)
    scene.alpha_composite(image, position)
    return {'id': row['id'], 'slot': slot + 1, 'anchor': list(anchor), 'position': list(position),
            'actualAlphaBounds': bounds, 'contactOverlapsFrontlip': bounds[3] > LIP_RECT[1], 'slotBounds': list(slot_bounds),
            'fitsNicheAndPhysicalShelfContact': True}


def compose(ids):
    scene = master.copy()
    mask = Image.new('L', master.size)
    d = ImageDraw.Draw(mask)
    info = []
    for slot, asset_id in enumerate(ids):
        record = add_trophy(scene, by_id[asset_id], slot)
        info.append(record)
        box = record['actualAlphaBounds']
        d.rectangle((box[0], box[1], box[2] - 1, box[3] - 1), fill=255)
    scene.paste(lip, LIP_RECT[:2])
    inverse = ImageChops.invert(mask)
    for channel in ImageChops.difference(scene, master).split():
        assert ImageChops.multiply(channel, inverse).getbbox() is None
    return scene, info


for row in rows:
    for slot in range(3):
        placements.append(add_trophy(master.copy(), row, slot))
assert len(placements) == 189
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 12)


def label(draw, text, xy, width=24):
    words = text.split('-')
    lines = ['']
    for word in words:
        if len(lines[-1]) + len(word) + 1 > width:
            lines.append(word)
        else:
            lines[-1] += ('-' if lines[-1] else '') + word
    draw.multiline_text(xy, '\n'.join(lines), font=font, fill='#26372d', spacing=2)


contact = Image.new('RGB', (7 * 190, 9 * 155), '#eee7d8')
for index, row in enumerate(rows):
    x, y = (index % 7) * 190, (index // 7) * 155
    image = images[row['id']]
    contact.paste(image, (x + (190 - image.width) // 2, y + 5), image)
    label(ImageDraw.Draw(contact), row['id'], (x + 4, y + 101))
    ImageDraw.Draw(contact).text((x + 4, y + 139), f'{image.width}x{image.height}', font=font, fill='#5b6454')
save(contact, ROOT / 'previews/all63-contact.png', 'Technical large-scale contact sheet of original trophy source derivatives.', {'assets': 63, 'sampling': 'LANCZOS', 'maxDimensions': LIMIT})

shelf_sheet = Image.new('RGB', (3 * 530, 7 * 165), '#eee7d8')
for group in range(21):
    ids = [row['id'] for row in rows[group * 3:group * 3 + 3]]
    scene, info = compose(ids)
    save(scene, ROOT / 'previews' / f'shelf-room-{group + 1:02}.png', 'Three original source trophies independently placed in the same detailed empty master.', {'masterSha256': sha(MASTER), 'ids': ids, 'placements': info, 'outsideTrophyPixelsUnchanged': True, 'pet': None})
    crop = scene.crop((550, 168, 1065, 291))
    x, y = (group % 3) * 530, (group // 3) * 165
    shelf_sheet.paste(crop.convert('RGB'), (x + 5, y))
    for index, asset_id in enumerate(ids):
        label(ImageDraw.Draw(shelf_sheet), asset_id.removeprefix('trophy-'), (x + index * 175 + 5, y + 126), 22)
save(shelf_sheet, ROOT / 'previews/all63-shelves.png', '21 actual shelf compositions; all63 detailed original trophy appearances.', {'masterSha256': sha(MASTER), 'compositions': 21, 'nativeRoomSize': master.size})

examples = {
    'tier1-house-mountain-heart': ['trophy-decor-1', 'trophy-expeditions-1', 'trophy-care-1'],
    'tier4-house-mountain-heart': ['trophy-decor-4', 'trophy-expeditions-4', 'trophy-care-4'],
    'mixed-house-mountain-heart': ['trophy-decor-1', 'trophy-expeditions-4', 'trophy-care-4'],
}
for name, ids in examples.items():
    scene, info = compose(ids)
    save(scene, ROOT / 'previews' / f'{name}.png', 'Readable house/mountain/heart trophies in detailed room; no pet added.', {'masterSha256': sha(MASTER), 'ids': ids, 'placements': info, 'pet': None})

proof = {'status': 'technical-checks-passed-user-visual-decision-pending', 'assets': 63,
         'roomSize': list(master.size), 'masterSha256': sha(MASTER), 'maxDimensions': LIMIT,
         'sourceRGBAUnmodified': True, 'sampling': 'One LANCZOS fit directly from originals',
         'resampledRGBAByteIdentityNotClaimed': True, 'placementsChecked': 189,
         'fullShelfCompositions': 21, 'outsideTrophyPixelsUnchanged': True,
         'sourceMetadataEmbedded': True, 'assetChecks': checks, 'placements': placements,
         'visualInspection': {'files': ['previews/all63-contact.png', 'previews/all63-shelves.png',
                                        'previews/tier1-house-mountain-heart.png',
                                        'previews/tier4-house-mountain-heart.png',
                                        'previews/mixed-house-mountain-heart.png'],
                              'observed': 'Initial all63contact/shelves reviewed. After one technical geometry/size correction, final mixed room reviewed: house, mountain and heart recognizable; curtain rod follows sloping window head; dresser meets cabinet with nearly zero gap.',
                              'latestGeometryInspection': 'previews/mixed-house-mountain-heart.png',
                              'userApprovalClaimed': False},
         'limits': ['These bounds checks do not establish visual approval.', 'No pet or app integration; root owns final composed scene.', 'Frame/map microtext is not treated as readable lettering.', 'One coherent detailed room with interchangeable awards only; complete-room modular recipes are not validated against changed cabinet geometry.']}
(ROOT / 'proof.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
manifest = {'produce': [], 'direct': rows,
            'semantic': [{'id': 'independent-three-trophy-shelf', 'implementation': 'Use three distinct RGBA images with the recorded footAnchor at [664,280],[810,280],[956,280] in1448x582room coordinates. Overlay exact master frontlip crop [590,281,1031,288] after figures. Root adds pet and semantic controls; scale whole scene uniformly.', 'notes': 'Native display sizes up to132x94 from original high-detail source, no rejected480-grid derivative. One coherent detailed room plus interchangeable awards; full-room modularity not claimed.', 'qa_status': 'needs_parent_review'}],
            'execution_order': ['Inspect detailed master', 'Fit63original standalone RGBA sources once with LANCZOS', 'Place all63 in three slots and record189actual bounds', 'Compose21real shelves and tier1/tier4/mixed examples', 'Visual review once with at most one correction'],
            'blockers': [], 'assumptions': ['This is the single final attempt; no150catalog redo.', 'User decides visual acceptance.', 'Original prompts/sourceRGBA preserved; resampling interpolation explicitly allowed.']}
(ROOT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
profile = {'roomSize': list(master.size), 'masterSha256': sha(MASTER), 'anchors': ANCHORS,
           'slotBounds': SLOTS, 'frontlipRect': LIP_RECT, 'maxDimensions': LIMIT,
           'source': 'Original art-production/trophies/assets RGBA', 'sampling': 'LANCZOS',
           'modularityScope': 'Only interchangeable independent awards on one coherent detailed room.'}
(ROOT / 'profile.json').write_text(json.dumps(profile, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: proof[key] for key in ('assets', 'roomSize', 'placementsChecked', 'fullShelfCompositions', 'outsideTrophyPixelsUnchanged')}))
