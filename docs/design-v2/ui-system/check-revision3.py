from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

root=Path(r'C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows')
out=root/'docs/design-v2/ui-system'
html=(out/'review.html').read_text(encoding='utf-8')
data=json.loads(re.search(r'<script type="application/json" id="revision-data">(.*?)</script>',html).group(1))['round3']
parents=data['parents']
assert len(parents)==800 and len({p['key'] for p in parents})==800
counts=Counter(p['sid'] for p in parents)
assert len(counts)==600 and sum(n==2 for n in counts.values())==200
for p in parents:
    form='shiny' if p['shiny'] else 'normal'
    assert (root/f"docs/design-v2/catalog-package/assets/{form}/{p['sid']}.gif").exists(), p
for r in data['recipes']:
    for sid in r['outcomes']:
        assert data['species'][str(sid)]['root']==sid, r
        assert (root/f'docs/design-v2/catalog-package/assets/normal/{sid}.gif').exists(), r
facts=json.loads((out/'flavor-facts.json').read_text(encoding='utf-8'))
flavor_path=root/'docs/design-v2/catalog-package/source/pokemon_species_flavor_text.csv'
assert hashlib.sha256(flavor_path.read_bytes()).hexdigest()==facts['sourceFileSha256']
flavor={(int(r['species_id']),int(r['version_id'])):' '.join(r['flavor_text'].replace('\u00ad','').split()) for r in csv.DictReader(flavor_path.open(encoding='utf-8',newline='')) if r['language_id']=='9'}
for fact in facts['facts']:
    assert fact['sourceText']==flavor[(fact['speciesId'],fact['sourceVersionId'])]
    assert fact['textRu'] and not re.search('Тип:|редкость|Начальное звено',fact['textRu'])
all_data=json.loads(re.search(r'<script type="application/json" id="revision-data">(.*?)</script>',html).group(1))
for sid,entry in all_data['facts'].items():
    assert entry['fact']==next(f['textRu'] for f in facts['facts'] if str(f['speciesId'])==sid)
assert 'type="checkbox" data-use-stone' not in html
assert 'data-use-stone aria-pressed="false"' in html
team=all_data['team']
assert len(team['members'])==800 and len({p['key'] for p in team['members']})==800
balance=json.loads((root/'docs/design-v2/final/balance.json').read_text())
for p in team['members']:
    assert len(p['traits'])==(2 if p['level']>=balance['secondTraitLevel'] else 1)
    assert p['trait']==p['traits'][0] and set(p['traits'])<=set(balance['traits'])
    assert (root/f"docs/design-v2/catalog-package/assets/{'shiny' if p['shiny'] else 'normal'}/{p['sid']}.gif").exists()
assert len(team['demoKeys'])==3
assert 'class="species-find-action"' in html and '>Где найти?</button>' in html
for file in ('review.js','revision2.js','revision3.js','capture.js','expedition-team.js'):
    subprocess.run(['node','--check',str(out/file)],check=True)
print('PASS: nursery and team fixtures 800 individuals; assets, trait unlocks, root offspring; 11 sourced facts; native stone/where buttons; 5 JS files parse.')
