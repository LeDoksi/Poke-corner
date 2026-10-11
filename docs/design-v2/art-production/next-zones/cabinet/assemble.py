"""Technical masks, nearest resize and composition only; no artistic repainting."""
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, PngImagePlugin

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent.parent
PILOT = ART / 'pilot-zones'
TROPHIES = ART / 'trophies'
SIZE = (480, 193)
RECT = (195, 65, 345, 142)
ANCHORS = [(226, 106), (262, 106), (300, 106)]
SLOTS = [(209, 79, 243, 108), (245, 79, 279, 108), (283, 79, 317, 108)]
SHOWCASE = ['trophy-population-1', 'trophy-expeditions-1', 'trophy-care-1']
for folder in ['assets', 'previews']:
    (ROOT / folder).mkdir(exist_ok=True)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(im, path, origin, prompt='Technical derived composition; no image generation.'):
    metadata = PngImagePlugin.PngInfo()
    metadata.add_text('impeccable:prompt', prompt)
    metadata.add_text('impeccable:origin', json.dumps(origin, ensure_ascii=False))
    im.save(path, pnginfo=metadata)

def normalize(path):
    im = Image.open(path).convert('RGBA')
    height = round(im.width * SIZE[1] / SIZE[0])
    assert height <= im.height
    return im.crop((0, 0, im.width, height)).resize(SIZE, Image.Resampling.NEAREST)

master = Image.open(PILOT / 'master.png').convert('RGBA')
assert master.size == SIZE
generation_folder = 'C:/Users/Georgy/.codex/generated_images/01a127e2-a882-78a0-b552-7bb0c81dc54c/'
source_jobs = [('c1-first','exec-7893cfff-ee5f-48ec-bbae-bd369b2d57cd.png','c1-prompt','../../pilot-zones/master.png'),('c1-context','exec-68d96df9-3317-4a97-8d8b-913eadd7c26a.png','c1-correction-prompt','c1-first.png'),('c3-context','exec-c53aaf0f-0746-49c2-9299-eeec9cf6f88a.png','c3-prompt','../../pilot-zones/master.png; c1-context.png geometry reference')]
source_metadata = []
for name,generated,prompt_file,edit_target in source_jobs:
    path = ROOT/'_sources'/f'{name}.png'
    raw = Image.open(path).copy()
    origin = {'tool':'builtin image_gen.imagegen','generatedOriginal':generation_folder+generated,'editTarget':edit_target,'nativeDimensions':raw.size,'promptFile':prompt_file+'.txt','nonDestructive':True}
    save(raw,path,origin,(ROOT/'_sources'/f'{prompt_file}.txt').read_text(encoding='utf-8').strip())
    source_metadata.append(origin | {'workspaceFile':f'_sources/{name}.png','sha256':sha(path)})
