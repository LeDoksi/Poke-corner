"""Technical room patches and actual-pixel plant mattes; no artwork repainting."""
import json
from itertools import product
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, PngImagePlugin

ROOT = Path(__file__).resolve().parent
PILOT = ROOT.parent.parent / 'pilot-zones'
SIZE = (480, 193)
SCALE = 3
metadata = PngImagePlugin.PngInfo()
origin = 'Technical crop, matte and composition from accepted pilot master, original BW 133.gif and contextual edits. No painted pixels added.\n'
for filename in ['empty-t1-prompt.txt', 'empty-t2-prompt.txt', 'pilea-prompt.txt']:
    origin += filename + '\n' + (ROOT / filename).read_text(encoding='utf-8') + '\n'
metadata.add_text('impeccable:prompt', origin)


def save(im, filename):
    im.save(ROOT / filename, pnginfo=metadata)


def normalize(path):
    im = Image.open(path).convert('RGBA')
    return im.crop((0, 0, im.width, round(im.width * SIZE[1] / SIZE[0]))).resize(SIZE, Image.Resampling.NEAREST)


def soft_mask(points, feather=2):
    core = Image.new('L', SIZE)
    ImageDraw.Draw(core).polygon(points, fill=255)
    return ImageChops.lighter(core, core.filter(ImageFilter.GaussianBlur(feather)))


def patch(source, mask, bounds, filename):
    im = source.crop(bounds)
    im.putalpha(mask.crop(bounds))
    save(im, filename)
    return im


original = Image.open(PILOT / 'master.png').convert('RGBA')
empty_t1 = normalize(ROOT / 'empty-t1.png')
empty_t2 = normalize(ROOT / 'empty-t2.png')
pilea_context = normalize(ROOT / 'pilea-context.png')

# A contextual removal restores only the old plant region; unrelated rerendered room pixels never ship.
clear_bounds = (135, 91, 168, 125)
clear_mask = soft_mask([(139, 96), (143, 93), (157, 93), (164, 100), (164, 119), (158, 122), (145, 122), (139, 115)], 2)
clear = patch(empty_t1, clear_mask, clear_bounds, 'clear-original-plant.png')
base = original.copy()
base.alpha_composite(clear, clear_bounds[:2])
save(base, 'master-no-plant.png')

table_bounds = (124, 107, 184, 168)
table_polygon = [(130, 114), (143, 109), (169, 109), (178, 114), (178, 151), (174, 160), (162, 164), (130, 162), (127, 150)]
table_mask = soft_mask(table_polygon, 2)
tables = [patch(base, table_mask, table_bounds, 'table-t1-zone.png'),
          patch(empty_t2, table_mask, table_bounds, 'table-t2-zone.png')]


def plant_matte(source, bounds, filename):
    """Recover alpha from contextual edit difference against the empty table, keeping source RGB."""
    im = source.crop(bounds)
    bg = empty_t1.crop(bounds)
    alpha = Image.new('L', im.size)
    # shortcut: this registered pair shares one backdrop; new plant silhouettes need their own bounded matte.
    for y in range(im.height):
        for x in range(im.width):
            rgb = im.getpixel((x, y))[:3]
            old = bg.getpixel((x, y))[:3]
            delta = max(abs(a - b) for a, b in zip(rgb, old))
            if delta >= 24:
                alpha.putpixel((x, y), 255)
    # Keep the connected plant, discard isolated backdrop variation, and fill its enclosed tiny gaps only.
    pixels = {(x, y) for y in range(im.height) for x in range(im.width) if alpha.getpixel((x, y))}
    components = []
    while pixels:
        seed = pixels.pop(); group = {seed}; queue = [seed]
        while queue:
            x, y = queue.pop()
            for xx, yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if (xx, yy) in pixels:
                    pixels.remove((xx, yy)); group.add((xx, yy)); queue.append((xx, yy))
        components.append(group)
    keep = max(components, key=len)
    alpha = Image.new('L', im.size)
    for xy in keep:
        alpha.putpixel(xy, 255)
    if filename == 'plant-pilea.png':
        # Confirmed tabletop residue beyond the pot outline, not a recolor or silhouette approximation.
        ImageDraw.Draw(alpha).rectangle((21, 27, 28, 29), fill=0)
    im.putalpha(alpha)
    assert alpha.getbbox() and len(keep) > 70
    save(im, filename)
    return im


plants = [None,
          plant_matte(original, (139, 96, 164, 122), 'plant-succulent.png'),
          plant_matte(pilea_context, (137, 92, 166, 122), 'plant-pilea.png')]
plant_source_bounds = [None, (139, 96, 164, 122), (137, 92, 166, 122)]
# Both current tables have a usable contact at this native location; future models register separately.
table_contacts = [(151, 119), (151, 116)]
plant_contacts = [None, (151, 119), (151, 120)]
chair_bounds = (0, 72, 225, 193)
chairs = [Image.open(PILOT / ('zone-' + letter + '.png')).convert('RGBA') for letter in 'ab']

