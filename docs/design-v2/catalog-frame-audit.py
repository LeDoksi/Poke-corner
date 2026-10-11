"""Inspect source GIFs; keep only comparison examples in the design project."""
import csv
import hashlib
import io
import json
import tempfile
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
inventory = json.loads((ROOT / 'catalog-coverage-audit.json').read_text('utf-8'))
revision = inventory['treeSha']
CACHE = Path(tempfile.gettempdir()) / ('pokecorner-gif-audit-' + revision)
CACHE.mkdir(exist_ok=True)
SAMPLES = ROOT / 'asset-examples'
SAMPLES.mkdir(exist_ok=True)
PREFIXES = {
    'bw': 'versions/generation-v/black-white/animated/',
    'bwShiny': 'versions/generation-v/black-white/animated/shiny/',
    'showdown': 'other/showdown/',
    'showdownShiny': 'other/showdown/shiny/',
}
SAMPLE_IDS = {25, 133, 650, 658, 906, 1000}

def fetch(url):
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'PokeCorner-source-audit'})
            with urllib.request.urlopen(req, timeout=40) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            if error.code < 500 and error.code != 429:
                raise
            if attempt == 2:
                raise
        except (OSError, TimeoutError):
            if attempt == 2:
                raise
        time.sleep(1 + attempt)

def inspect(data):
    with Image.open(io.BytesIO(data)) as im:
        assert im.format == 'GIF', 'Not a GIF'
        frames = im.n_frames
        hashes = set()
        for index in range(frames):
            im.seek(index)
            hashes.add(hashlib.sha256(im.convert('RGBA').tobytes()).digest())
            if len(hashes) > 1:
                break
        return {'frames': frames, 'visuallyChanges': len(hashes) > 1, 'width': im.width, 'height': im.height}

# A two-frame file is animated only if its rendered pixels actually change.
fixture = io.BytesIO()
Image.new('RGB', (2, 2), 'red').save(fixture, format='GIF', save_all=True,
    append_images=[Image.new('RGB', (2, 2), 'blue')], duration=100)
assert inspect(fixture.getvalue())['visuallyChanges']
still = io.BytesIO()
Image.new('RGB', (2, 2), 'red').save(still, format='GIF')
assert not inspect(still.getvalue())['visuallyChanges']

csv_url = 'https://raw.githubusercontent.com/PokeAPI/pokeapi/master/data/v2/csv/pokemon.csv'
csv_data = fetch(csv_url)
pokemon = list(csv.DictReader(io.StringIO(csv_data.decode('utf-8'))))
defaults = {int(p['species_id']): p for p in pokemon if p['is_default'] == '1'}
assert len(defaults) == inventory['speciesCount'], 'Source count changed; refresh inventory'
assert all(i in defaults for i in range(1, inventory['speciesCount'] + 1))

def audit(species_id, group):
    pokemon_id = int(defaults[species_id]['id'])
    row = {'speciesId': species_id, 'pokemonId': pokemon_id,
           'name': defaults[species_id]['identifier'], 'group': group}
    row['url'] = f'https://raw.githubusercontent.com/PokeAPI/sprites/{revision}/sprites/pokemon/{PREFIXES[group]}{pokemon_id}.gif'
    if pokemon_id == species_id and species_id in inventory['groups'][group]['missing']:
        return dict(row, status='absent-in-pinned-tree')
    path = CACHE / f'{group}-{pokemon_id}.gif'
    try:
        data = path.read_bytes() if path.exists() else fetch(row['url'])
        details = inspect(data)
        if not path.exists():
            path.write_bytes(data)
        if species_id in SAMPLE_IDS:
            (SAMPLES / path.name).write_bytes(data)
        return dict(row, status='ok', bytes=len(data), **details)
    except Exception as error:
        return dict(row, status='error', error=str(error))

jobs = [(sid, group) for sid in sorted(defaults) for group in PREFIXES]
rows = []
with ThreadPoolExecutor(max_workers=8) as executor:
    futures = [executor.submit(audit, *job) for job in jobs]
    for future in as_completed(futures):
        rows.append(future.result())
        if len(rows) % 400 == 0:
            print(f'Checked {len(rows)}/{len(jobs)}', flush=True)
assert len(rows) == len(jobs)
summary = {}
for group in PREFIXES:
    part = [r for r in rows if r['group'] == group]
    summary[group] = {
        'changing': sum(r.get('visuallyChanges', False) for r in part),
        'static': sum(r['status'] == 'ok' and not r['visuallyChanges'] for r in part),
        'missing': [r['speciesId'] for r in part if r['status'] == 'absent-in-pinned-tree'],
        'errors': [r for r in part if r['status'] == 'error'],
        'bytes': sum(r.get('bytes', 0) for r in part),
    }
indexed = {(r['speciesId'], r['group']): r for r in rows}
paired = {g: [sid for sid in defaults if all(indexed[sid, variant].get('visuallyChanges', False)
          for variant in (g, g + 'Shiny'))] for g in ('bw', 'showdown')}
report = {'checkedAtUtc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
    'revision': revision, 'speciesCount': len(defaults), 'defaultMappingSource': csv_url,
    'defaultMappingSha256': hashlib.sha256(csv_data).hexdigest(),
    'nonmatchingDefaultIds': {str(sid): int(p['id']) for sid, p in defaults.items() if sid != int(p['id'])},
    'summary': summary, 'animatedPairs': {g: len(ids) for g, ids in paired.items()},
    'combinedPairs': len(set(paired['bw']) | set(paired['showdown'])),
    'rows': sorted(rows, key=lambda r: (r['speciesId'], r['group']))}
(ROOT / 'catalog-frame-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: v for k, v in report.items() if k != 'rows'}, ensure_ascii=False), flush=True)
