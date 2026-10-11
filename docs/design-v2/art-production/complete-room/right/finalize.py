"""Technical masks, nearest registration, alpha-derived shadows and combination proofs."""
import json, hashlib, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageChops, ImageFilter, PngImagePlugin

ROOT=Path(__file__).resolve().parent
ART=ROOT.parent.parent
SRC=ROOT/'_sources';OUT=ROOT/'assets';PRE=ROOT/'previews'
MASTER=Image.open(ART/'pilot-zones/master.png').convert('RGBA')
SIZE=(480,193);ZONE=(352,76,451,149);BEDZONE=(413,132,479,172)
EMBED='C:/Users/Georgy/.agents/skills/impeccable/scripts/embed-prompt.mjs'
MODELS={
 'd1':{'polygon':[(350,85),(356,82),(420,82),(425,85),(425,89),(423,89),(423,135),(420,135),(420,129),(357,129),(357,135),(353,135),(353,89),(350,89)],'targetWidth':76,'top':87,'left':358},
 'd2':{'polygon':[(332,94),(339,91),(413,91),(414,94),(413,96),(413,125),(411,128),(411,131),(408,131),(408,128),(406,128),(406,133),(402,133),(402,128),(337,128),(336,132),(333,132),(333,128),(332,128)],'targetWidth':83,'top':99,'left':357},
 'd3':{'polygon':[(353,66),(364,63),(397,63),(397,66),(396,68),(396,121),(395,125),(394,125),(394,128),(391,129),(391,126),(388,127),(388,133),(384,133),(384,127),(359,125),(359,132),(356,132),(355,125),(353,125),(353,122),(354,122),(354,68),(353,68)],'targetWidth':40,'top':78,'left':385},
 'd4':{'polygon':[(328,94),(336,91),(430,91),(430,124),(426,129),(423,129),(423,135),(418,135),(418,130),(334,130),(334,134),(329,134),(329,129),(328,129)],'targetWidth':86,'top':103,'left':357},
}

def embed(path,promptfile):
 subprocess.run(['rtk','proxy','node',EMBED,str(path),'--prompt-file',str(promptfile)],check=True,capture_output=True)

def shadow_from_alpha(sprite,width,height,opacity):
 alpha=sprite.getchannel('A').resize((width,height),Image.Resampling.BILINEAR)
 alpha=alpha.point(lambda a:round(a*opacity/255)).filter(ImageFilter.GaussianBlur(.6))
 shadow=Image.new('RGBA',(width,height),(51,29,15,0));shadow.putalpha(alpha)
 return shadow

produce=[];direct=[];dressers={};beds={};sources=[]
for model,spec in MODELS.items():
 raw=SRC/f'dresser-{model}-context.png';pf=SRC/f'dresser-{model}-prompt.txt';embed(raw,pf)
 full=Image.open(raw).convert('RGBA').resize(SIZE,Image.Resampling.NEAREST)
 mask=Image.new('L',SIZE,0);ImageDraw.Draw(mask).polygon(spec['polygon'],fill=255)
 full.putalpha(mask);bounds=mask.getbbox();obj=full.crop(bounds)
 width=spec['targetWidth'];height=round(obj.height*width/obj.width)
 obj=obj.resize((width,height),Image.Resampling.NEAREST)
 # Register all front feet at one floor depth; texture is never painted or recolored.
 xy=(spec['left'],141-height)
 patch=Image.new('RGBA',(ZONE[2]-ZONE[0],ZONE[3]-ZONE[1]))
 sh=shadow_from_alpha(obj,width+8,6,105)
 patch.alpha_composite(sh,(xy[0]-ZONE[0]-1,140-ZONE[1]))
 patch.alpha_composite(obj,(xy[0]-ZONE[0],xy[1]-ZONE[1]))
 out=OUT/f'dresser-{model}-zone.png';patch.save(out);embed(out,pf)
 obj.save(SRC/f'dresser-{model}-cutout.png');mask.save(SRC/f'dresser-{model}-mask.png')
 dressers[model]=patch
 row={'id':f'dresser-{model}-zone','source_crop':str(raw.relative_to(ROOT)),'sourceOrigin':'pilot-zones/master.png accepted common-room edit target',
 'output_path':str(out.relative_to(ROOT)),'strategy':'contextual scene edit; technical silhouette mask, uniform nearest normalization, alpha-derived contact shadow',
 'prompt_used':pf.read_text(encoding='utf-8').strip(),'prompt_file':str(pf.relative_to(ROOT)),
 'dimensions':list(patch.size),'format':'png','transparency':True,'globalZoneRect':list(ZONE),'destAnchor':[ZONE[0],ZONE[1]],
 'objectBounds':[xy[0],xy[1],xy[0]+width,141],'footAnchor':[xy[0]+width/2,141],'topPlantAnchor':[xy[0]+width*.72,xy[1]+3],
 'topPlantMaxSize':[18,18],'technicalSourcePolygon':spec['polygon'],'deviations':['Generator placement registered to shared right-wall reserve using uniform nearest sampling; global room reroll pixels discarded.'],'qa_status':'accepted','qa_notes':'Compared with accepted room and contextual source; whole models, intended drawer counts, empty tops, warm light and side-floor contact fit; 28 static room combinations reviewed.'}
 produce.append(row);sources.append({'id':row['id'],'raw':str(raw.relative_to(ROOT)),'rawSize':list(Image.open(raw).size),'exactPrompt':str(pf.relative_to(ROOT))})

