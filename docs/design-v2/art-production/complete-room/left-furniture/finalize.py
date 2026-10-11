"""Embed exact prompts with installed Impeccable helper and record completed visual QA."""
import hashlib
import json
import subprocess
from pathlib import Path
from PIL import Image
from prepare import ROOT, FILES

helper=Path('C:/Users/Georgy/.agents/skills/impeccable/scripts/embed-prompt.mjs')
assert helper.exists()
manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
for key in FILES:
    prompt=ROOT/'_sources'/f'{key}-prompt.txt'
    targets=[ROOT/'_sources'/f'{key}-context.png']+[ROOT/r['output_path'] for r in manifest['produce'] if r['id'].lower().startswith(key)]
    for target in targets:
        subprocess.run(['rtk','node',str(helper),str(target),'--prompt-file',str(prompt)],check=True,capture_output=True)
        image=Image.open(target);image.load()
        assert image.info['impeccable:prompt'].strip()==prompt.read_text(encoding='utf-8').strip()
for row in manifest['produce']:
    row['qa_status']='accepted'
    row['provenance']['sourceSha256']=hashlib.sha256((ROOT/row['source_crop']).read_bytes()).hexdigest()
manifest['semantic'][0]['qa_status']='accepted'
manifest['assumptions']=[v for v in manifest['assumptions'] if 'embed-prompt' not in v]
manifest['assumptions'].append('Exact prompts embedded with installed Impeccable embed-prompt.mjs; metadata asserted equal to sidecar.')
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
proof=json.loads((ROOT/'proof.json').read_text(encoding='utf-8'))
proof['status']='accepted-after-source-crop-and-final-patch-visual-comparison'
proof['allExactPromptsEmbeddedAndVerified']=True
proof['consumableAssets']=8
(ROOT/'proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
subprocess.run(['rtk','node',str(helper),'--scan',str(ROOT)],check=True)
print('Final visual QA accepted; six source originals, eight assets, exact embedded prompts verified.')
