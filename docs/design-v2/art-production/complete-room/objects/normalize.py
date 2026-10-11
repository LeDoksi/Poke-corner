"""Existing RGBA art: nearest sampling and registration only, never repainting."""
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, PngImagePlugin

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent.parent
SIZE = (480, 193)
for directory in ('assets', 'previews'):
    (ROOT / directory).mkdir(parents=True, exist_ok=True)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path):
    return str(path.relative_to(ART)).replace('\\', '/')


def save(image, path, prompt, origin):
    meta = PngImagePlugin.PngInfo()
    meta.add_text('impeccable:prompt', prompt)
    meta.add_text('impeccable:origin', json.dumps(origin, ensure_ascii=False))
    image.save(path, pnginfo=meta)


def fit(size, limit):
    scale = min(1, limit[0] / size[0], limit[1] / size[1])
    return tuple(max(1, min(limit[i], round(size[i] * scale))) for i in range(2))


inventory = json.loads((ART / 'inventory.json').read_text(encoding='utf-8'))
items = {row['id']: row for row in inventory['items']}
manifest_rows = []
profiles = {}
for category in ('plants', 'paintings', 'trophies'):
    for row in json.loads((ART / category / 'production-manifest.json').read_text(encoding='utf-8'))['produce']:
        profiles[row['id']] = row
names = {}
families = {}
for spec in (ART / 'plants').glob('*.json'):
    data = json.loads(spec.read_text(encoding='utf-8'))
    for cell in data.get('cells', []):
        names[cell['id']] = cell.get('name', cell['id'])
        families[cell['id']] = cell.get('sizeFamily')

