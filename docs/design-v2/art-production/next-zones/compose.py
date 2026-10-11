"""Combine the accepted room and independently registered left/cabinet layers."""
import json
import runpy
from itertools import product
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, PngImagePlugin

ROOT = Path(__file__).resolve().parent
left = runpy.run_path(str(ROOT / 'left/assemble.py'))
profile = json.loads((ROOT / 'cabinet/profile.json').read_text(encoding='utf-8'))
placements = json.loads((ROOT / 'cabinet/proof.json').read_text(encoding='utf-8'))['placements']
meta = PngImagePlugin.PngInfo()
origin = 'Technical composition of separate contextual room zones, original BW 133.gif and source-derived trophy/frontlip layers.\n'
for prompt in sorted((ROOT / 'left').glob('*prompt.txt')) + sorted((ROOT / 'cabinet/_sources').glob('*prompt.txt')):
    origin += str(prompt.relative_to(ROOT)) + '\n' + prompt.read_text(encoding='utf-8') + '\n'
meta.add_text('impeccable:prompt', origin)


def save(im, name):
    im.save(ROOT / name, pnginfo=meta)


def scene(chair, table, plant, cabinet, trophies):
    im = left['assemble'](chair, table, plant, False)
    model = profile['models'][cabinet]
    rect = model['zoneRect']
    model_id = model['id'].lower()
    zone = Image.open(ROOT / f'cabinet/assets/{model_id}-zone.png').convert('RGBA')
    im.alpha_composite(zone, rect[:2])
    if trophies:
        for slot, trophy_id in enumerate(['trophy-population-1','trophy-expeditions-1','trophy-care-1'], 1):
            placement = next(p for p in placements if p['model']==model_id and p['id']==trophy_id and p['slot']==slot)
            trophy = Image.open(ROOT / f'cabinet/assets/{trophy_id}-logical.png').convert('RGBA')
            im.alpha_composite(trophy, tuple(placement['position']))
        im.alpha_composite(Image.open(ROOT / f'cabinet/assets/{model_id}-frontlip.png').convert('RGBA'), rect[:2])
    return im


cases = list(product(range(2),range(2),range(3),range(2),range(2)))
reference = scene(0,0,0,0,0)
union = Image.new('L',(480,193))
draw = ImageDraw.Draw(union)
draw.rectangle((0,72,224,192),fill=255)
draw.rectangle((195,65,344,141),fill=255)
inverse = ImageChops.invert(union)
for case in cases:
    im = scene(*case)
    for channel in ImageChops.difference(im.convert('RGB'),reference.convert('RGB')).split():
        assert ImageChops.multiply(channel,inverse).getbbox() is None
    # Each cabinet choice preserves the independently chosen left corner pixel-for-pixel.
    plain = left['assemble'](*case[:3],False)
    assert im.crop((0,0,190,193)).tobytes()==plain.crop((0,0,190,193)).tobytes()

gif = Image.open(ROOT.parent.parent / 'catalog-package/assets/normal/133.gif')
pet_frames=[];durations=[]
for index in range(gif.n_frames):
    gif.seek(index);pet_frames.append(gif.convert('RGBA').copy());durations.append(gif.info.get('duration',100))
assert gif.n_frames==67
shadow = Image.new('RGBA',(480,193))
ImageDraw.Draw(shadow).ellipse((266,170,290,175),fill=(48,30,16,60))


def pet_on(im,index):
    pet=pet_frames[index]
    out=im.copy();out.alpha_composite(shadow);out.alpha_composite(pet,(255,126))
    solid=pet.getchannel('A').point(lambda n:255 if n==255 else 0)
    actual=out.crop((255,126,301,173))
    for channel in ImageChops.difference(actual.convert('RGB'),pet.convert('RGB')).split():
        assert ImageChops.multiply(channel,solid).getbbox() is None
    return out.convert('RGB')


