"""Inventory and static technical placement previews; never paints/recolors asset pixels."""
import argparse
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
SCENE=Image.open(ROOT/'architecture/assets/room-base.png').convert('RGBA')


def expected():
    result=[]
    def add(asset_id,category,path=None):
        result.append({'id':asset_id,'category':category,'file':path or f'{category}/assets/{asset_id}.png'})
    for prefix in ['table-t','cabinet-c','armchair-a','dresser-d']:
        for i in range(1,5): add(prefix+str(i),'furniture')
    for model in ['pillow','basket','nest']:
        for color in ['cream','sage']: add(f'bed-{model}-{color}','beds')
    for model in ['cactus','succulent','fern','orchid','pilea','flowering-shrub','ficus','monstera','palm','dracaena']:
        for pot in ['terracotta','woven-cream']: add(f'plant-{model}-{pot}','plants')
    for name in ['kanto-map','johto-map','hoenn-map','pallet-sunset','ecruteak-lanterns','lake','pikachu-berries',
                 'eevee-forest','bulbasaur-plants','dragonite-postman','snorlax-meadow','mew-stars']:
        add('painting-'+name,'paintings')
    for model in ['woven','checkered','oval','pokeball']:
        for color in ['cream','sage','blue']: add(f'rug-{model}-{color}','rugs')
    for model in ['short','linen','tied']:
        for color in ['cream','sage','blue']: add(f'curtain-{model}-{color}','curtains')
    for model in ['plain','panels','floral','stars']:
        for color in (['honey','cream','walnut'] if model=='panels' else ['cream','sage','blue']):
            add(f'wall-{model}-{color}','walls','architecture/assets/room-base.png' if (model,color)==('plain','cream') else None)
    for chain in ['species','population','catches','expeditions','regions','team','care','nursery','evolution','shiny','decor']:
        for tier in range(1,5): add(f'trophy-{chain}-{tier}','trophies')
    for name in ['nick','favorite','type-team','bonus-find','nursery-expedition','family-expedition','evolved-expedition',
                 'all-berries','berry-evolution','branch-choice','offspring-choice','all-plants','three-pictures',
                 'mixed-furniture','all-legends','second-trait','retrain','rehome','xp-potion']:
        add('trophy-special-'+name,'trophies')
    assert len(result)==150 and len({r['id'] for r in result})==150
    assert len([r for r in result if r['category']=='trophies'])==63
    return result


