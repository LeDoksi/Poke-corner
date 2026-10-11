"""Embed readable provenance in derived PNGs, preserving all decoded image pixels."""
import json
import hashlib
import subprocess
import sys
from pathlib import Path
from PIL import Image

ROOT=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parent
HELPER='C:/Users/Georgy/.agents/skills/impeccable/scripts/embed-prompt.mjs'
command=['rtk','proxy','node',HELPER]
scan=subprocess.run(command+['--scan',str(ROOT)],capture_output=True,text=True,encoding='utf-8')
assert scan.returncode in [0,1,3],scan.stderr
missing=[Path(line.removeprefix('MISSING: ').strip()) for line in scan.stdout.splitlines() if line.startswith('MISSING: ')]
right_path=ROOT/'right/manifest.json'
right=json.loads(right_path.read_text(encoding='utf-8')) if right_path.exists() else {'produce':[],'direct':[]}
right_prompts={r['id']:r['prompt_used'] for r in right['produce']+right['direct']}
temp=ROOT/'metadata-prompt.txt'
for path in missing:
    im=Image.open(path);before=im.convert('RGBA').tobytes();size=im.size
    frame_hashes=[]
    for index in range(getattr(im,'n_frames',1)):
        im.seek(index);frame_hashes.append((hashlib.sha256(im.convert('RGBA').tobytes()).hexdigest(),im.info.get('duration')))
    im.seek(0)
    prompt=im.info.get('impeccable:prompt')
    if not prompt:
        assert right_prompts,('Missing source provenance',str(path))
        relevant=[v for k,v in right_prompts.items() if k.removesuffix('-zone') in path.name]
        prompt='Technical PNG derivative, no repainting. Source and registered coordinates: right/manifest.json. File: '+str(path.relative_to(ROOT))+'\n'+'\n'.join(relevant or right_prompts.values())
    temp.write_text(prompt,encoding='utf-8')
    result=subprocess.run(command+[str(path),'--prompt-file',str(temp)],capture_output=True,text=True,encoding='utf-8')
    assert result.returncode==0,(str(path),result.stderr)
    after=Image.open(path)
    assert after.size==size and after.convert('RGBA').tobytes()==before,path
    assert getattr(after,'n_frames',1)==len(frame_hashes)
    for index,expected in enumerate(frame_hashes):
        after.seek(index)
        assert (hashlib.sha256(after.convert('RGBA').tobytes()).hexdigest(),after.info.get('duration'))==expected,path
final=subprocess.run(command+['--scan',str(ROOT)],capture_output=True,text=True,encoding='utf-8')
assert final.returncode==0,final.stdout
print(json.dumps({'embeddedOrNormalized':len(missing),'decodedPixelsUnchanged':True,'finalScan':final.stdout.strip()}))