pet = Image.open(ROOT.parent.parent.parent / 'catalog-package/assets/normal/133.gif')
pet.seek(0)
pet = pet.convert('RGBA')
pet_xy = (255, 126)
shadow = Image.new('RGBA', SIZE)
ImageDraw.Draw(shadow).ellipse((266, 170, 290, 175), fill=(48, 30, 16, 60))


def assemble(chair, table, plant, with_pet=True):
    scene = base.copy()
    scene.alpha_composite(chairs[chair], chair_bounds[:2])
    # Chair A's patch can restore original plant pixels at its soft table exclusion; clean again first.
    scene.alpha_composite(clear, clear_bounds[:2])
    scene.alpha_composite(tables[table], table_bounds[:2])
    if plant:
        bounds = plant_source_bounds[plant]
        contact = plant_contacts[plant]
        destination = table_contacts[table]
        xy = (bounds[0] + destination[0] - contact[0], bounds[1] + destination[1] - contact[1])
        scene.alpha_composite(plants[plant], xy)
    if with_pet:
        scene.alpha_composite(shadow)
        scene.alpha_composite(pet, pet_xy)
    return scene


cases = list(product(range(2), range(2), range(3)))
frames = [assemble(*case) for case in cases]
for case, frame in zip(cases, frames):
    name = f'chair-{case[0]+1}_table-{case[1]+1}_plant-{case[2]}.png'
    save(frame.resize((1440, 579), Image.Resampling.NEAREST), name)
    source_crop = frame.crop((255, 126, 301, 173))
    solid = pet.getchannel('A').point(lambda a: 255 if a == 255 else 0)
    for channel in ImageChops.difference(source_crop.convert('RGB'), pet.convert('RGB')).split():
        assert ImageChops.multiply(channel, solid).getbbox() is None
    diff = ImageChops.difference(frame.convert('RGB'), frames[0].convert('RGB')).getbbox()
    assert diff is None or (diff[0] >= 0 and diff[1] >= 72 and diff[2] <= 225 and diff[3] <= 193)

# Changing the plant preserves the other furniture: actual paired-pixel comparisons outside registered plant area.
plant_change_bounds = (134, 87, 169, 125)
for chair, table in product(range(2), range(2)):
    empty = assemble(chair, table, 0, False)
    for plant in [1, 2]:
        occupied = assemble(chair, table, plant, False)
        diff = ImageChops.difference(empty.convert('RGB'), occupied.convert('RGB')).getbbox()
        assert diff and all([diff[0] >= plant_change_bounds[0], diff[1] >= plant_change_bounds[1],
                            diff[2] <= plant_change_bounds[2], diff[3] <= plant_change_bounds[3]])

contact = Image.new('RGB', (960, 193 * 6), '#f5f0e4')
for index, frame in enumerate(frames):
    contact.paste(frame.convert('RGB'), ((index % 2) * 480, (index // 2) * 193))
save(contact, 'all-12-combinations.png')
# Visual swap sample only; original 67-frame pet motion remains demonstrated by the accepted pilot APNG.
sequence = [(0,0,1),(0,1,1),(0,1,2),(1,1,2),(1,0,2),(1,0,0)]
cycle = [assemble(*case).convert('RGB') for case in sequence]
cycle[0].save(ROOT / 'independent-swaps.png', save_all=True, append_images=cycle[1:],
              duration=1800, loop=0, disposal=0, blend=0, pnginfo=metadata)
proof = {'status':'technical-checks-passed-visual-review-pending', 'canvas':list(SIZE), 'displayScale':SCALE,
         'combinations':len(cases), 'chairModels':['A1','A2'], 'tableModels':['T1','T2'],
         'plantModels':['none','succulent','pilea'], 'outsideLeftZoneUnchanged':True,
         'plantSwapPreservesOtherFurniture':True, 'opaqueSourcePetPixelsUnchanged':True,
         'tableContacts':table_contacts, 'plantSourceContacts':plant_contacts,
         'tableZoneRect':table_bounds, 'plantSourceBounds':plant_source_bounds,
         'origins': ['../../pilot-zones/master.png','empty-t1.png','empty-t2.png','pilea-context.png'],
         'limits':['Only two tables and two plant silhouettes registered.','Other table/pot contact combinations require registration.',
                   'This new swap APNG uses the first original pet frame; original pet motion remains in pilot-swap.png.']}
(ROOT / 'proof.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps({'combinations':len(cases),'plantAlphaPixels':[sum(p.getchannel('A').histogram()[1:]) for p in plants[1:]],
                  'checks':'source-pet / outside-zone / independent-plant passed'}))
