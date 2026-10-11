"""Stage full PokeAPI metadata and audited BW assets without changing saves/runtime."""
import csv
import hashlib
import io
import json
import shutil
import tempfile
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PACKAGE = ROOT / 'catalog-package'
RAW = PACKAGE / 'source'
RAW.mkdir(parents=True, exist_ok=True)
audit = json.loads((ROOT / 'catalog-frame-audit.json').read_text('utf-8'))
sprite_revision = audit['revision']
CACHE = Path(tempfile.gettempdir()) / ('pokecorner-gif-audit-' + sprite_revision)

def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'PokeCorner-catalog-package'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()

commit = json.loads(fetch('https://api.github.com/repos/PokeAPI/pokeapi/commits/master'))['sha']
tables = ['pokemon', 'pokemon_species', 'pokemon_types', 'types', 'pokemon_evolution',
          'pokemon_species_names', 'pokemon_species_flavor_text', 'pokemon_species_prose',
          'languages', 'type_names', 'pokemon_habitats']
sources = {}
def load(name):
    url = f'https://raw.githubusercontent.com/PokeAPI/pokeapi/{commit}/data/v2/csv/{name}.csv'
    data = fetch(url)
    (RAW / (name + '.csv')).write_bytes(data)
    sources[name] = {'url': url, 'sha256': hashlib.sha256(data).hexdigest()}
    return name, list(csv.DictReader(io.StringIO(data.decode('utf-8'))))
with ThreadPoolExecutor(max_workers=4) as executor:
    data = dict(executor.map(load, tables))
species = data['pokemon_species']
assert len(species) == audit['speciesCount'], 'Catalog changed; refresh asset audit'
defaults = {int(p['species_id']): p for p in data['pokemon'] if p['is_default'] == '1'}
assert len(defaults) == len(species)
assert {int(s['id']) for s in species} == set(defaults)
types = {t['id']: t['identifier'] for t in data['types']}
assets = {(r['speciesId'], r['group']): r for r in audit['rows']}
catalog = []
for item in species:
    sid = int(item['id'])
    pokemon = defaults[sid]
    entry = {'id': sid, 'species': item, 'defaultPokemon': pokemon,
        'types': [types[t['type_id']] for t in sorted(data['pokemon_types'], key=lambda t: int(t['slot'])) if t['pokemon_id'] == pokemon['id']],
        'names': [n for n in data['pokemon_species_names'] if n['pokemon_species_id'] == str(sid)],
        'descriptions': [n for n in data['pokemon_species_flavor_text'] if n['species_id'] == str(sid)],
        'prose': [n for n in data['pokemon_species_prose'] if n['pokemon_species_id'] == str(sid)],
        'evolutionRules': [n for n in data['pokemon_evolution'] if n['evolved_species_id'] == str(sid)],
        'assets': {}, 'voiceStatus': 'unresolved', 'gameAvailability': 'pending-integration'}
    for variant, group in [('normal', 'bw'), ('shiny', 'bwShiny')]:
        resource = assets[sid, group]
        ready = resource.get('visuallyChanges', False)
        asset = {'animated': ready, 'sourceUrl': resource['url']}
        if ready:
            source = CACHE / f'{group}-{resource["pokemonId"]}.gif'
            assert source.exists(), f'Missing audited cache: {source}'
            assert source.stat().st_size == resource['bytes'], f'Cache size mismatch: {source}'
            relative = f'assets/{variant}/{sid}.gif'
            target = PACKAGE / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            asset.update(path=relative, sha256=hashlib.sha256(source.read_bytes()).hexdigest(), frames=resource['frames'])
        entry['assets'][variant] = asset
    assert entry['types'], f'Missing types: {sid}'
    catalog.append(entry)
manifest = {'schemaVersion': 1, 'dataRevision': commit, 'spriteRevision': sprite_revision,
    'sources': sources, 'lookupTables': {name: data[name] for name in ['types', 'type_names', 'languages', 'pokemon_habitats']},
    'species': catalog}
(PACKAGE / 'catalog.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
summary = {'species': len(catalog), 'normalAnimated': sum(s['assets']['normal']['animated'] for s in catalog),
    'shinyAnimated': sum(s['assets']['shiny']['animated'] for s in catalog), 'dataRevision': commit,
    'runtimeChanged': False, 'voicesReady': False}
(PACKAGE / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps(summary), flush=True)
