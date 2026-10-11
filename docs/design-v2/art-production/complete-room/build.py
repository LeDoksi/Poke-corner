"""Complete registered 150-item room catalogue; technical composition, no artwork repainting."""
import html
import json
from itertools import product
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, PngImagePlugin

ROOT=Path(__file__).resolve().parent
ART=ROOT.parent
SIZE=(480,193)
OUT=ROOT/'previews';OUT.mkdir(exist_ok=True)
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',13)


def data(path):return json.loads(path.read_text(encoding='utf-8'))


def save(im,path,origin):
    meta=PngImagePlugin.PngInfo();meta.add_text('impeccable:prompt',origin)
    im.save(path,pnginfo=meta)


rows=[]
for row in data(ROOT/'surfaces/manifest.json')['assets']:
    rows.append(row)
for row in data(ROOT/'objects/manifest.json')['direct']:
    rows.append({**row,'file':'objects/'+row['output_path'].replace('\\','/')})
for row in data(ROOT/'right/manifest.json')['produce']+data(ROOT/'right/manifest.json')['direct']:
    rows.append({**row,'id':row['id'].removesuffix('-zone'),'category':'furniture' if row['id'].startswith('dresser') else 'beds',
                 'file':'right/'+row['output_path'].replace('\\','/'),'position':row['destAnchor']})
new_furniture={row['id']:row for row in data(ROOT/'left-furniture/manifest.json')['produce']}
for prefix,short in [('table-t','T'),('armchair-a','A'),('cabinet-c','C')]:
    for index in range(1,5):
        item_id=prefix+str(index)
        code=short+str(index)
        if code in new_furniture:
            row=new_furniture[code]
            path=ROOT/'left-furniture'/row['output_path']
            rect=row['zoneRect'];anchors=row['anchors']
        elif short=='A':
            path=ART/'pilot-zones'/f'zone-{chr(96+index)}.png'
            rect=[0,72,225,193];anchors={'floorAnchor':[85,178]}
        elif short=='T':
            path=ART/'next-zones/left'/f'table-t{index}-zone.png'
            rect=[124,107,184,168];anchors={'topPlantAnchor':[151,119 if index==1 else 116]}
        else:
            model='c1' if index==1 else 'c3'
            path=ART/'next-zones/cabinet/assets'/f'{model}-zone.png'
            rect=[195,65,345,142];anchors={'topPlantAnchor':[307,73]}
        rows.append({'id':item_id,'category':'furniture','file':str(path.relative_to(ART)).replace('\\','/'),
                     'fileBase':'art','zoneRect':rect,'position':rect[:2],'anchors':anchors})

registry={r['id']:r for r in rows}
expected=data(ART/'inventory.json')['items']
assert len(rows)==150 and len(registry)==150
assert set(registry)=={r['id'] for r in expected}
images={}
for row in rows:
    base=ART if row.get('fileBase')=='art' else ROOT
    path=base/row['file'];assert path.exists(),path
    im=Image.open(path).convert('RGBA');images[row['id']]=im
    row['dimensions']=list(im.size)
    assert im.width>0 and im.height>0
    if row['category'] not in ['walls','furniture']:
        assert im.getchannel('A').getextrema()[0]==0,row['id']

SLOTS={'windowsill':[88,94],'cabinet-top':[307,73],'floor':[23,172],
       'picture-left':[221,41],'picture-center':[266,41],'picture-right':[311,41],
       'trophy-left':[226,106],'trophy-center':[262,106],'trophy-right':[300,106]}
PET=Image.open(ART.parent/'catalog-package/assets/normal/133.gif')
PET_FRAMES=[];PET_TIMES=[]
for index in range(PET.n_frames):
    PET.seek(index);PET_FRAMES.append(PET.convert('RGBA').copy());PET_TIMES.append(PET.info.get('duration',100))
assert len(PET_FRAMES)==67
shadow=Image.new('RGBA',SIZE)
ImageDraw.Draw(shadow).ellipse((266,170,290,175),fill=(48,30,16,60))


def put(scene,item_id):
    if not item_id:return
    row=registry[item_id];im=images[item_id]
    if item_id.startswith('armchair'):
        im=im.copy();alpha=im.getchannel('A')
        # Context above the actual chair restores no furniture and must not erase the chosen wall finish.
        ImageDraw.Draw(alpha).rectangle((0,0,im.width,14),fill=0);im.putalpha(alpha)
    scene.alpha_composite(im,tuple(row['position']))