old=json.loads((ART/'beds/asset-manifest.json').read_text(encoding='utf-8'))
for family in ['pillow','basket','nest']:
 for color in ['cream','sage']:
  aid=f'bed-{family}-{color}';src=ART/'beds/assets'/f'{aid}.png';im=Image.open(src).convert('RGBA')
  metadata=next(r for r in old['produce'] if r['id']==aid)
  pf=SRC/f'{aid}-prompt.txt';pf.write_text(metadata['prompt_used']+'\n',encoding='utf-8')
  # Existing standalone art is direct; the only image operations are alpha crop/nearest resize.
  im=im.crop(im.getchannel('A').getbbox());width=58;height=round(im.height*width/im.width)
  im=im.resize((width,height),Image.Resampling.NEAREST);xy=(417,166-height)
  patch=Image.new('RGBA',(BEDZONE[2]-BEDZONE[0],BEDZONE[3]-BEDZONE[1]))
  sh=shadow_from_alpha(im,62,5,82);patch.alpha_composite(sh,(3,165-BEDZONE[1]))
  patch.alpha_composite(im,(xy[0]-BEDZONE[0],xy[1]-BEDZONE[1]))
  out=OUT/f'{aid}.png';patch.save(out);embed(out,pf);beds[aid]=patch
  direct.append({'id':aid,'source_crop':str(src.relative_to(ART)),'sourceOrigin':metadata['prompt_file'],
   'output_path':str(out.relative_to(ROOT)),'strategy':'prior standalone production alpha art; technical alpha crop, uniform nearest resize, alpha-derived contact shadow',
   'prompt_used':metadata['prompt_used'],'prompt_file':str(pf.relative_to(ROOT)),'dimensions':list(patch.size),'format':'png','transparency':True,
   'globalZoneRect':list(BEDZONE),'destAnchor':[BEDZONE[0],BEDZONE[1]],'objectBounds':[xy[0],xy[1],xy[0]+width,166],
   'footAnchor':[446,166],'deviations':[],'qa_status':'accepted','qa_notes':'Compared with original standalone source and accepted room; empty silhouettes/palettes retained, six independent variants reviewed in all four dresser combinations.'})

