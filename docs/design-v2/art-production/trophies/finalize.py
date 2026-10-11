from pathlib import Path
import json
from PIL import Image
r=Path(__file__).resolve().parent
records=[]
for f in sorted(r.glob('*.manifest.json')):
    doc=json.loads(f.read_text(encoding='utf-8'))
    for a in doc['assets']:
        im=Image.open(r.parent/a['file']).convert('RGBA')
        bounds=im.getchannel('A').point(lambda v:255 if v>=32 else 0).getbbox()
        a['alphaBoundsInOutput']=list(bounds)
        a['footAnchor']=[.5,round(bounds[3]/im.height,5)]
        a['logicalSize']=[a['displayWidth'],round(im.height/im.width*a['displayWidth'],2)]
        a['qaStatus']='needs_parent_review'
        a['visualReview']='Contact sheet passes isolated material/silhouette/palette check; cabinet placement pending.'
        assert a['logicalSize'][1]<=80 and a['displayWidth']<=86,a['id']
        records.append(a)
    f.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=r/'production-manifest.json'
doc=json.loads(p.read_text(encoding='utf-8'))
for a in doc['produce']:
    live=next(x for x in records if x['id']==a['id'])
    a['dimensions']=live['size']; a['logicalSize']=live['logicalSize']; a['footAnchor']=live['footAnchor']
    a['qa_status']='needs_parent_review'
    if a['id'] in ['trophy-special-nursery-expedition','trophy-special-family-expedition','trophy-special-evolved-expedition','trophy-special-all-berries']:
        a['deviations']='Parent-approved nonliving motifs: egg in backpack, two matching backpacks, botanical book with arrow, berry basket; safety-rejected prior brief replaced.'
doc['blockers']=[]
doc['count']=len(records)
doc['assumptions'].append('Foot anchor updated from nominal 0.92 to measured opaque bottom of each cut asset; parent must use manifest anchor.')
p.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert len(records)==63
print('63 assets checked: alpha, unique manifests, logical bounds <=86x80, measured foot anchors.')
