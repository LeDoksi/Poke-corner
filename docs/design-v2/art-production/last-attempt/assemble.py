"""Last bounded demonstration: detailed room, contextual right choices, independent readable awards."""
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, PngImagePlugin

ROOT=Path(__file__).resolve().parent
SIZE=(1448,582)
meta=PngImagePlugin.PngInfo()
prompts='Technical common-master/contextual-zone composition. Room remains detailed, no480-grid conversion; BW sprite is original3x.\n'
for path in sorted(ROOT.glob('*prompt.txt')):prompts+=path.name+'\n'+path.read_text(encoding='utf-8')+'\n'
meta.add_text('impeccable:prompt',prompts)


def save(im,name):im.save(ROOT/name,pnginfo=meta)


master=Image.open(ROOT/'master.png').convert('RGBA')
assert master.size==SIZE
# The corrected cabinet footprint invalidates old right-zone recipes; publish the coherent master only.
backgrounds=[master]

pack=json.loads((ROOT/'final-assets/manifest.json').read_text(encoding='utf-8'))
rows=pack['trophies']
registry={row['id']:row for row in rows};assert len(registry)==56
anchors=[(664,280),(810,280),(956,280)]
lip_rect=(590,281,1031,288)
lip=master.crop(lip_rect);save(lip,'shelf-frontlip.png')
showcase=['trophy-population-1','trophy-expeditions-1','trophy-care-1']


def awards(background,ids):
    scene=background.copy()
    for item_id,point in zip(ids,anchors):
        if item_id is None:continue
        row=registry[item_id];trophy=Image.open(ROOT/'final-assets'/row['file']).convert('RGBA')
        foot=row['footAnchor'];xy=(round(point[0]-trophy.width*foot[0]),round(point[1]-trophy.height*foot[1]))
        scene.alpha_composite(trophy,xy)
    scene.alpha_composite(lip,lip_rect[:2])
    return scene


gif=Image.open(ROOT.parent.parent/'catalog-package/assets/normal/133.gif')
pets=[];durations=[]
for index in range(gif.n_frames):
    gif.seek(index);pets.append(gif.convert('RGBA').copy());durations.append(gif.info.get('duration',100))
assert len(pets)==67
pet_xy=(731,383)
shadow=Image.new('RGBA',SIZE)
ImageDraw.Draw(shadow).ellipse((778,520,824,530),fill=(47,30,15,66))


def pet_on(background,index):
    pet=pets[index].resize((138,141),Image.Resampling.NEAREST)
    scene=background.copy();scene.alpha_composite(shadow);scene.alpha_composite(pet,pet_xy)
    actual=scene.crop((731,383,869,524)).resize((46,47),Image.Resampling.NEAREST)
    solid=pets[index].getchannel('A').point(lambda a:255 if a==255 else 0)
    for channel in ImageChops.difference(actual.convert('RGB'),pets[index].convert('RGB')).split():
        assert ImageChops.multiply(channel,solid).getbbox() is None
    return scene.convert('RGB')


decorated=[awards(background,showcase) for background in backgrounds]
for index,background in enumerate(decorated):save(pet_on(background,0),f'assembled-{index+1}.png')
save(pet_on(awards(backgrounds[0],['trophy-population-4','trophy-expeditions-4','trophy-care-4']),0),'assembled-final-trophies.png')
frames=[pet_on(decorated[0],index) for index in range(67)]
frames[0].save(ROOT/'animated-room.png',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=0,blend=0,pnginfo=meta)
animation=Image.open(ROOT/'animated-room.png');time=0;source_index=0;source_time=0
for index in range(animation.n_frames):
    animation.seek(index);end=time+animation.info['duration'];actual=animation.convert('RGB')
    while source_index<len(frames) and source_time<end-.001:
        assert source_time>=time-.001 and actual.tobytes()==frames[source_index].tobytes()
        source_time+=durations[source_index];source_index+=1
    time=end
assert source_index==67 and abs(time-sum(durations))<.1
del frames
award_choices=[pet_on(awards(master,ids),0) for ids in [showcase,
    ['trophy-population-4','trophy-expeditions-4','trophy-care-4'],
    ['trophy-special-all-legends','trophy-shiny-4','trophy-nursery-4']]]
award_choices[0].save(ROOT/'trophy-choices.png',save_all=True,append_images=award_choices[1:],duration=2400,loop=0,disposal=0,blend=0,pnginfo=meta)
proof={'status':'fixed-room-design-packet-ready','roomSize':SIZE,'roomCoarsenedToBWGrid':False,
       'displayedRoomStates':1,'modularityStatus':'Full decor customization retired; independent earned awards only.',
       'masterSource':'corrected-source.png','trophyAppearances':56,'archivedSourceAppearances':63,'trophyMaxSize':[132,94],'trophyAnchors':anchors,'shelfLipRect':lip_rect,
       'originalPetFrames':67,'petScale':3,'petOpaqueSourcePixelsUnchanged':True,
       'decodedAnimationExactlyMatchesEverySourceTimestamp':True,'encodedFrames':animation.n_frames,
       'animationMs':time,'animationBytes':(ROOT/'animated-room.png').stat().st_size,
       'finalMode':'One coherent fixed room with independently interchangeable earned awards.',
       'limits':['Earlier150-item decor catalogue is historical.','Earlier four rightcorner combinations are historical and not approved for the corrected master.',
                 'Small faces in crowded final trophy groups remain limited even at larger room scale.','No WPF integration.']}
(ROOT/'proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
(ROOT/'index.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>PokéCorner — последняя попытка</title>
<style>body{margin:0;background:#f4eee3;color:#283529;font:18px Segoe UI,sans-serif}main{max-width:1448px;margin:auto;padding:20px}img{width:100%;height:auto}p{max-width:75ch;line-height:1.55}h2{margin-top:40px}a{color:#275638}a:focus-visible{outline:3px solid #275638;outline-offset:4px}</style>
<main><h1>Итоговая комната</h1><p>Фиксированная детальная сцена и независимые заработанные трофеи. Карниз следует оконной раме, комод рядом со шкафом. Полный магазин декора исключён.</p><img src="animated-room.png" alt="Детальная комната с крупными трофеями и настоящим анимированным Иви">
<h2>Смена трофеев</h2><p>Меняются только отдельные награды. Здесь Иви показан одним исходным кадром. Полная смена мебели для исправленной сцены не заявляется.</p><img src="trophy-choices.png" alt="Независимая смена крупных трофеев">
<h2>Поздние трофеи</h2><img src="assembled-final-trophies.png" alt="Та же комната с финальными ступенями наград"><p><a href="final-assets/previews/album-open.png">Все26 карточек активного альбома</a> · <a href="final-assets/previews/album-locked.png">Закрытые силуэты</a> · <a href="../../final/index.html">Итоговые статичные экраны</a></p><p>56 внешних видов наград,10 цепочек и16 самостоятельных целей. Приложение не менялось; художественный пакет и правила готовы к внедрению.</p></main></html>''',encoding='utf-8')
print(json.dumps({k:proof[k] for k in ['roomSize','displayedRoomStates','encodedFrames','animationMs','animationBytes']}))
