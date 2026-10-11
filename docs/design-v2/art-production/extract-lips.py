"""Technical, pixel-preserving extraction of cabinet front lips on registered canvases."""
import json
import subprocess
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parent
registration=root/'furniture/cabinet-surface-registration.json'
data=json.loads(registration.read_text(encoding='utf-8'))
records=[]
for model in data['models']:
    source=(registration.parent/model['source']).resolve()
    output=(registration.parent/model['frontLipOutput']).resolve()
    assert output.is_relative_to(root)
    image=Image.open(source).convert('RGBA')
    x,y,w,h=model['lipRect']
    assert w>0 and h>0 and 0<=x<x+w<=image.width and 0<=y<y+h<=image.height
    strip=image.crop((x,y,x+w,y+h))
    assert strip.getchannel('A').getextrema()[1]>0
    canvas=Image.new('RGBA',image.size)
    canvas.paste(strip,(x,y))
    assert canvas.crop((x,y,x+w,y+h)).tobytes()==strip.tobytes()
    assert canvas.getchannel('A').getpixel((0,0))==0
    canvas.save(output)
    prompt='Origin: technical front-lip extraction from '+source.name+'; lipRect='+str(model['lipRect'])+'; pixels unchanged, canvas registration preserved. '+image.info.get('impeccable:prompt','')
    provenance=output.with_suffix('.origin.txt')
    provenance.write_text(prompt,encoding='utf-8')
    subprocess.run(['rtk','proxy','node','C:/Users/Georgy/.agents/skills/impeccable/scripts/embed-prompt.mjs',
                    str(output),'--prompt-file',str(provenance)],check=True,capture_output=True)
    records.append({'id':model['id']+'-lip','file':str(output.relative_to(root)).replace('\\','/'),
                    'size':list(image.size),'rect':model['lipRect'],'status':'technical-extraction-verified'})
assert len(records)==4
(root/'furniture/lip-manifest.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
print('4 registered shelf lip overlays verified; source pixels unchanged')
