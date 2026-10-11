"""Actual contextual art crops and bounded alpha patches; technical operations only."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter
from prepare import ROOT, ART, SIZE, FILES, save

ASSETS = ROOT/'assets'
PREVIEWS = ROOT/'previews'
ASSETS.mkdir(exist_ok=True)
PREVIEWS.mkdir(exist_ok=True)
MASTER = ART/'pilot-zones/master.png'
EMPTY = ART/'next-zones/left/master-no-plant.png'
master = Image.open(MASTER).convert('RGBA')
empty = Image.open(EMPTY).convert('RGBA')
assert master.size == empty.size == SIZE

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def bounded_mask(polygon, rect, feather=1):
    mask = Image.new('L',SIZE)
    ImageDraw.Draw(mask).polygon(polygon,fill=255)
    mask = ImageChops.lighter(mask,mask.filter(ImageFilter.GaussianBlur(feather)))
    bounded = Image.new('L',SIZE)
    bounded.paste(mask.crop(rect),rect[:2])
    return bounded

def outside_equal(scene, base, rect):
    delta = ImageChops.difference(scene.convert('RGB'),base.convert('RGB'))
    ImageDraw.Draw(delta).rectangle((rect[0],rect[1],rect[2]-1,rect[3]-1),fill=(0,0,0))
    assert delta.getbbox() is None

table_rect=(124,107,184,168)
chair_rect=(0,72,225,193)
cabinet_rect=(195,65,345,142)
table_mask=bounded_mask([(128,114),(140,109),(167,109),(177,113),(180,128),(180,151),(176,159),(166,164),(129,164),(127,150)],table_rect,1)
chair_mask=bounded_mask([(44,88),(49,85),(100,85),(110,93),(111,104),(127,109),(128,153),(125,162),(116,169),(105,175),(67,178),(42,169),(36,160),(39,131),(43,110)],chair_rect,2)
# Hard table protection prevents the chair context from restoring or altering independent table/plant pixels.
protected=(128,92,187,168)
ImageDraw.Draw(chair_mask).rectangle((protected[0],protected[1],protected[2]-1,protected[3]-1),fill=0)
cabinet_mask=bounded_mask([(199,72),(323,72),(325,75),(324,128),(324,132),(322,135),(199,135),(198,130),(198,76)],cabinet_rect,0)
shadow=bounded_mask([(199,132),(326,132),(339,140),(205,141),(196,136)],cabinet_rect,1)
cabinet_mask=ImageChops.lighter(cabinet_mask,shadow)

def register_cabinet(im,key):
    # Native source pixel bands are resized to the shared shelf and floor coordinates; no recolor/repaint.
    bounds={'c2':(195,70,317,133),'c4':(196,72,323,133)}[key]
    bands={'c2':[(70,101,72,106),(101,103,106,108),(103,133,108,136)],
           'c4':[(72,104,72,106),(104,106,106,108),(106,133,108,136)]}[key]
    registered=im.copy()
    for sy0,sy1,dy0,dy1 in bands:
        part=im.crop((bounds[0],sy0,bounds[2],sy1)).resize((127,dy1-dy0),Image.Resampling.NEAREST)
        registered.paste(part,(198,dy0))
    return registered,{'sourceObjectBounds':bounds,'targetObjectBounds':[198,72,325,136],
                       'sourceTargetVerticalBands':bands,'method':'nearest source-band registration; intrinsic shelf RGB unchanged except sampling'}

rows=[]
scenes={}
proof={'canvas':list(SIZE),'displayScale':3,'outsideZoneEquality':{},'chairTableProtection':{},
       'alphaBounds':{},'petFramesChecked':0,'cabinetTrophyPlacementsChecked':0}
for key in FILES:
    source=ROOT/'_sources'/f'{key}-context.png'
    logical=ROOT/'_sources'/f'{key}-logical.png'
    prompt=(ROOT/'_sources'/f'{key}-prompt.txt').read_text(encoding='utf-8').strip()
    im=Image.open(logical).convert('RGBA')
    registration=None
    if key.startswith('t'):
        role='table';rect=table_rect;mask=table_mask;base=empty
        anchors={'topPlantAnchor':[151,120] if key=='t3' else [151,116],
                 'floorAnchor':[151,150] if key=='t3' else [151,155], 'maxTopPlantLogicalSize':[25,29]}
    elif key.startswith('a'):
        role='armchair';rect=chair_rect;mask=chair_mask;base=empty
        anchors={'floorAnchor':[85,178], 'objectContactApprox':[85,163], 'protectedTablePlantRect':list(protected)}
    else:
        role='cabinet';rect=cabinet_rect;mask=cabinet_mask;base=master
        im,registration=register_cabinet(im,key)
        anchors={'floorAnchor':[262,136], 'shelfAnchors':[[226,106],[262,106],[300,106]],
                 'slotRects':[[209,79,243,108],[245,79,279,108],[283,79,317,108]],
                 'shelfFrontlipBounds':[207,106,317,108],'topPlantAnchor':[307,73],'topPlantMaxSize':[20,18]}
    filename=f'{role}-{key}-zone.png'
    origin={'tool':'builtin image_gen.imagegen','source':f'_sources/{key}-context.png','sourceSha256':sha(source),
            'editTarget':str((MASTER if key.startswith('c') else EMPTY).relative_to(ART)),
            'zoneRect':list(rect),'cropMethod':'native nearest normalization, actual contextual region alpha mask',
            'registration':registration,'protectedTablePlantRect':list(protected) if key.startswith('a') else None}
    patch=im.crop(rect)
    patch.putalpha(mask.crop(rect))
    save(patch,ASSETS/filename,prompt,origin)
    save(mask.crop(rect),ROOT/'_sources'/f'{key}-zone-mask.png',prompt,origin)
    assert patch.size==(rect[2]-rect[0],rect[3]-rect[1])
    assert patch.getchannel('A').getextrema()==(0,255)
    scene=base.copy();scene.alpha_composite(patch,rect[:2])
    assert scene.size==SIZE
    outside_equal(scene,base,rect)
    proof['outsideZoneEquality'][key]=True
    proof['alphaBounds'][key]=list(patch.getchannel('A').getbbox())
    if key.startswith('a'):
        assert scene.crop(protected).tobytes()==base.crop(protected).tobytes()
        proof['chairTableProtection'][key]=True
    save(scene,PREVIEWS/f'{key}-logical.png',prompt,origin)
    save(scene.resize((1440,579),Image.Resampling.NEAREST),PREVIEWS/f'{key}-display3.png',prompt,origin)
    scenes[key]=scene
    rows.append({'id':key.upper(),'source_crop':f'_sources/{key}-context.png','output_path':f'assets/{filename}',
                 'strategy':'contextual full-room edit then true alpha bounded zone patch',
                 'prompt_used':f'_sources/{key}-prompt.txt','dimensions':list(patch.size),
                 'logicalDimensions':list(patch.size),'roomCanvas':list(SIZE),'format':'png','transparency':True,
                 'zoneRect':list(rect),'anchors':anchors,'cropMethod':origin['cropMethod'],
                 'provenance':origin,'deviations':'Cabinet source bands technically registered to exact common shelf/floor grid.' if registration else 'Raw contextual surroundings may drift; only bounded local patch ships.',
                 'qa_status':'needs_parent_review'})
    if key.startswith('c'):
        lip=Image.new('RGBA',patch.size)
        lip.paste(scene.crop((207,106,317,108)),(207-rect[0],106-rect[1]))
        lipname=f'cabinet-{key}-frontlip.png'
        save(lip,ASSETS/lipname,prompt,origin|{'cropMethod':'actual registered intrinsic shelf lip crop'})
        rows.append({'id':key.upper()+'-frontlip','source_crop':f'_sources/{key}-context.png',
                     'output_path':f'assets/{lipname}','strategy':'intrinsic source shelf edge split for trophy occlusion',
                     'prompt_used':f'_sources/{key}-prompt.txt','dimensions':list(lip.size),'logicalDimensions':list(lip.size),
                     'format':'png','transparency':True,'zoneRect':list(rect),'cropMethod':'source lip pixels only',
                     'provenance':origin,'deviations':'Same technical registration as cabinet zone.','qa_status':'needs_parent_review'})

# Existing canonical BW frames remain separate and unchanged; verify every frame against every patch preview.
petfile=ART.parent/'catalog-package/assets/normal/133.gif'
pet=Image.open(petfile)
petcount=pet.n_frames
for index in range(petcount):
    pet.seek(index);frame=pet.convert('RGBA')
    solid=frame.getchannel('A').point(lambda n:255 if n==255 else 0)
    for key,scene in scenes.items():
        occupied=scene.copy();occupied.alpha_composite(frame,(255,126))
        for channel in ImageChops.difference(occupied.crop((255,126,301,173)).convert('RGB'),frame.convert('RGB')).split():
            assert ImageChops.multiply(channel,solid).getbbox() is None
        proof['petFramesChecked']+=1
        if index==0:
            save(occupied.resize((1440,579),Image.Resampling.NEAREST),PREVIEWS/f'{key}-pet-display3.png',
                 'Unmodified canonical BW pet frame0 composed after room patches.',{'source':str(petfile),'petFrame':0,'scene':key})

# Real trophy alpha bounds, all 63 assets in each of three shared slots in both new cabinet models.
trophyroot=ART/'trophies'
troprows=json.loads((trophyroot/'production-manifest.json').read_text(encoding='utf-8'))['produce']
assert len(troprows)==63
scale=json.loads((ART/'next-zones/cabinet/profile.json').read_text(encoding='utf-8'))['models'][0]['trophyScale']
showcase=['trophy-population-1','trophy-expeditions-1','trophy-care-1']
for key in ['c2','c4']:
    full=scenes[key].copy()
    for row in troprows:
        image=Image.open(trophyroot/row['output_path']).convert('RGBA')
        target=tuple(max(1,round(v*scale)) for v in row['logicalSize'])
        image=image.resize(target,Image.Resampling.NEAREST)
        opaque=image.getchannel('A').getbbox();assert opaque
        for slot,anchor in zip([[209,79,243,108],[245,79,279,108],[283,79,317,108]],[[226,106],[262,106],[300,106]]):
            xy=(round(anchor[0]-target[0]*row['footAnchor'][0]),round(anchor[1]-target[1]*row['footAnchor'][1]))
            bb=(xy[0]+opaque[0],xy[1]+opaque[1],xy[0]+opaque[2],xy[1]+opaque[3])
            assert slot[0]<=bb[0]<bb[2]<=slot[2] and slot[1]<=bb[1]<bb[3]<=slot[3]
            proof['cabinetTrophyPlacementsChecked']+=1
        if row['id'] in showcase:
            anchor=[[226,106],[262,106],[300,106]][showcase.index(row['id'])]
            xy=(round(anchor[0]-target[0]*row['footAnchor'][0]),round(anchor[1]-target[1]*row['footAnchor'][1]))
            full.alpha_composite(image,xy)
    full.alpha_composite(Image.open(ASSETS/f'cabinet-{key}-frontlip.png').convert('RGBA'),cabinet_rect[:2])
    save(full.resize((1440,579),Image.Resampling.NEAREST),PREVIEWS/f'{key}-three-trophies-display3.png',
         'Technical assembly of registered cabinet and three pre-existing independent trophies.',{'model':key,'scale':scale,'trophies':showcase})

contact=Image.new('RGB',(1440,579*3),'#f5f0e4')
for index,key in enumerate(FILES):
    contact.paste(scenes[key].resize((720,290),Image.Resampling.NEAREST).convert('RGB'),((index%2)*720,(index//2)*579))
save(contact,PREVIEWS/'six-states-contact.png','Six native room states technical contact sheet.',{'ids':list(FILES)})
proof['masterDimensions']=list(master.size)
proof['sourcePetFrames']=petcount
proof['trophyScale']=scale
proof['status']='technical-checks-passed-awaiting-final-visual-comparison'
(ROOT/'proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
manifest={'produce':rows,'direct':[],
          'semantic':[{'id':'registered-furniture-room-composition',
                       'implementation':'Compose shared 480x193 master first; for chair/table use next-zones/left/master-no-plant.png. Paste each alpha zone at zoneRect x/y with integer nearest sampling. Place independent table plants by per-model topPlantAnchor. For cabinets paste three independent trophy sprites by footAnchor at common shelfAnchors, then cabinet-frontlip at same zoneRect. Draw original BW pet after room/decor layers. Scale display3 to1440x579, preserving native pixels.',
                       'notes':'Chair masks hard-exclude the entire original table/plant rectangle. Alpha zones are contextual room overlays, not portable studio furniture cutouts. Cabinet band registration preserves intrinsic source art and standard trophy geometry.',
                       'qa_status':'needs_parent_review'}],
          'execution_order':['T3 contextual edit','T4 contextual edit','A3 contextual edit','A4 contextual edit','C2 contextual edit','C4 contextual edit','technical crop/mask/source registration','bounds and canonical pet/trophy checks','visual patch comparison'],
          'blockers':[],
          'assumptions':['Only six assigned missing furniture models produced; no app code changes.',
                         'Exact source prompts embedded with Pillow PNG text because repo embed-prompt.mjs is absent.',
                         'Parent composes other room layers and furniture combinations.',
                         'DisplayScale3 is nearest sampling; sources saved non-destructively.']}
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'models':len(FILES),'assets':len(rows),'petFrameCompositions':proof['petFramesChecked'],
                  'trophyPlacements':proof['cabinetTrophyPlacementsChecked'],'outsideAllZonesUnchanged':True,'chairTablesProtected':True}))