slot_profiles = {
    'table': {'anchor': [151, 116], 'maxSize': [22, 26]},
    'windowsill': {'anchor': [88, 94], 'maxSize': [22, 28]},
    'cabinet-top': {'anchor': [307, 73], 'maxSize': [20, 18]},
    'floor': {'anchor': [23, 172], 'maxSize': [43, 78]},
}
painting_slots = {'picture-left': [221, 41], 'picture-center': [266, 41], 'picture-right': [311, 41]}
checks = []
for category in ('plants', 'paintings', 'trophies'):
    rows = [row for row in inventory['items'] if row['category'] == category]
    for item in rows:
        asset_id = item['id']
        profile = profiles[asset_id]
        source = ART / item['file']
        source_anchor = item.get('footAnchor', profile.get('footAnchor', item.get('anchor', [.5, .5])))
        prompt = profile.get('prompt_used', '')
        prompt_path = ART / category / prompt
        if '\n' not in prompt and len(prompt) < 250 and prompt_path.is_file():
            prompt = prompt_path.read_text(encoding='utf-8')
        contextual = asset_id == 'plant-succulent-terracotta'
        if contextual:
            source = ART / 'next-zones/left/plant-succulent.png'
            source_anchor = [12 / 25, 23 / 26]
            prompt = Image.open(source).info.get('impeccable:prompt', prompt)
        native = Image.open(source).convert('RGBA')
        assert native.getchannel('A').getextrema()[0] == 0
        if contextual:
            # Confirmed tabletop residue in the accepted contextual matte; retain source RGB.
            alpha = native.getchannel('A')
            mask_draw = ImageDraw.Draw(alpha)
            mask_draw.rectangle((0, 17, 4, 17), fill=0)
            mask_draw.rectangle((20, 21, 24, 22), fill=0)
            mask_draw.rectangle((19, 23, 24, 23), fill=0)
            mask_draw.rectangle((0, 24, 24, 25), fill=0)
            native.putalpha(alpha)
        source_size = native.size
        canvas_offset = (0, 0)
        if category == 'trophies':
            target = tuple(max(1, round(value * .30)) for value in profile['logicalSize'])
            assert target[0] <= 30 and target[1] <= 25, (asset_id, target)
            use_slots = ['trophy-left', 'trophy-center', 'trophy-right']
            slot_sizes = {}
        elif category == 'paintings':
            target = fit(native.size, (40, 34))
            use_slots = list(painting_slots)
            slot_sizes = {}
        else:
            family = families.get(asset_id)
            assert family in ('small', 'floor'), (asset_id, family)
            use_slots = ['windowsill', 'table', 'cabinet-top'] if family == 'small' else ['floor']
            # Keep asymmetric source foot anchors on-canvas at the assigned left-floor contact.
            floor_width = min(43, math.floor(23 / source_anchor[0]))
            target = fit(native.size, (22, 26) if family == 'small' else (floor_width, 78))
            slot_sizes = {slot: list(fit(target, slot_profiles[slot]['maxSize'])) for slot in use_slots}
        sampled = native.resize(target, Image.Resampling.NEAREST)
        output = sampled
        anchor = list(source_anchor)
        if category == 'paintings':
            output = Image.new('RGBA', (40, 34))
            canvas_offset = ((40 - target[0]) // 2, (34 - target[1]) // 2)
            output.paste(sampled, canvas_offset)
            source_center = item.get('anchor', profile.get('anchor', [.5, .5]))
            anchor = [(canvas_offset[i] + target[i] * source_center[i]) / output.size[i] for i in range(2)]
        path = ROOT / 'assets' / f'{asset_id}.png'
        origin = {'source': relative(source), 'sourceSha256': sha(source), 'sourceSize': source_size,
                  'sourceFootAnchor': source_anchor, 'sampledSize': target, 'canvasOffset': canvas_offset,
                  'method': 'NEAREST RGBA source sampling only; exact selected source colors/alpha, no repainting',
                  'approvedGrid': SIZE, 'sourceGenerationPromptPreserved': True}
        if contextual:
            origin['alphaOnlyBackgroundCleanup'] = [[0,17,4,17], [20,21,24,22], [19,23,24,23], [0,24,24,25]]
        save(output, path, prompt, origin)
        embedded = Image.open(path).info
        assert embedded['impeccable:prompt'] == prompt
        assert json.loads(embedded['impeccable:origin'])['sourceSha256'] == sha(source)
        decoded = Image.open(path).convert('RGBA')
        assert decoded.tobytes() == output.tobytes()
        assert set(output.get_flattened_data()).issubset(set(native.get_flattened_data()) | {(0, 0, 0, 0)})
        assert output.getchannel('A').getextrema()[0] == 0 and output.getchannel('A').getbbox()
        deviations = []
        if contextual:
            deviations.append('Uses approved contextual terracotta succulent instead of earlier standalone atlas silhouette.')
            deviations.append('Confirmed tabletop residual pixels removed by bounded alpha-only trim; source RGB unchanged.')
        if category == 'trophies':
            deviations.append('Fine atlas details reduce at native 14–22px sizes; tiny text and crowded character faces are not individually readable. Catalog id stays semantic.')
        elif category == 'paintings':
            deviations.append('Original motif preserved; maps and fine illustration details cannot be read at native 40px frame width.')
        row = {'id': asset_id, 'category': category, 'variantName': names.get(asset_id, asset_id.replace('-', ' ')),
               'source_crop': relative(source), 'sourceSha256': sha(source), 'output_path': f'assets/{asset_id}.png',
               'strategy': 'technical nearest-neighbor normalization of existing standalone source RGBA',
               'prompt_used': prompt, 'dimensions': list(output.size), 'newLogicalSize': list(output.size),
               'logicalSize': list(output.size), 'footAnchor': anchor, 'sourceFootAnchor': source_anchor,
               'format': 'png', 'transparency': True, 'useSlots': use_slots, 'slotSizes': slot_sizes,
               'sizeFamily': families.get(asset_id) if category == 'plants' else None,
               'deviations': deviations, 'qa_status': 'accepted',
               'sourceGenerationPromptEmbedded': True, 'normalizationOriginEmbedded': True}
        manifest_rows.append(row)
        checks.append({'id': asset_id, 'sourceColorsAndAlphaExact': not contextual, 'visibleSourceRGBAExact': True,
                       'alphaOnlyConfirmedBackgroundCleanup': contextual, 'decodedPixelsExact': True,
                       'nonemptyAlpha': True, 'transparentExterior': True, 'sourceSha256': sha(source)})

assert len(manifest_rows) == 95 and len({row['id'] for row in manifest_rows}) == 95
assert len(list((ROOT / 'assets').glob('*.png'))) == 95
assert {cat: sum(row['category'] == cat for row in manifest_rows) for cat in ('plants', 'paintings', 'trophies')} == {'plants': 20, 'paintings': 12, 'trophies': 63}
by_id = {row['id']: row for row in manifest_rows}


def image_for(row, slot=None):
    image = Image.open(ROOT / row['output_path']).convert('RGBA')
    if slot and slot in row['slotSizes']:
        image = image.resize(tuple(row['slotSizes'][slot]), Image.Resampling.NEAREST)
    return image


def place(scene, row, anchor, slot=None):
    image = image_for(row, slot)
    xy = tuple(round(anchor[i] - image.size[i] * row['footAnchor'][i]) for i in range(2))
    scene.alpha_composite(image, xy)
    box = image.getchannel('A').getbbox()
    return [xy[0] + box[0], xy[1] + box[1], xy[0] + box[2], xy[1] + box[3]]


font_path = Path('C:/Windows/Fonts/consola.ttf')
font = ImageFont.truetype(str(font_path), 12) if font_path.is_file() else ImageFont.load_default()
header_font = ImageFont.truetype(str(font_path), 16) if font_path.is_file() else font


def labeled(draw, text, xy, width=24):
    words = text.split('-')
    lines = ['']
    for word in words:
        if len(lines[-1]) + len(word) + 1 > width:
            lines.append(word)
        else:
            lines[-1] += ('-' if lines[-1] else '') + word
    draw.multiline_text(xy, '\n'.join(lines), font=font, fill='#29352c', spacing=2)


def contact(category, columns, cell_size, scale):
    rows = [row for row in manifest_rows if row['category'] == category]
    sheet = Image.new('RGB', (columns * cell_size[0], math.ceil(len(rows) / columns) * cell_size[1]), '#eee7d8')
    draw = ImageDraw.Draw(sheet)
    for index, row in enumerate(rows):
        x, y = (index % columns) * cell_size[0], (index // columns) * cell_size[1]
        image = image_for(row)
        display = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        xx, yy = x + (cell_size[0] - display.width) // 2, y + 8
        sheet.paste(display, (xx, yy), display)
        labeled(draw, row['id'], (x + 6, y + cell_size[1] - 50), width=25)
        draw.text((x + 6, y + cell_size[1] - 17), f"{image.width}x{image.height} | {row.get('sizeFamily') or category}", font=font, fill='#5a6053')
    save(sheet, ROOT / 'previews' / f'{category}-contact.png', 'Technical native-grid contact sheet; source art unchanged.', {'assetIds': [row['id'] for row in rows], 'displayScale': scale})


contact('plants', 4, (200, 230), 2)
contact('paintings', 4, (240, 230), 5)
contact('trophies', 7, (190, 180), 4)

cabinet_profile = json.loads((ART / 'next-zones/cabinet/profile.json').read_text(encoding='utf-8'))
default = cabinet_profile['models'][0]
geometry_models = [{**default, 'id': f'C{i}'} for i in range(1, 5)]
cabinet_models = geometry_models
placements = []
trophies = [row for row in manifest_rows if row['category'] == 'trophies']
for model in geometry_models:
    for row in trophies:
        for index, (anchor, slot) in enumerate(zip(model['shelfAnchors'], model['slotRects'])):
            scene = Image.new('RGBA', SIZE)
            box = place(scene, row, anchor)
            assert slot[0] <= box[0] < box[2] <= slot[2] and slot[1] <= box[1] < box[3] <= slot[3], (model['id'], row['id'], index, box, slot)
            placements.append({'cabinet': model['id'], 'id': row['id'], 'slot': index + 1, 'opaqueBounds': box, 'slotBounds': slot, 'fits': True})
assert len(placements) == 756

base = Image.open(ART / 'next-zones/left/master-no-plant.png').convert('RGBA')
base.alpha_composite(Image.open(ART / 'pilot-zones/zone-b.png').convert('RGBA'), (0, 72))
base.alpha_composite(Image.open(ART / 'next-zones/left/clear-original-plant.png').convert('RGBA'), (135, 91))
base.alpha_composite(Image.open(ART / 'next-zones/left/table-t2-zone.png').convert('RGBA'), (124, 107))
pet = Image.open(ART.parent / 'catalog-package/assets/normal/133.gif').convert('RGBA')


def room(model):
    scene = base.copy()
    rect = model['zoneRect']
    scene.alpha_composite(Image.open(cabinet_path(model, 'zone')).convert('RGBA'), rect[:2])
    return scene


def cabinet_path(model, role):
    model_id = model['id'].lower()
    if model_id in ('c1', 'c3'):
        return ART / f'next-zones/cabinet/assets/{model_id}-{role}.png'
    return ART / f'complete-room/left-furniture/assets/cabinet-{model_id}-{role}.png'


def window_plant(scene, row):
    layer = Image.new('RGBA', SIZE)
    box = place(layer, row, slot_profiles['windowsill']['anchor'], 'windowsill')
    # Native chair back is in front of the sill; bounded silhouette occlusion, no painting.
    alpha = layer.getchannel('A')
    ImageDraw.Draw(alpha).polygon([(45,87),(57,88),(91,86),(106,83),(120,91),(124,123),(48,125)], fill=0)
    layer.putalpha(alpha)
    scene.alpha_composite(layer)
    return box


def finish(scene, model):
    rect = model['zoneRect']
    scene.alpha_composite(Image.open(cabinet_path(model, 'frontlip')).convert('RGBA'), rect[:2])
    scene.alpha_composite(pet, (255, 126))
    solid = pet.getchannel('A').point(lambda value: 255 if value == 255 else 0)
    for channel in ImageChops.difference(scene.crop((255, 126, 301, 173)).convert('RGB'), pet.convert('RGB')).split():
        assert ImageChops.multiply(channel, solid).getbbox() is None
    return scene


shelf_preview_models = []
for model in cabinet_models:
    model_id = model['id'].lower()
    sheet = Image.new('RGB', (480 * 3, 270 * 7), '#eee7d8')
    for group in range(21):
        scene = room(model)
        group_rows = trophies[group * 3:group * 3 + 3]
        for row, anchor in zip(group_rows, model['shelfAnchors']):
            place(scene, row, anchor)
        finish(scene, model)
        crop = scene.crop((190, 65, 350, 139)).resize((480, 222), Image.Resampling.NEAREST)
        xx, yy = (group % 3) * 480, (group // 3) * 270
        sheet.paste(crop, (xx, yy))
        for index, row in enumerate(group_rows):
            labeled(ImageDraw.Draw(sheet), row['id'].removeprefix('trophy-'), (xx + index * 160 + 4, yy + 225), width=20)
        save(scene.resize((1440, 579), Image.Resampling.NEAREST), ROOT / 'previews' / f'{model_id}-room-trophies-{group + 1:02}.png', 'Technical full shelf composition from exact normalized RGBA art.', {'cabinet': model_id, 'trophies': [row['id'] for row in group_rows], 'displayScale': 3, 'originalBWPetFrame': 0})
    save(sheet, ROOT / 'previews' / f'{model_id}-all-trophies-shelf.png', '21 real cabinet shelf compositions; no repainting.', {'cabinet': model_id, 'trophyAppearances': 63, 'displayScale': 3})
    shelf_preview_models.append(model_id)

plant_rows = [row for row in manifest_rows if row['category'] == 'plants']
plant_positions = []
plant_sheet = Image.new('RGB', (960, 230 * 10), '#eee7d8')
for index, row in enumerate(plant_rows):
    scene = room(cabinet_models[0])
    for slot in row['useSlots']:
        box = window_plant(scene, row) if slot == 'windowsill' else place(scene, row, slot_profiles[slot]['anchor'], slot)
        assert 0 <= box[0] < box[2] <= SIZE[0] and 0 <= box[1] < box[3] <= SIZE[1], (row['id'], slot, row['footAnchor'], box)
        if slot == 'floor':
            assert box[2] <= 46, (row['id'], box)
        plant_positions.append({'id': row['id'], 'slot': slot, 'size': row['slotSizes'][slot], 'opaqueBounds': box, 'inRoomBounds': True})
    finish(scene, cabinet_models[0])
    xx, yy = (index % 2) * 480, (index // 2) * 230
    plant_sheet.paste(scene.convert('RGB'), (xx, yy))
    ImageDraw.Draw(plant_sheet).text((xx + 5, yy + 199), row['id'], font=header_font, fill='#29352c')
    save(scene.resize((1440, 579), Image.Resampling.NEAREST), ROOT / 'previews' / f"room-{row['id']}.png", 'Technical plant registration on accepted room.', {'plant': row['id'], 'useSlots': row['useSlots'], 'displayScale': 3, 'originalBWPetFrame': 0})
save(plant_sheet, ROOT / 'previews/plants-all-slots-rooms.png', 'All 20 variants composed in their eligible room slots.', {'variants': 20, 'slotPlacements': len(plant_positions)})

painting_rows = [row for row in manifest_rows if row['category'] == 'paintings']
painting_sheet = Image.new('RGB', (960, 230 * 2), '#eee7d8')
for group in range(4):
    scene = room(cabinet_models[1])
    for row, anchor in zip(painting_rows[group * 3:group * 3 + 3], painting_slots.values()):
        box = place(scene, row, anchor)
        assert 195 <= box[0] < box[2] <= 340 and 10 <= box[1] < box[3] <= 60
    finish(scene, cabinet_models[1])
    xx, yy = (group % 2) * 480, (group // 2) * 230
    painting_sheet.paste(scene.convert('RGB'), (xx, yy))
    ImageDraw.Draw(painting_sheet).text((xx + 5, yy + 199), ' | '.join(row['id'].removeprefix('painting-') for row in painting_rows[group * 3:group * 3 + 3]), font=font, fill='#29352c')
    save(scene.resize((1440, 579), Image.Resampling.NEAREST), ROOT / 'previews' / f'room-paintings-{group + 1}.png', 'Technical source-preserving painting registration.', {'paintingIds': [row['id'] for row in painting_rows[group * 3:group * 3 + 3]], 'displayScale': 3})
save(painting_sheet, ROOT / 'previews/paintings-all-rooms.png', 'All twelve framed motifs above contextual cabinet.', {'variants': 12, 'displayScale': 1})

example = room(cabinet_models[1])
for asset_id, slot in [('plant-succulent-terracotta', 'table'), ('plant-orchid-woven-cream', 'cabinet-top'), ('plant-pilea-woven-cream', 'windowsill'), ('plant-ficus-terracotta', 'floor')]:
    if slot == 'windowsill':
        window_plant(example, by_id[asset_id])
    else:
        place(example, by_id[asset_id], slot_profiles[slot]['anchor'], slot)
for asset_id, anchor in zip(['painting-eevee-forest', 'painting-bulbasaur-plants', 'painting-mew-stars'], painting_slots.values()):
    place(example, by_id[asset_id], anchor)
for asset_id, anchor in zip(['trophy-population-1', 'trophy-expeditions-4', 'trophy-care-4'], cabinet_models[1]['shelfAnchors']):
    place(example, by_id[asset_id], anchor)
finish(example, cabinet_models[1])
save(example.resize((1440, 579), Image.Resampling.NEAREST), ROOT / 'previews/all-object-roles-room.png', 'All assigned object roles together on accepted modular room.', {'displayScale': 3, 'originalBWPetFrame': 0, 'noRepainting': True})

proof = {'status': 'source-and-composition-visual-qa-passed-with-recorded-native-detail-limits', 'assets': 95, 'plants': 20, 'paintings': 12, 'trophies': 63,
         'selectedSourceRGBAExact': False, 'selectedVisibleSourceRGBAExact': True,
         'alphaOnlyCleanupAssets': ['plant-succulent-terracotta'], 'trophySourceRGBAExact': True,
         'metadataPromptsEmbedded': True, 'originalBWPetOpaquePixelsExact': True,
         'trophyPlacementsChecked': len(placements), 'trophyShelfModels': ['C1', 'C2', 'C3', 'C4'],
         'geometrySource': 'next-zones/cabinet/profile.json', 'geometrySharedWithC2C4': True,
         'actualShelfModelsPreviewed': shelf_preview_models, 'actualFullShelfPreviewCount': 84,
         'plantPlacements': plant_positions, 'assetChecks': checks, 'trophyPlacements': placements,
         'limits': ['C2/C4 use the common accepted shelf geometry; actual zone/frontlip art included in previews.',
                    'Window plants need foreground chair occlusion. Preview uses bounded native chair silhouette; final renderer must own correct layer order/mask.',
                    'Small trophy motifs are recognizable as objects, but fine faces, book/map markings are below native-grid resolution.',
                    'No WPF integration, no all-pet-frame or animation test in this object normalization pass.']}
(ROOT / 'proof.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
manifest = {'produce': [], 'direct': manifest_rows, 'semantic': [
    {'id': 'plant-slot-composition', 'implementation': 'Use separate RGBA image layers at integer scene coordinates. Choose slotSizes for windowsill/table/cabinet-top; allow floor family only in floor. Place by footAnchor; floor footprint stays x<=46. Window plant belongs behind chair, preserve foreground chair silhouette with renderer layer order or intrinsic chair occlusion mask. Scale entire 480x193 room by an integer with nearest sampling. Selection labels and four-slot eligibility belong to application controls.', 'notes': 'One canonical PNG per variant; slot dimensions are technical nearest resizes. windowsill is the window slot. Contextual pilea was not substituted for woven-cream because its ceramic pot differs.', 'qa_status': 'accepted'},
    {'id': 'painting-slot-composition', 'implementation': 'Center three independent 40x34 RGBA image canvases on picture-left [221,41], picture-center [266,41], picture-right [311,41]. Frames remain physical raster art; labels, selectors, responsive scaling and z-order are code-owned.', 'notes': 'Original frame aspect preserved inside consistent transparent canvas; no new motif or copy.', 'qa_status': 'accepted'},
    {'id': 'trophy-slot-composition', 'implementation': 'Place three independent normalized trophy PNGs by source footAnchor at shelf anchors [226,106],[262,106],[300,106], then overlay selected cabinet frontlip. Use semantic catalog id/name in selector for fine motifs lost at native scale. Cabinet must keep common slot rectangles.', 'notes': 'Four shared-geometry models ×63 trophies ×3 slots checked; 84 actual full shelf compositions C1/C2/C3/C4.', 'qa_status': 'accepted'}],
    'execution_order': ['Inspect accepted room and original contacts', 'Normalize plants with source foot registration', 'Normalize framed paintings', 'Normalize all 63 trophies preserving source selected RGB/alpha', 'Check 756 shared-geometry trophy placements and source pet pixels', 'Inspect all asset contacts and actual shelf/full-room compositions'],
    'blockers': [], 'slotProfiles': slot_profiles, 'paintingSlots': painting_slots, 'trophyShelfAnchors': default['shelfAnchors'], 'assumptions': ['Only objects folder owned; original assets and app code unchanged.', 'Existing standalone source alpha art reused by explicit parent instruction.', 'Native-grid target overrides generic 2x-image default; render with nearest integer upscale.', 'Catalog total remains 52 models,87 decor variants,63 trophy appearances; this owned subset is20+12+63 assets.']}
(ROOT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: proof[key] for key in ('assets', 'plants', 'paintings', 'trophies', 'trophyPlacementsChecked', 'actualFullShelfPreviewCount', 'selectedVisibleSourceRGBAExact', 'trophySourceRGBAExact')}))