(ROOT/'source-metadata.json').write_text(json.dumps(source_metadata,indent=2)+'\n',encoding='utf-8')
rows = json.loads((TROPHIES / 'production-manifest.json').read_text(encoding='utf-8'))['produce']
assert len(rows) == 63 and len({r['id'] for r in rows}) == 63
scale = min(.30, 25 / max(r['logicalSize'][1] for r in rows), 29 / max(r['logicalSize'][0] for r in rows))
core = Image.new('L', SIZE)
polygon = [(199,72),(323,72),(324,74),(323,127),(324,130),(322,135),(199,135),(198,130),(199,127)]
ImageDraw.Draw(core).polygon(polygon, fill=255)
shadow = Image.new('L', SIZE)
ImageDraw.Draw(shadow).polygon([(201,132),(326,132),(338,139),(205,140),(196,136)], fill=255)
mask = ImageChops.lighter(core, ImageChops.lighter(shadow, shadow.filter(ImageFilter.GaussianBlur(1))))
save(mask.crop(RECT), ROOT / '_sources' / 'zone-mask.png', {'method':'bounded cabinet + local floor shadow selection', 'zoneRect':RECT})
pet = Image.open(ART.parent / 'catalog-package/assets/normal/133.gif').convert('RGBA')
assert pet.size == (46,47)
pet_xy = (255,126)
tests = []
produce = []
direct = []
profiles = []
for model in ['c1','c3']:
    source = ROOT / '_sources' / f'{model}-context.png'
    prompt_path = ROOT / '_sources' / ('c1-correction-prompt.txt' if model=='c1' else 'c3-prompt.txt')
    prompt = prompt_path.read_text(encoding='utf-8').strip()
    im = normalize(source)
    patch = im.crop(RECT)
    patch.putalpha(mask.crop(RECT))
    origin = {'source':str(source.relative_to(ROOT)), 'sourceSha256':sha(source), 'master':'../../pilot-zones/master.png', 'masterSha256':sha(PILOT/'master.png'), 'method':'contextual generation then bounded technical mask + nearest resize', 'zoneRect':RECT, 'promptChain':['_sources/c1-prompt.txt','_sources/c1-correction-prompt.txt'] if model=='c1' else ['_sources/c3-prompt.txt']}
    save(patch, ROOT/'assets'/f'{model}-zone.png', origin, prompt)
    scene = master.copy()
    scene.alpha_composite(patch, (RECT[0],RECT[1]))
    diff = ImageChops.difference(scene.convert('RGB'), master.convert('RGB')).getbbox()
    assert diff and RECT[0]<=diff[0]<diff[2]<=RECT[2] and RECT[1]<=diff[1]<diff[3]<=RECT[3]
    for protected in [(0,0,195,193),(345,0,480,193),(0,0,480,65),(0,142,480,193)]:
        assert scene.crop(protected).tobytes() == master.crop(protected).tobytes()
    lip = Image.new('RGBA', SIZE)
    lip.paste(scene.crop((207,106,317,108)), (207,106))
    save(lip.crop(RECT),ROOT/'assets'/f'{model}-frontlip.png',{'source':f'assets/{model}-zone.png','logicalBounds':[207,106,317,108],'method':'technical crop of intrinsic cabinet shelf edge'},prompt)
    full = scene.copy()
    for row in rows:
        trophy_source = TROPHIES / row['output_path']
        native = Image.open(trophy_source).convert('RGBA')
        target = tuple(max(1, round(v * scale)) for v in row['logicalSize'])
        trophy = native.resize(target,Image.Resampling.NEAREST)
        alpha = trophy.getchannel('A').getbbox()
        assert alpha
        for index,(anchor,slot) in enumerate(zip(ANCHORS,SLOTS)):
            xy = (round(anchor[0]-target[0]*row['footAnchor'][0]), round(anchor[1]-target[1]*row['footAnchor'][1]))
            bounds = (xy[0]+alpha[0],xy[1]+alpha[1],xy[0]+alpha[2],xy[1]+alpha[3])
            fits = slot[0]<=bounds[0]<bounds[2]<=slot[2] and slot[1]<=bounds[1]<bounds[3]<=slot[3]
            assert fits,(model,row['id'],index,bounds,slot)
            tests.append({'model':model,'id':row['id'],'slot':index+1,'logicalSize':target,'position':xy,'opaqueBounds':bounds,'slotBounds':slot,'fits':fits})
        if row['id'] in SHOWCASE:
            index = SHOWCASE.index(row['id'])
            anchor = ANCHORS[index]
            xy = (round(anchor[0]-target[0]*row['footAnchor'][0]), round(anchor[1]-target[1]*row['footAnchor'][1]))
            full.alpha_composite(trophy,xy)
            path = ROOT/'assets'/f"{row['id']}-logical.png"
            if not path.exists():
                save(trophy,path,{'source':f'../../trophies/{row["output_path"]}','sourceSha256':sha(trophy_source),'logicalSize':target,'perCabinetScale':scale,'method':'technical nearest-neighbor resize only; art unchanged'})
                direct.append({'id':row['id'],'source_crop':f'../../trophies/{row["output_path"]}','output_path':f'assets/{path.name}','strategy':'technical nearest-neighbor resize of existing production alpha art','dimensions':target,'format':'png','transparency':True,'deviations':'Prior art remains high-detail at small logical size; no artistic repainting.','qa_status':'needs_parent_review'})
    full.alpha_composite(lip)
    for name,assembly in [('empty',scene),('full',full)]:
        save(assembly,ROOT/'previews'/f'{model}-{name}-logical.png',{'master':'../../pilot-zones/master.png','zone':f'assets/{model}-zone.png','shelfFrontlip':f'assets/{model}-frontlip.png' if name=='full' else None,'trophies':SHOWCASE if name=='full' else []})
        assembly.alpha_composite(pet,pet_xy)
        opaque = pet.getchannel('A').point(lambda n:255 if n==255 else 0)
        for channel in ImageChops.difference(assembly.crop((255,126,301,173)).convert('RGB'),pet.convert('RGB')).split():
            assert ImageChops.multiply(channel,opaque).getbbox() is None
        save(assembly.resize((1440,579),Image.Resampling.NEAREST),ROOT/'previews'/f'{model}-{name}-display3.png',{'source':f'{model}-{name}-logical.png','pet':'../../../catalog-package/assets/normal/133.gif','petFrame':0,'petPixelsUnchanged':True,'displayScale':3})
    profiles.append({'id':model.upper(),'zoneRect':RECT,'cabinetBounds':[198,72,325,136],'shelfAnchors':ANCHORS,'slotRects':SLOTS,'shelfFrontlipBounds':[207,106,317,108],'trophyScale':scale,'topPlantAnchor':[307,73],'topPlantMaxSize':[20,18],'paintingReserve':[195,10,340,60],'changedPixelBox':diff})
    for role in ['zone','frontlip']:
        produce.append({'id':f'{model}-{role}','source_crop':f'_sources/{model}-context.png','output_path':f'assets/{model}-{role}.png','strategy':'contextual fullscene edit, bounded technical mask; intrinsic front shelf edge split from source' if role=='frontlip' else 'contextual fullscene edit then bounded technical mask','prompt_used':str(prompt_path.relative_to(ROOT)),'dimensions':[RECT[2]-RECT[0],RECT[3]-RECT[1]],'format':'png','transparency':True,'deviations':'Actual cabinet is lower than initial y57 target; measured top y72, within assigned scene region. Cabinet center x262.5.','qa_status':'needs_parent_review'})