def put_object(scene,item_id,slot,anchor=None):
    if not item_id:return
    row=registry[item_id];assert slot in row['useSlots'],(item_id,slot)
    im=images[item_id]
    if slot in row.get('slotSizes',{}):im=im.resize(tuple(row['slotSizes'][slot]),Image.Resampling.NEAREST)
    point=anchor or SLOTS[slot]
    foot=row['footAnchor'];xy=(round(point[0]-im.width*foot[0]),round(point[1]-im.height*foot[1]))
    alpha=im.getchannel('A').getbbox();assert alpha
    assert xy[0]+alpha[0]>=0 and xy[0]+alpha[2]<=480,(item_id,slot,xy)
    assert xy[1]+alpha[1]>=0 and xy[1]+alpha[3]<=193,(item_id,slot,xy)
    scene.alpha_composite(im,xy)


def lip(scene,cabinet):
    model=cabinet[-2:]
    if model in ['c2','c4']:path=ROOT/'left-furniture/assets'/f'cabinet-{model}-frontlip.png'
    else:path=ART/'next-zones/cabinet/assets'/f'{model}-frontlip.png'
    scene.alpha_composite(Image.open(path).convert('RGBA'),(195,65))


DEFAULT={'wall':'wall-plain-cream','chair':'armchair-a1','table':'table-t1','cabinet':'cabinet-c3','dresser':'dresser-d2',
         'curtain':'curtain-linen-cream','rug':'rug-woven-sage','bed':'bed-basket-cream',
         'plants':{'windowsill':'plant-fern-terracotta','table':'plant-succulent-terracotta',
                   'cabinet-top':'plant-flowering-shrub-woven-cream','floor':'plant-monstera-woven-cream'},
         'pictures':['painting-kanto-map','painting-bulbasaur-plants','painting-mew-stars'],
         'trophies':['trophy-population-4','trophy-regions-4','trophy-care-4']}


def scene(config,pet_index=None):
    c={**DEFAULT,**config}
    im=images[c['wall']].copy()
    put(im,c['rug']);put(im,c['curtain'])
    plants=c['plants']
    put_object(im,plants.get('windowsill'),'windowsill')
    for slot,item_id in zip(['picture-left','picture-center','picture-right'],c['pictures']):put_object(im,item_id,slot)
    put(im,c['chair']);put(im,c['table']);put(im,c['cabinet']);put(im,c['dresser'])
    for slot,item_id in zip(['trophy-left','trophy-center','trophy-right'],c['trophies']):put_object(im,item_id,slot)
    lip(im,c['cabinet'])
    put_object(im,plants.get('table'),'table',registry[c['table']]['anchors']['topPlantAnchor'])
    put_object(im,plants.get('cabinet-top'),'cabinet-top')
    put_object(im,plants.get('floor'),'floor')
    put(im,c['bed'])
    if pet_index is not None:
        pet=PET_FRAMES[pet_index];im.alpha_composite(shadow);im.alpha_composite(pet,(255,126))
        solid=pet.getchannel('A').point(lambda a:255 if a==255 else 0)
        for ch in ImageChops.difference(im.crop((255,126,301,173)).convert('RGB'),pet.convert('RGB')).split():
            assert ImageChops.multiply(ch,solid).getbbox() is None
    return im


states={
 'starter':{'chair':None,'dresser':None,'curtain':None,'rug':None,'bed':None,'cabinet':'cabinet-c1',
            'plants':{},'pictures':[None]*3,'trophies':[None]*3},
 'developing':{'table':'table-t2','cabinet':'cabinet-c2','rug':'rug-woven-cream','curtain':'curtain-short-cream',
               'plants':{'table':'plant-pilea-woven-cream'},'pictures':['painting-kanto-map',None,None],
               'trophies':['trophy-care-1',None,None],'bed':'bed-pillow-cream'},
 'furnished':{},
 'alternative':{'wall':'wall-stars-blue','chair':'armchair-a4','table':'table-t4','cabinet':'cabinet-c4','dresser':'dresser-d4',
                'curtain':'curtain-linen-blue','rug':'rug-pokeball-blue','bed':'bed-nest-sage',
                'plants':{'windowsill':'plant-orchid-woven-cream','table':'plant-cactus-terracotta',
                          'cabinet-top':'plant-pilea-woven-cream','floor':'plant-ficus-terracotta'},
                'pictures':['painting-johto-map','painting-eevee-forest','painting-snorlax-meadow'],
                'trophies':['trophy-shiny-4','trophy-special-all-legends','trophy-special-retrain']}}
