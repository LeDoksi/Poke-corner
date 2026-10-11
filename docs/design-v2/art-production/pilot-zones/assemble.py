"""Small zonal pilot: technical crop/scale/composition, original BW frames unchanged."""
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT=Path(__file__).resolve().parent
profile=json.loads((ROOT/'profile.json').read_text(encoding='utf-8'))
size=tuple(profile['canvas']);display=profile['displayScale'];rect=tuple(profile['zoneRect'])
assert 0<=rect[0]<rect[2]<=size[0] and 0<=rect[1]<rect[3]<=size[1]


def normalize(path):
    im=Image.open(path).convert('RGBA')
    crop_h=round(im.width*size[1]/size[0])
    assert crop_h<=im.height
    box=(0,0,im.width,crop_h)
    return im.crop(box).resize(size,Image.Resampling.NEAREST),list(im.size),box


master,native_a,crop_a=normalize(ROOT/'state-a.png')
alternate,native_b,crop_b=normalize(ROOT/'state-b.png')
protected=(128,92,187,167)  # Fixed table/plant in this narrowly scoped chair-swap pilot.
alternate.paste(master.crop(protected),(protected[0],protected[1]))
core=Image.new('L',size)
mask_polygon=[(27,77),(105,76),(132,86),(138,132),(141,155),(154,164),(157,177),(133,186),(37,185),(22,172),(22,109)]
ImageDraw.Draw(core).polygon(mask_polygon,fill=255)
mask=ImageChops.lighter(core,core.filter(ImageFilter.GaussianBlur(3)))
# Preserve the fixed table/plant with a soft technical exclusion, not an opaque rectangle seam.
table_core=Image.new('L',size)
ImageDraw.Draw(table_core).rectangle(protected,fill=255)
table_mask=ImageChops.lighter(table_core,table_core.filter(ImageFilter.GaussianBlur(2)))
mask=ImageChops.multiply(mask,ImageChops.invert(table_mask))
zone_a=master.crop(rect);zone_b=alternate.crop(rect)
zone_a.putalpha(mask.crop(rect));zone_b.putalpha(mask.crop(rect))
zone_a.save(ROOT/'zone-a.png');zone_b.save(ROOT/'zone-b.png')
master.save(ROOT/'master.png')
backgrounds=[]
for zone in [zone_a,zone_b]:
    scene=master.copy();scene.alpha_composite(zone,(rect[0],rect[1]));backgrounds.append(scene)
diff=ImageChops.difference(backgrounds[0].convert('RGB'),backgrounds[1].convert('RGB'))
changed=diff.getbbox();assert changed and changed[0]>=rect[0] and changed[1]>=rect[1] and changed[2]<=rect[2] and changed[3]<=rect[3]
assert backgrounds[0].crop(protected).tobytes()==backgrounds[1].crop(protected).tobytes()

gif=Image.open(ROOT.parent.parent/'catalog-package/assets/normal/133.gif')
union=tuple(profile['petUnionBox']);pet_frames=[];durations=[]
for index in range(gif.n_frames):
    gif.seek(index);pet_frames.append(gif.convert('RGBA').crop(union));durations.append(gif.info.get('duration',100))
assert len(pet_frames)==profile['frames']
px,py=profile['petFloorAnchor'];pet_xy=(round(px-pet_frames[0].width/2),py-pet_frames[0].height)
assert pet_xy[0]>rect[2] and py<=size[1]

shadow=Image.new('RGBA',size)
# Renderer geometry, not a painted/recolored Pokemon asset.
ImageDraw.Draw(shadow).ellipse((px-12,py-3,px+12,py+2),fill=(48,30,16,60))
rendered=[]
for variant,background in enumerate(backgrounds):
    for pet in pet_frames:
        frame=background.copy();frame.alpha_composite(shadow);frame.alpha_composite(pet,pet_xy)
        source_crop=frame.crop((pet_xy[0],pet_xy[1],pet_xy[0]+pet.width,pet_xy[1]+pet.height))
        solid=pet.getchannel('A').point(lambda n:255 if n==255 else 0)
        changed_pet=ImageChops.difference(source_crop.convert('RGB'),pet.convert('RGB'))
        for channel in changed_pet.split(): assert ImageChops.multiply(channel,solid).getbbox() is None
        rendered.append(frame.convert('RGB'))
    rendered[variant*len(pet_frames)].resize((size[0]*display,size[1]*display),Image.Resampling.NEAREST).save(ROOT/f'assembled-{chr(97+variant)}.png')