assert len(tests)==378
profile = {'canvas':SIZE,'displayScale':3,'master':'../../pilot-zones/master.png','models':profiles,'maskPolygon':polygon,'shadowMaskFeatherPx':1,'renderOrder':['master','cabinetZone','independentTrophies','cabinetFrontlip','topPlant','separateOriginalPet']}
(ROOT/'profile.json').write_text(json.dumps(profile,indent=2)+'\n',encoding='utf-8')
proof = {'status':'pixel-bounds-tests-passed-not-visual-approval','trophies':63,'models':2,'slotsPerModel':3,'placementsChecked':len(tests),'allPixelBoundsFit':True,'outsideZoneMasterPixelsUnchanged':True,'windowChairTableProtected':True,'petOpaquePixelsUnchanged':True,'petFrameTested':0,'trophyScale':scale,'masterSha256':sha(PILOT/'master.png'),'placements':tests,'limitations':['Only alpha pixel bounds, not 63 visual shelf compositions.','One original BW pet frame preview, no live WPF test.','Other furniture and top plants not composed here.','Existing high-detail trophy art only resized, no new pixel-art conversion.']}
(ROOT/'proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
manifest = {'produce':produce,'direct':direct,'semantic':[{'id':'cabinet-three-slot-composition','implementation':'Compose common 480x193 master then cabinet-zone patch at zoneRect origin; resize each existing trophy by per-model trophyScale, place by source footAnchor at shelfAnchors, then draw independent cabinet-frontlip at same zoneRect origin. Add separate plant using topPlantAnchor and keep original BW pet above room layers. Use integer displayScale3 and nearest/pixelated sampling; no UI raster.','notes':'One shared set of anchors deliberately calibrated from actual two cabinet sources; source figures remain separate and interchangeable.','qa_status':'needs_parent_review'}],'execution_order':['C1 contextual edit','one C1 shelf correction','C3 contextual edit from common master + C1 geometry reference','bounded technical masks','63x3x2 alpha pixel-bounds checks','empty and full showcase previews'],'blockers':[],'assumptions':['Only C1 and C3 small batch; no app code changes.','Two empty niches generated, trophies independently reused.','Catalog fidelity and common assembled review remain parent responsibility.']}
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:proof[k] for k in ['placementsChecked','allPixelBoundsFit','outsideZoneMasterPixelsUnchanged','petOpaquePixelsUnchanged','trophyScale']}))
