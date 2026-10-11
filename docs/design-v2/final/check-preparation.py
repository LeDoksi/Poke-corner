"""Verify the prepared handoff and emit a lean runtime allowlist. Never reads user state."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FINAL = Path(__file__).resolve().parent
DESIGN = FINAL.parent

def read(path):
    return json.loads(path.read_text(encoding='utf-8'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def target(path):
    if path.is_relative_to(DESIGN):
        return path.relative_to(DESIGN).as_posix()
    return 'fonts/'+path.name

def main():
    catalog = read(DESIGN/'catalog-package/catalog.json')
    content = read(DESIGN/'catalog-package/runtime-content.json')
    assert len(catalog['species']) == len(content['species']) == 1025
    assert {s['id'] for s in content['species']} == set(range(1,1026))
    assert all(s['displayName'] and s['descriptionRu'] and s['descriptionSource'] for s in content['species'])
    normal = {int(p.stem):p for p in (DESIGN/'catalog-package/assets/normal').glob('*.gif')}
    shiny = {int(p.stem):p for p in (DESIGN/'catalog-package/assets/shiny').glob('*.gif')}
    usable = {i:p for i,p in shiny.items() if i in normal and sha(p) != sha(normal[i])}
    assert (len(normal),len(shiny),len(usable)) == (868,865,859)
    delivery = read(DESIGN/'voice-preview/delivery.json')
    assert {v['id'] for v in delivery['voices']} == set(normal)
    assert len(delivery['effects']) == 1
    for v in delivery['voices']+delivery['effects']:
        p=DESIGN/'voice-preview'/v['path']
        assert v.get('status','ready') == 'ready' and p.is_file() and sha(p) == v['sha256']
    rarity={s['id']:s for s in read(DESIGN/'rarity-draft-v2.json')['species']}
    localized={s['id']:s for s in content['species']}
    voices={v['id']:v for v in delivery['voices']}
    def sprite(i, pool):
        return {'path':target(pool[i]),'sha256':sha(pool[i])} if i in pool else None
    rows=[]
    for s in catalog['species']:
        i=s['id']; raw=s['species']; r=rarity[i]; c=localized[i]
        rows.append({'id':i,'displayName':c['displayName'],'aliases':c['aliases'],
                     'descriptionRu':c['descriptionRu'],'types':s['types'],
                     'familyRootId':r['familyRootId'],'rarity':r['rarity'],
                     'evolvesFromSpeciesId':int(raw['evolves_from_species_id']) if raw['evolves_from_species_id'] else None,
                     'evolutionChainId':int(raw['evolution_chain_id']),
                     'normalAvailable':i in normal,'shinyAvailable':i in usable,
                     'normalAsset':sprite(i,normal),'shinyAsset':sprite(i,usable),
                     'voice':{'path':target(DESIGN/'voice-preview'/voices[i]['path']),
                              'sha256':voices[i]['sha256'],'temporary':voices[i]['temporary']} if i in voices else None})
    runtime_catalog={'schemaVersion':1,'dataRevision':catalog['dataRevision'],
                     'pathBase':'Data package root; paths match runtime-package target fields',
                     'availabilityPolicy':'Only distinct supported shiny. Source catalog flags/URLs are historical acquisition evidence, not runtime eligibility.',
                     'species':rows}
    runtime_path=FINAL/'runtime-catalog.json'
    runtime_path.write_text(json.dumps(runtime_catalog,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    legacy = read(FINAL/'legacy-v1/manifest.json')
    for f in legacy['files']:
        p=FINAL/'legacy-v1'/f['path']
        assert p.stat().st_size == f['bytes'] and sha(p) == f['sha256']
    tasks=read(FINAL/'transition-tasks.json')['tasks']
    by_id={t['id']:t for t in tasks}
    assert len(by_id) == 20 and len({t['kanbanId'] for t in tasks}) == 20
    visited=set()
    while len(visited)<20:
        ready={t['id'] for t in tasks if t['id'] not in visited and set(t['dependsOn']) <= visited}
        assert ready, 'cyclic or missing task dependency'
        visited.update(ready)
    for t in tasks:
        assert t['kanbanDependsOn'] == [by_id[d]['kanbanId'] for d in t['dependsOn']]
        assert len(t['checklist']) == 5
    checks=read(FINAL/'checks.json')
    assert (checks['staticScreens'],checks['onboardingSteps'],checks['serviceStates']) == (6,6,18)
    assert checks['internalAnchorsExist'] and checks['linksExist'] and not checks['runtimeJavaScript']
    rules=read(FINAL/'catalog-rules.json')
    for edge in rules['evolutionEdges']:
        assert edge['normalAvailable'] == (edge['from'] in normal and edge['to'] in normal)
        assert edge['shinyAvailable'] == (edge['from'] in usable and edge['to'] in usable)
    # Only active files, not source archives/contact sheets/historical code, belong in Data.
    files=set(normal.values())|set(usable.values())
    files.update(DESIGN/'voice-preview'/v['path'] for v in delivery['voices']+delivery['effects'])
    art=DESIGN/'art-production/last-attempt/final-assets'
    art_files=set((art/'assets').rglob('*.png'))
    assert len(art_files)==114
    files.update(art_files)
    files.add(art/'manifest.json')
    shop_manifest=read(FINAL/'assets/shop/manifest.json')
    shop={FINAL/'assets/shop'/item['file'] for item in shop_manifest['items']}
    assert len(shop)==12
    files.update(shop)
    files.add(FINAL/'assets/shop/manifest.json')
    for rel in ['final/runtime-catalog.json','catalog-package/runtime-content.json',
                'voice-preview/delivery.json','rarity-draft-v2.json','berry-profiles.json',
                'nursery-recipes-draft.json','location-draft.json','final/balance.json',
                'final/catalog-rules.json','final/achievement-rewards.json','final/DESIGN.json',
                'final/legacy-v1/OfflineStarterFallback.json','final/legacy-v1/manifest.json']:
        files.add(DESIGN/rel)
    files.update((ROOT/'docs/prototype-v1').rglob('*.woff2'))
    files.update((ROOT/'docs/prototype-v1').rglob('*.ttf'))
    font_license=ROOT/'docs/prototype-v1/assets/Nunito-OFL.txt'
    assert font_license.is_file()
    files.add(font_license)
    entries=[{'path':p.relative_to(ROOT).as_posix(),'target':target(p),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(files)]
    assert all('/previews/' not in e['path'] and '/history/' not in e['path'] for e in entries)
    targets={e['target'] for e in entries}
    assert len(targets)==len(entries)
    operational=[a for s in rows for a in [s['normalAsset'],s['shinyAsset'],s['voice']] if a]
    assert all(a['path'] in targets for a in operational)
    assert all(s['shinyAsset'] is None for s in rows if not s['shinyAvailable'])
    manifest={'schemaVersion':1,'catalogRevision':catalog['dataRevision'],
              'purpose':'T01 copies source path to Data/target. Load final/runtime-catalog.json; operational paths are rooted at Data. Source manifest references are provenance, not runtime dependencies.',
              'normalSprites':868,'usableShinySprites':859,'voices':868,'effects':1,
              'files':entries,'totalBytes':sum(e['bytes'] for e in entries)}
    (FINAL/'runtime-package.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    result={'passed':True,'species':1025,'normal':868,'usableShiny':859,'voices':868,
            'audioEffects':1,'artPng':114,'shopPng':12,'tasks':20,'dependencyGraphAcyclic':True,
            'runtimeFiles':len(entries),'runtimeBytes':manifest['totalBytes'],
            'allOperationalPathsInAllowlist':True,
            'runtimeManifestSha256':sha(FINAL/'runtime-package.json'),
            'scope':'Prepared files and local task registry; live board verified separately. Not WPF migration/release acceptance.'}
    (FINAL/'preparation-checks.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result))

if __name__=='__main__':
    main()
