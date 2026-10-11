"""Technical reference preparation; preserves source pet pixels and frame coordinates."""
import json
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parent
ROOT.mkdir(exist_ok=True)
gif=Image.open(ROOT.parent.parent/'catalog-package/assets/normal/133.gif')
frames=[];bounds=[];durations=[]
for i in range(gif.n_frames):
    gif.seek(i);im=gif.convert('RGBA')
    frames.append(im.copy());bounds.append(im.getchannel('A').getbbox())
    durations.append(gif.info.get('duration',100))
valid=[b for b in bounds if b]
union=(min(b[0] for b in valid),min(b[1] for b in valid),max(b[2] for b in valid),max(b[3] for b in valid))
frames[0].crop(union).save(ROOT/'eevee-reference.png')
base=Image.open(ROOT.parent/'architecture/assets/room-base.png').convert('RGBA')
base.resize((480,193),Image.Resampling.NEAREST).save(ROOT/'empty-reference.png')
profile={'canvas':[480,193],'displayScale':3,'sourceCanvas':list(gif.size),
         'petUnionBox':list(union),'petNativeSize':[union[2]-union[0],union[3]-union[1]],
         'frames':len(frames),'durations':durations,'zoneRect':[0,72,225,193],
         'petFloorAnchor':[278,173],
         'status':'pilot-profile-not-visual-approval'}
assert len(frames)>1 and all(d>0 for d in durations)
(ROOT/'profile.json').write_text(json.dumps(profile,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:profile[k] for k in ['canvas','petNativeSize','frames','zoneRect']}))