def inventory():
    metadata={}
    for path in sorted(ROOT.rglob('*.manifest.json')):
        data=json.loads(path.read_text(encoding='utf-8'))
        for a in data.get('assets',[]): metadata[a['id']]=a
    for pattern in ['asset-manifest.json','production-manifest.json']:
        for path in sorted(ROOT.rglob(pattern)):
            data=json.loads(path.read_text(encoding='utf-8'))
            for a in data.get('produce',[]): metadata[a['id']]={**metadata.get(a['id'],{}),**a}
    items=expected()
    for item in items:
        meta=metadata.get(item['id'],{})
        for key in ['displayWidth','logicalSize','footAnchor','anchor','backEdgeAnchor','qa_status','deviations']:
            if key in meta: item[key]=meta[key]
        path=ROOT/item['file']
        item['status']='missing'
        if path.exists():
            im=Image.open(path)
            assert im.format=='PNG'
            alpha=im.convert('RGBA').getchannel('A')
            if item['category']!='walls': assert alpha.getextrema()[0]==0,item['id']
            assert im.info.get('impeccable:prompt'),f'Missing provenance:{item["id"]}'
            item.update(status='generated',size=list(im.size),alphaExtrema=list(alpha.getextrema()))
    summary={c:{'expected':sum(i['category']==c for i in items),
                'generated':sum(i['category']==c and i['status']=='generated' for i in items)}
             for c in sorted({i['category'] for i in items})}
    report={'status':'art-generated-placement-review-in-progress','expected':150,
            'generated':sum(i['status']=='generated' for i in items),'summary':summary,
            'technicalShelfLips':4,'items':items}
    (ROOT/'inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return report,{i['id']:i for i in items}


def gallery(report):
    names={'furniture':'Мебель','beds':'Лежанки','plants':'Растения','paintings':'Картины',
           'rugs':'Ковры','curtains':'Шторы','walls':'Стены','trophies':'Трофеи'}
    parts=['<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">',
           '<title>PokéCorner — библиотека уголка</title><style>body{margin:0;background:#f6f2e7;color:#283b31;font:16px Segoe UI,sans-serif}main{max-width:1280px;margin:auto;padding:32px}h1{font-size:36px;margin:0 0 12px}h2{font-size:26px;margin:40px 0 16px}p{line-height:1.5}figure{margin:0;padding:12px;background:#fffaf0;border-radius:8px}figcaption{font-size:13px;overflow-wrap:anywhere;margin-top:8px}img{width:100%;height:148px;object-fit:contain;image-rendering:pixelated}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px}.scene{height:auto;border-radius:8px}a{color:inherit}.note{color:#6c665b}.missing{opacity:.5}</style><main>',
           '<h1>Библиотека уголка</h1>',
           f'<p>{report["generated"]} / 150 раздельных предметов · 87 вариантов декора и 63 внешности трофеев</p>',
           '<p class="note">Статичная производственная галерея. Размеры и прозрачность проверены; визуальная проверка сочетаний выборочная. Все PNG доступны по клику, без игровых действий.</p>',
           '<h2>Проверка сборки</h2><a href="previews/assembled-room.png"><img class="scene" src="previews/assembled-room.png" alt="Комната, собранная из раздельных ассетов"></a>',
           '<h2>Трофеи в четырёх шкафах</h2><a href="previews/shelf-stress.png"><img class="scene" src="previews/shelf-stress.png" alt="Крупные трофеи на полках четырёх моделей шкафов"></a>']
    for category in ['furniture','plants','paintings','rugs','curtains','walls','beds','trophies']:
        parts.append('<h2>'+names[category]+'</h2><div class="grid">')
        for item in report['items']:
            if item['category']!=category: continue
            label=html.escape(item['id'])
            if item['status']=='generated':
                path=html.escape(item['file'],quote=True)
                parts.append(f'<figure><a href="{path}"><img loading="lazy" src="{path}" alt="{label}"></a><figcaption>{label}</figcaption></figure>')
            else: parts.append(f'<figure class="missing"><figcaption>{label} · ещё не готов</figcaption></figure>')
        parts.append('</div>')
    parts.append('</main></html>')
    (ROOT/'index.html').write_text('\n'.join(parts),encoding='utf-8')


def sprite(record):
    return Image.open(ROOT/record['file']).convert('RGBA')


def foot(record):
    return record.get('footAnchor',record.get('anchor',[.5,.98]))


def put(canvas,record,point,width=None,limit=None,anchor=None):
    image=sprite(record)
    scale=(width/image.width) if width else 1
    if limit: scale=min(scale,limit[0]/image.width,limit[1]/image.height)
    wh=[max(1,round(image.width*scale)),max(1,round(image.height*scale))]
    image=image.resize(wh,Image.Resampling.NEAREST)
    ax,ay=anchor or foot(record)
    xy=[round(point[0]-ax*wh[0]),round(point[1]-ay*wh[1])]
    canvas.alpha_composite(image,xy)
    return {'xy':xy,'size':wh,'scale':scale,'anchor':[ax,ay]}


def contacts(report):
    out=ROOT/'previews';out.mkdir(exist_ok=True)
    for category in report['summary']:
        items=[i for i in report['items'] if i['category']==category and i['status']=='generated']
        if not items: continue
        columns=4 if category!='trophies' else 7
        cellw,cellh=(240,200) if category!='walls' else (380,180)
        canvas=Image.new('RGBA',(columns*cellw,((len(items)+columns-1)//columns)*cellh),(247,243,232,255))
        draw=ImageDraw.Draw(canvas)
        for n,item in enumerate(items):
            x=(n%columns)*cellw;y=(n//columns)*cellh
            put(canvas,item,(x+cellw/2,y+cellh-38),width=cellw-24,limit=(cellw-24,cellh-52))
            draw.text((x+8,y+cellh-28),item['id'],font=FONT,fill=(34,43,34))
        canvas.convert('RGB').save(out/f'{category}-contact.png')


def shelf_tests(items):
    registration=json.loads((ROOT/'furniture/cabinet-surface-registration.json').read_text(encoding='utf-8'))
    trophies=[i for i in items.values() if i['category']=='trophies' and i['status']=='generated']
    if not trophies: return
    examples=sorted(trophies,key=lambda i:i.get('logicalSize',[0,0])[1],reverse=True)[:3]
    checks=[];panels=[]
    for model in registration['models']:
        cabinet=items[model['id']];image=sprite(cabinet);panel=Image.new('RGBA',(650,440),(247,243,232,255))
        xoff,yoff=(650-image.width)//2,50
        panel.alpha_composite(image,(xoff,yoff))
        world_scale=454/image.width
        bx,by,bw,bh=model['emptyBayRect']
        for trophy in trophies:
            im=sprite(trophy);ax,ay=foot(trophy)
            logical=trophy.get('logicalSize',[76,76])
            nominal=min(logical[0]/world_scale/im.width,logical[1]/world_scale/im.height)
            for n,(sx,sy) in enumerate(model['shelfSupportAnchors']):
                available_h=sy-by-5
                scale=min(nominal,available_h/max(1,ay*im.height),(bw/3-10)/im.width)
                w,h=round(im.width*scale),round(im.height*scale)
                left=sx-ax*w;top=sy-ay*h
                fits=top>=by+3 and left>=bx-1 and left+w<=bx+bw+1
                checks.append({'cabinet':model['id'],'trophy':trophy['id'],'slot':n,
                               'fitsEnvelope':fits,'scaledBelowNominal':scale<nominal-.0001})
                assert fits,(model['id'],trophy['id'],n)
        for trophy,point in zip(examples,model['shelfSupportAnchors']):
            im=sprite(trophy);logical=trophy.get('logicalSize',[76,76]);ay=foot(trophy)[1]
            scale=min(logical[0]/world_scale/im.width,logical[1]/world_scale/im.height,
                      (point[1]-by-5)/max(1,ay*im.height),(bw/3-10)/im.width)
            put(panel,trophy,(xoff+point[0],yoff+point[1]),width=im.width*scale)
        lip=Image.open(ROOT/'furniture'/model['frontLipOutput']).convert('RGBA')
        panel.alpha_composite(lip,(xoff,yoff))
        ImageDraw.Draw(panel).text((16,12),model['id']+' - largest trophy fit',font=FONT,fill=(34,43,34))
        panels.append(panel)
    canvas=Image.new('RGBA',(1300,880),(247,243,232,255))
    for n,panel in enumerate(panels): canvas.alpha_composite(panel,((n%2)*650,(n//2)*440))
    canvas.convert('RGB').save(ROOT/'previews/shelf-stress.png')
    result={'status':'mathematical-envelope-check-not-all-artistic-combinations',
            'checks':len(checks),'passed':sum(c['fitsEnvelope'] for c in checks),
            'uniformFitReductionUsed':sum(c['scaledBelowNominal'] for c in checks),'records':checks}
    (ROOT/'shelf-fit-checks.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')


def room_preview(items):
    canvas=SCENE.resize((1448,582),Image.Resampling.NEAREST)
    def point(x,y): return [x-44,y-231]
    def place(asset_id,p,w,limit=None,anchor=None):
        r=items.get(asset_id)
        if not r or r['status']!='generated': return None
        return put(canvas,r,p,w,limit,anchor)
    rug=items.get('rug-woven-sage')
    if rug and rug['status']=='generated':
        place(rug['id'],point(845,664),1060,anchor=rug.get('backEdgeAnchor',[.5,.03]))
    for asset,p in [('painting-kanto-map',(705,323)),('painting-bulbasaur-plants',(885,324)),('painting-mew-stars',(1085,324))]:
        place(asset,point(*p),154,(181,125),[.5,.5])
    place('curtain-linen-cream',point(252,235),400,(400,365),[.5,0])
    place('plant-fern-terracotta',point(187,522),130,(145,135))
    cabinet=place('cabinet-c3',point(851,632),454)
    if cabinet:
        model=next(m for m in json.loads((ROOT/'furniture/cabinet-surface-registration.json').read_text(encoding='utf-8'))['models'] if m['id']=='cabinet-c3')
        def transform(p): return [cabinet['xy'][0]+p[0]*cabinet['scale'],cabinet['xy'][1]+p[1]*cabinet['scale']]
        bx,by,bw,bh=model['emptyBayRect']
        for name,support in zip(['trophy-decor-4','trophy-expeditions-4','trophy-care-4'],model['shelfSupportAnchors']):
            if items[name]['status']=='generated':
                r=items[name];im=sprite(r);ax,ay=foot(r)
                hcap=(support[1]-by-5)*cabinet['scale']
                place(name,transform(support),76,(86,hcap/max(ay,.1)))
        lip=Image.open(ROOT/'furniture/assets/cabinet-c3-lip.png').convert('RGBA').resize(cabinet['size'],Image.Resampling.NEAREST)
        canvas.alpha_composite(lip,cabinet['xy'])
        place('plant-cactus-terracotta',transform(model['topPlantSupport']),60,(70,75))
    place('dresser-d2',point(1245,640),309)
    place('bed-basket-cream',point(1385,697),195,(200,105))
    table=place('table-t1',point(475,716),148)
    if table:
        im=sprite(items['table-t1']);alpha=im.getchannel('A');x=round(im.width*.35)
        top=next(y for y in range(im.height) if alpha.getpixel((x,y))>=128)
        p=[table['xy'][0]+x*table['scale'],table['xy'][1]+(top+im.height*.025)*table['scale']]
        place('plant-succulent-terracotta',p,55,(86,96))
    place('armchair-a1',point(294,765),290,(290,245))
    place('plant-monstera-woven-cream',point(105,754),155,(165,285))
    pet=Image.open(ROOT.parent/'catalog-package/assets/normal/133.gif').convert('RGBA')
    bounds=pet.getchannel('A').getbbox()
    assert bounds
    pet=pet.crop(bounds)
    scale=min(174/pet.width,180/pet.height)
    pet=pet.resize((round(pet.width*scale),round(pet.height*scale)),Image.Resampling.NEAREST)
    px,py=point(839,757);canvas.alpha_composite(pet,(round(px-pet.width/2),round(py-pet.height)))
    canvas.convert('RGB').save(ROOT/'previews/assembled-room.png')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--previews',action='store_true');args=parser.parse_args()
    report,items=inventory()
    if args.previews: contacts(report);shelf_tests(items);room_preview(items);gallery(report)
    print(json.dumps({'generated':report['generated'],'expected':150,'summary':report['summary']}))
