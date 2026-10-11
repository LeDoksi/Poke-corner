"""Normalize existing fabric/floor art and registered wall plates without repainting."""
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, PngImagePlugin

ROOT=Path(__file__).resolve().parent
ART=ROOT.parent.parent
SIZE=(480,193)
assets=ROOT/'assets'
assets.mkdir(exist_ok=True)


def normalize(path):
    im=Image.open(path).convert('RGBA')
    return im.crop((0,0,im.width,round(im.width*193/480))).resize(SIZE,Image.Resampling.NEAREST)


def save(im,path,origin):
    meta=PngImagePlugin.PngInfo()
    meta.add_text('impeccable:prompt',origin)
    im.save(path,pnginfo=meta)


original=Image.open(ART/'pilot-zones/master.png').convert('RGBA')
restored=normalize(ROOT.parent/'empty-context.png')
core=Image.new('L',SIZE)
ImageDraw.Draw(core).polygon([(39,85),(100,85),(126,105),(133,92),(167,92),(181,109),(182,161),(170,180),(26,181),(22,159),(25,116)],fill=255)
mask=ImageChops.lighter(core,core.filter(ImageFilter.GaussianBlur(3)))
empty=Image.composite(restored,original,mask)
save(empty,ROOT.parent/'master-empty.png','Bounded technical restoration of only chair/table/plant footprints in accepted pilot master.\n'+(ROOT.parent/'empty-prompt.txt').read_text(encoding='utf-8'))

rows=[]
curtains=json.loads((ART/'curtains/production-manifest.json').read_text(encoding='utf-8'))['produce']
rugs=json.loads((ART/'rugs/production-manifest.json').read_text(encoding='utf-8'))['produce']
for category,items in [('curtains',curtains),('rugs',rugs)]:
    for row in items:
        source=ART/row['output_path'] if row['output_path'].startswith(category+'/') else ART/category/row['output_path']
        im=Image.open(source).convert('RGBA')
        if category=='curtains':
            left,right=row['rodEndpoints']
            scale=112/((right[0]-left[0])*im.width)
            target=(round(im.width*scale),round(im.height*scale))
            position=(round(13-target[0]*left[0]),round(4-target[1]*left[1]))
            anchor={'rodEndpoints':row['rodEndpoints'],'mountingAnchor':row['mountingAnchor']}
        else:
            target=(280,round(im.height*280/im.width))
            position=(162,round(159-target[1]*row['backEdgeAnchor'][1]))
            anchor={'backEdgeAnchor':row['backEdgeAnchor'],'footAnchor':row['footAnchor']}
        native=im.resize(target,Image.Resampling.NEAREST)
        low,high=native.getchannel('A').getextrema()
        assert low==0 and high>=240
        path=assets/(row['id']+'.png')
        save(native,path,'Technical nearest-neighbor normalized existing transparent '+category+' art. Source: '+str(source.relative_to(ART))+'\n'+row['prompt_used'])
        rows.append({'id':row['id'],'category':category,'file':str(path.relative_to(ROOT.parent)),
                     'position':position,'logicalSize':target,**anchor,'source':str(source.relative_to(ART)),
                     'method':'Uniform nearest normalization; source RGB/alpha retained','displayScale':3})

# Only paper/plaster surfaces may change: window, every timber member and the floor remain common master pixels.
wall_mask=Image.new('L',SIZE)
draw=ImageDraw.Draw(wall_mask)
planes=[[(7,0),(23,0),(23,89),(7,90)],[(119,1),(156,11),(156,85),(119,85)],
        [(168,5),(431,5),(431,85),(168,85)],[(437,10),(474,1),(474,93),(437,86)]]
for polygon in planes:draw.polygon(polygon,fill=255)
save(wall_mask,ROOT/'wall-surface-mask.png','Technical registration of upper-wall finish planes on the accepted common master; timber/window/floor excluded.')
inventory=json.loads((ART/'inventory.json').read_text(encoding='utf-8'))['items']
for row in [r for r in inventory if r['category']=='walls']:
    if row['id']=='wall-plain-cream':plate=empty.copy()
    else:
        source=ART/row['file']; finish=normalize(source)
        plate=Image.composite(finish,empty,wall_mask)
    outside=ImageChops.invert(wall_mask)
    for ch in ImageChops.difference(plate.convert('RGB'),empty.convert('RGB')).split():
        assert ImageChops.multiply(ch,outside).getbbox() is None
    path=assets/(row['id']+'.png')
    save(plate,path,'Technical upper-wall plane crop from existing registered wall plate '+row['file']+' onto accepted empty architecture. Only wall_mask pixels replaced; all other pixels unchanged. Original generation prompt in source asset.')
    rows.append({'id':row['id'],'category':'walls','file':str(path.relative_to(ROOT.parent)),
                 'position':[0,0],'logicalSize':list(SIZE),'source':row['file'],
                 'method':'Registered upper-wall plane replacement; common protected architecture','displayScale':3})
assert len(rows)==33 and len({r['id'] for r in rows})==33
(ROOT/'manifest.json').write_text(json.dumps({'assets':rows,'wallPlanes':planes,'counts':{'curtains':9,'rugs':12,'walls':12}},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':len(rows),'protectedWallArchitecture':True,'grid':SIZE}))