for name,config in states.items():
    save(scene(config,0).resize((1440,579),Image.Resampling.NEAREST),OUT/(name+'.png'),
         'Actual 150-item registered catalogue composition; original BW Eevee frame0. '+json.dumps(config,ensure_ascii=False))

# Every item gets an actual furnished-room preview; this is finite coverage, not a claim about millions of combinations.
by_category={}
for row in rows:
    category=row['category'];by_category.setdefault(category,[]).append(row)
    c={}
    if category=='furniture':
        key='chair' if row['id'].startswith('armchair') else row['id'].split('-')[0]
        c[key]=row['id']
    elif category=='plants':c['plants']={**DEFAULT['plants'],('floor' if row['sizeFamily']=='floor' else 'table'):row['id']}
    elif category=='paintings':c['pictures']=[row['id']]*3
    elif category=='trophies':c['trophies']=[row['id']]*3
    else:c[{'walls':'wall','curtains':'curtain','rugs':'rug','beds':'bed'}[category]]=row['id']
    save(scene(c,0),OUT/(row['id']+'.png'),'Single catalogue item in fully composed registered room: '+row['id'])

for category,items in by_category.items():
    sheet=Image.new('RGB',(960,214*((len(items)+1)//2)),'#f5f0e4')
    for index,row in enumerate(items):
        x=(index%2)*480;y=(index//2)*214
        sheet.paste(Image.open(OUT/(row['id']+'.png')).convert('RGB'),(x,y))
        ImageDraw.Draw(sheet).text((x+8,y+194),row['id'],font=FONT,fill='#29372f')
    save(sheet,OUT/(category+'-in-room.png'),'All '+category+' variants as actual registered room assemblies; no artwork repainting.')

tests=0
for chair,table,cabinet,dresser in product(range(1,5),repeat=4):
    scene({'chair':f'armchair-a{chair}','table':f'table-t{table}','cabinet':f'cabinet-c{cabinet}','dresser':f'dresser-d{dresser}'},0)
    tests+=1
plant_checks=0
for row in by_category['plants']:
    for slot in row['useSlots']:
        for table in (range(1,5) if slot=='table' else [1]):
            c={'table':f'table-t{table}','plants':{slot:row['id']}}
            scene(c,0);plant_checks+=1

frames=[];times=[]
for config in states.values():
    for index in range(len(PET_FRAMES)):
        frames.append(scene(config,index).convert('RGB'));times.append(PET_TIMES[index])
meta=PngImagePlugin.PngInfo();meta.add_text('impeccable:prompt','Four actual complete catalogue room states; separate original BW67-frame Eevee preserved at every source timestamp.')
frames[0].save(OUT/'room-tour.png',save_all=True,append_images=frames[1:],duration=times,loop=0,disposal=0,blend=0,pnginfo=meta)
apng=Image.open(OUT/'room-tour.png');decoded=[];total=0
for index in range(apng.n_frames):
    apng.seek(index);duration=apng.info['duration'];decoded.append((total,total+duration,apng.convert('RGB').copy()));total+=duration
pointer=0;at=0
for expected,duration in zip(frames,times):
    while at>=decoded[pointer][1]-.001:pointer+=1
    assert decoded[pointer][2].tobytes()==expected.tobytes();at+=duration
assert abs(total-sum(times))<.1
report={'status':'complete-interior-art-catalogue','reviewDocument':'QA.md','expected':150,'registered':150,
        'counts':{c:len(items) for c,items in by_category.items()},'nativeCanvas':SIZE,'displayScale':3,
        'furnitureCombinationsChecked':tests,'plantSlotChecks':plant_checks,'actualItemRoomPreviews':150,
        'sourcePetFrames':len(PET_FRAMES),'tourOriginalTimestampFrames':len(frames),'tourEncodedFrames':apng.n_frames,
        'tourDurationMs':total,'tourBytes':(OUT/'room-tour.png').stat().st_size,
        'tourDecodedExact':True,'opaquePetSourceExact':True,'renderOrder':['wall','rug','curtains','windowPlant',
        'paintings','chair','table','cabinet','dresser','trophies','shelfLip','surfacePlants','floorPlant','bed','pet'],
        'limits':['No WPF/app integration.','Finite catalogue,slot and256furniture checks do not exhaust all decor combinations.',
                  'Small native map/trophy artwork preserves silhouette/color but not every source microdetail.']}
export=ROOT/'assets';export.mkdir(exist_ok=True)
locked=export/'locked';locked.mkdir(exist_ok=True)
for row in rows:
    im=images[row['id']].copy()
    if row['id'].startswith('armchair'):
        alpha=im.getchannel('A');ImageDraw.Draw(alpha).rectangle((0,0,im.width,14),fill=0);im.putalpha(alpha)
    origin=im.info.get('impeccable:prompt','Source artwork provenance embedded in original registered file.')
    save(im,export/(row['id']+'.png'),'Canonical technical export, no RGB repainting. Source: '+row['file']+'\n'+origin)
    row['sourceFile']=row['file'];row['sourceFileBase']=row.pop('fileBase','complete-room')
    row['file']='assets/'+row['id']+'.png'
    assert Image.open(ROOT/row['file']).convert('RGBA').tobytes()==im.tobytes()
    if row['category']=='trophies':
        mask=Image.new('RGBA',im.size,(255,255,255,0));mask.putalpha(im.getchannel('A'))
        path=locked/(row['id']+'-mask.png')
        save(mask,path,'Technical opacity mask: exact unchanged alpha channel of '+row['id']+'.png; tint with UI brush for a locked trophy silhouette. No colored artwork repainting.')
        assert Image.open(path).getchannel('A').tobytes()==im.getchannel('A').tobytes()
        row['lockedMask']='assets/locked/'+path.name
for model in ['c1','c2','c3','c4']:
    if model in ['c2','c4']:path=ROOT/'left-furniture/assets'/f'cabinet-{model}-frontlip.png'
    else:path=ART/'next-zones/cabinet/assets'/f'{model}-frontlip.png'
    im=Image.open(path).convert('RGBA')
    save(im,export/f'cabinet-{model}-frontlip.png','Technical canonical export of exact shelf-edge pixels from '+str(path.relative_to(ART)))
save(Image.open(ROOT/'master-empty.png').convert('RGBA'),export/'room-base.png','Common empty architecture restored within bounded footprints. Exact source generation: empty-prompt.txt')
report['canonicalItemFiles']=150;report['technicalBaseAndLips']=5;report['lockedTrophyMasks']=63
(ROOT/'inventory.json').write_text(json.dumps({'report':report,'items':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'proof.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
labels={'furniture':'Мебель','beds':'Лежанки','plants':'Растения','paintings':'Картины','trophies':'Трофеи','curtains':'Шторы','rugs':'Ковры','walls':'Стены'}
sections=''.join('<section><h2>'+labels[cat]+'</h2><div class="items">'+''.join(
    '<figure><a href="previews/'+html.escape(r['id'])+'.png"><img loading="lazy" src="previews/'+html.escape(r['id'])+'.png" alt="'+html.escape(r['id'])+' в комнате"></a><figcaption>'+html.escape(r['id'])+'</figcaption></figure>' for r in items)+'</div></section>' for cat,items in by_category.items())
(ROOT/'index.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>PokéCorner — полный интерьер</title>
<style>body{margin:0;background:#f5f0e4;color:#29372f;font:17px Segoe UI,sans-serif}main{max-width:1440px;margin:auto;padding:24px}p{line-height:1.55;max-width:75ch}img{width:100%;height:auto;image-rendering:pixelated}h2{margin-top:48px}figure{margin:0}figcaption{padding:8px 0 20px}a{color:#294e35}a:focus-visible{outline:3px solid #294e35;outline-offset:4px}.items{display:grid;grid-template-columns:1fr 1fr;gap:20px}@media(max-width:850px){.items{grid-template-columns:1fr}}</style>
<main><h1>Комната PokéCorner</h1><p>Полный интерьерный каталог: 87 вариантов декора и 63 трофея. Мебель и предметы выбираются отдельно; ниже каждый показан в собранной комнате. Игрового интерактива на этой странице нет.</p><img src="previews/room-tour.png" alt="Четыре собранных состояния комнаты с исходной анимацией Иви"><p>Стартовая, постепенно обжитая, заполненная и альтернативная комната. Анимация питомца сохранена из набора Black &amp; White.</p>'''+sections+'</main></html>',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