for a,b in zip(rendered[:len(pet_frames)],rendered[len(pet_frames):]):
    bounds=ImageChops.difference(a,b).getbbox()
    assert bounds and bounds[0]>=rect[0] and bounds[1]>=rect[1] and bounds[2]<=rect[2] and bounds[3]<=rect[3]

rendered[0].save(ROOT/'pilot-swap.png',save_all=True,append_images=rendered[1:],duration=durations*2,
                 loop=0,disposal=0,blend=0,optimize=False)
animation=Image.open(ROOT/'pilot-swap.png');assert animation.is_animated and 2<=animation.n_frames<=len(rendered)
total=0;decoded=[]
for index in range(animation.n_frames):
    animation.seek(index);duration=animation.info['duration']
    decoded.append((total,total+duration,animation.convert('RGB').copy()));total+=duration
assert abs(total-2*sum(durations))<.1
# Lossless APNG may merge repeated pictures; verify every original timestamp, not an arbitrary frame count.
pointer=0;time=0
for expected,duration in zip(rendered,durations*2):
    while time>=decoded[pointer][1]-.001: pointer+=1
    assert decoded[pointer][0]<=time<decoded[pointer][1]
    assert decoded[pointer][2].tobytes()==expected.tobytes()
    time+=duration
proof={'status':'technical-checks-passed-visual-review-required','canvas':list(size),'displayScale':display,
       'nativeMasterSizes':[native_a,native_b],'nativeCrops':[crop_a,crop_b],'zoneRect':list(rect),
       'protectedTablePlantRect':list(protected),'changedPixelBox':list(changed),
       'petSource':'catalog-package/assets/normal/133.gif','petPixelsUnchanged':True,
       'sourcePetFrames':len(pet_frames),'renderedPreviewFrames':len(rendered),'animatedPreviewFrames':animation.n_frames,
       'decodedAnimationMatchesEverySourceTimestamp':True,
       'animatedPreviewTotalMs':total,'outsideZoneIdenticalForEveryPairedFrame':True,
       'method':'Shared master + context-painted chair zone with feathered technical mask; no global room reroll during swap.',
       'maskPolygon':mask_polygon,'maskFeatherNativePx':3,
       'limitations':['Only two chairs and one fixed table/plant tested.','Other zone boundaries/masks not designed.',
                      'Table/plant are explicitly protected from incidental generation changes.',
                      'Pixel grid and contact shadow do not prove artistic coherence.']}
(ROOT/'proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
(ROOT/'index.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>PokéCorner — зональный пилот</title>
<style>body{margin:0;background:#f5f0e4;color:#29372f;font:17px Segoe UI,sans-serif}main{max-width:1440px;margin:auto;padding:28px}h1{margin:0 0 12px}p{line-height:1.5}img{width:100%;height:auto;image-rendering:pixelated;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}a{color:inherit}@media(max-width:800px){.pair{grid-template-columns:1fr}}</style>
<main><h1>Один уголок, два кресла</h1><p>Кресло меняется автоматически. Иви — исходная анимация Black &amp; White. Остальная комната, столик и растение сохраняются.</p>
<img src="pilot-swap.png" alt="Анимированный пилот: смена кресла и настоящий Иви"><p>Малый технический пилот для оценки цельности. Приложение не менялось; полный набор зон ещё не построен.</p>
<div class="pair"><section><h2>Деревянное кресло</h2><a href="assembled-a.png"><img src="assembled-a.png" alt="Первое кресло"></a></section><section><h2>Мягкое кресло</h2><a href="assembled-b.png"><img src="assembled-b.png" alt="Второе кресло"></a></section></div></main></html>''',encoding='utf-8')
print(json.dumps({'frames':animation.n_frames,'milliseconds':total,'changedPixelBox':list(changed),
                  'outsideZoneIdentical':True,'petPixelsUnchanged':True}))