pet=Image.open(ART/'pilot-zones/eevee-reference.png').convert('RGBA')
petxy=(278-round(pet.width/2),173-pet.height)
sheet=Image.new('RGB',(7*282,4*250),'#f5efdf');sd=ImageDraw.Draw(sheet)
checks=[];variants=[('none',None)]+list(beds.items())
for row,(model,patch) in enumerate(dressers.items()):
 for col,(bed,bedpatch) in enumerate(variants):
  bg=MASTER.copy();bg.alpha_composite(patch,ZONE[:2])
  if bedpatch is not None:bg.alpha_composite(bedpatch,BEDZONE[:2])
  allowed=Image.new('L',SIZE,255);ad=ImageDraw.Draw(allowed);ad.rectangle((ZONE[0],ZONE[1],ZONE[2]-1,ZONE[3]-1),fill=0)
  if bedpatch is not None:ad.rectangle((BEDZONE[0],BEDZONE[1],BEDZONE[2]-1,BEDZONE[3]-1),fill=0)
  diff=ImageChops.difference(bg.convert('RGB'),MASTER.convert('RGB'))
  for channel in diff.split():assert ImageChops.multiply(channel,allowed).getbbox() is None
  assert bg.crop((255,126,301,173)).tobytes()==MASTER.crop((255,126,301,173)).tobytes()
  assert bg.crop((195,65,345,142)).tobytes()==MASTER.crop((195,65,345,142)).tobytes()
  scene=bg.copy();scene.alpha_composite(pet,petxy)
  label=f'{model}-{bed}';scene.save(PRE/f'{label}-logical.png')
  scene.resize((1440,579),Image.Resampling.NEAREST).save(PRE/f'{label}-display3.png')
  crop=scene.crop((345,70,480,179)).resize((270,218),Image.Resampling.NEAREST)
  sheet.paste(crop,(col*282+6,row*250+24));sd.text((col*282+6,row*250+5),label,fill='#2b241c')
  checks.append({'dresser':model,'bed':bed,'outsideZoneUnchanged':True,'cabinetReserveUnchanged':True,'petReserveUnchanged':True})
sheet.save(PRE/'28-combinations-right-contact.png')
# Every output is a bounded true-alpha zone, every combination uses the original master.
for p in OUT.glob('*.png'):
 a=Image.open(p).getchannel('A');lo,hi=a.getextrema();assert lo==0 and hi>=250;assert a.getbbox()
manifest={'canvas':[480,193],'displayScale':3,'master':'../../pilot-zones/master.png','produce':produce,'direct':direct,
 'semantic':[{'id':'right-zone-composition','implementation':'Compose common master, cabinet and trophies, dresser alpha patch at globalZoneRect origin, then optional empty bed patch at its independent origin; draw real BW pet last. Keep WPF Canvas or HTML scene at 480x193 logical pixels, integral nearest displayScale3. Use model-specific topPlantAnchor and max size for a separate dresser plant. No bed is baked into dresser, and none means skip bed layer.','notes':'Dresser and bed contact shadows are already in their independent alpha patches; do not double-shadow. Bed partly overlaps right front foot region by depth, with bed foreground drawn later. Parent owns common-full-room integration.','qa_status':'accepted'}],
 'execution_order':['four contextual dresser edits D1/D2/D3/D4','technical alpha masks and shared floor registration','six prior standalone bed nearest normalizations','28 common-master combination previews and outside-zone assertions'],
 'blockers':[],'assumptions':['No app or root documentation touched.','Alpha-derived shadows are renderer geometry, not painted new artwork.','480x193 logical sprites are intentionally nearest-scaled x3, raw contextual renders retain full source resolution.','Final artistic acceptance remains parent common-room review.']}
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'source-metadata.json').write_text(json.dumps({'sources':sources,'bedOrigins':'../../beds/asset-manifest.json','masterSha256':hashlib.sha256((ART/'pilot-zones/master.png').read_bytes()).hexdigest()},indent=2)+'\n',encoding='utf-8')
(ROOT/'proof.json').write_text(json.dumps({'combinations':28,'assets':10,'allOutputsTrueAlpha':True,'checks':checks,'petSource':'pilot-zones/eevee-reference.png; original BW frame','limits':['One static original pet frame only; no WPF execution.','Artistic verification is separate from numeric assertions.']},indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':len(produce)+len(direct),'previews':len(checks),'zoneRect':ZONE,'bedZoneRect':BEDZONE,'outsideZoneUnchanged':True}))