# Each step changes exactly one independent choice; the pet continues its original timed animation.
sequence=[(0,0,1,0,0),(0,0,1,0,1),(0,0,1,1,1),(0,1,1,1,1),
          (0,1,2,1,1),(1,1,2,1,1),(1,1,0,1,1),(1,1,0,1,0),
          (0,1,0,1,0),(0,0,0,1,0),(0,0,0,0,0)]
for current,following in zip(sequence,sequence[1:]+sequence[:1]):
    assert sum(a!=b for a,b in zip(current,following))==1
rendered=[];times=[]
for step,case in enumerate(sequence):
    bg=scene(*case)
    save(pet_on(bg,0).resize((1440,579),Image.Resampling.NEAREST),f'state-{step+1}.png')
    for tick in range(20):
        index=(step*20+tick)%len(pet_frames)
        rendered.append(pet_on(bg,index));times.append(durations[index])
rendered[0].save(ROOT / 'room-independent-swaps.png',save_all=True,append_images=rendered[1:],
                 duration=times,loop=0,disposal=0,blend=0,pnginfo=meta)
animation=Image.open(ROOT / 'room-independent-swaps.png')
decoded=[];total=0
for index in range(animation.n_frames):
    animation.seek(index);duration=animation.info['duration']
    decoded.append((total,total+duration,animation.convert('RGB').copy()));total+=duration
assert abs(total-sum(times))<.1
cursor=0;at=0
for expected,duration in zip(rendered,times):
    while at>=decoded[cursor][1]-.001:cursor+=1
    assert decoded[cursor][0]<=at<decoded[cursor][1]
    assert decoded[cursor][2].tobytes()==expected.tobytes()
    at+=duration
proof={'status':'technical-checks-passed-visual-review-pending','combinationsChecked':len(cases),
       'choices':{'chairs':2,'tables':2,'plantsIncludingEmpty':3,'cabinets':2,'trophyShelfStates':2},
       'outsideZonesUnchanged':True,'cabinetPreservesLeftCorner':True,'sourcePetOpaquePixelsUnchanged':True,
       'animationDecodedAtEverySourceTimestamp':True,'sourcePetFrames':len(pet_frames),
       'renderedTimestampFrames':len(rendered),'encodedAPNGFrames':animation.n_frames,
       'animationDurationMs':total,'animationBytes':(ROOT / 'room-independent-swaps.png').stat().st_size,
       'sequence':sequence,'limits':['Only the current two cabinet models and three representative trophies shown.',
       '378 trophy-size tests do not prove every trophy silhouette is visually legible.','No app or WPF integration.']}
(ROOT/'proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'index.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>PokéCorner — независимый декор</title><style>body{margin:0;background:#f5f0e4;color:#29372f;font:18px Segoe UI,sans-serif}main{max-width:1440px;margin:auto;padding:24px}p{max-width:75ch;line-height:1.5}img{width:100%;height:auto;image-rendering:pixelated}a{color:#294e35}a:focus-visible{outline:3px solid #294e35;outline-offset:4px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:24px}@media(max-width:850px){.pair{grid-template-columns:1fr}}</style>
<main><h1>Комната собирается по предметам</h1><p>В анимации по очереди меняется только один выбор: трофеи, шкаф, столик, растение, кресло. Иви продолжает исходную анимацию. Это просмотр художественной сборки, без игрового интерактива.</p>
<img src="room-independent-swaps.png" alt="Последовательная замена предметов комнаты с анимированным Иви"><div class="pair"><section><h2>Дубовый шкаф, пустая полка</h2><a href="state-1.png"><img src="state-1.png" alt="Первое сочетание"></a></section><section><h2>Книжный шкаф и три трофея</h2><a href="state-6.png"><img src="state-6.png" alt="Второе сочетание"></a></section></div><p><a href="left/all-12-combinations.png">Все сочетания левого уголка</a></p></main></html>''',encoding='utf-8')
print(json.dumps({k:proof[k] for k in ['combinationsChecked','encodedAPNGFrames','animationDurationMs','animationBytes']}))
