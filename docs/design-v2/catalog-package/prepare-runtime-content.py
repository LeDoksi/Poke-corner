"""Build a compact offline Russian factual overview, not canonical flavor translation."""
import hashlib
import json
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent
catalog = json.loads((ROOT / 'catalog.json').read_text('utf-8'))
rarity_path = ROOT.parent / 'rarity-draft-v2.json'
rarity = json.loads(rarity_path.read_text('utf-8'))
assert catalog['dataRevision'] == rarity['dataRevision'], 'Refresh rarity from the same catalog revision'
types_ru = {'normal': 'обычный', 'fighting': 'боевой', 'flying': 'летающий', 'poison': 'ядовитый',
            'ground': 'земляной', 'rock': 'каменный', 'bug': 'насекомый', 'ghost': 'призрачный',
            'steel': 'стальной', 'fire': 'огненный', 'water': 'водный', 'grass': 'травяной',
            'electric': 'электрический', 'psychic': 'психический', 'ice': 'ледяной',
            'dragon': 'драконий', 'dark': 'тёмный', 'fairy': 'волшебный'}
rarity_ru = {'Common': 'обычная', 'Uncommon': 'необычная', 'Rare': 'редкая', 'Legendary': 'легендарная'}
species = {r['id']: r for r in catalog['species']}
names = {sid: next(n['name'] for n in row['names'] if n['local_language_id'] == '9')
         for sid, row in species.items()}
families = {sid: family for family in rarity['families'] for sid in family['memberIds']}
assert set(species) == set(names) == set(families) == set(range(1, 1026))
with (ROOT / 'source/pokemon_species_names.csv').open(encoding='utf-8', newline='') as source:
    original_names = {int(r['pokemon_species_id']): r['name'] for r in csv.DictReader(source)
                      if r['local_language_id'] == '9'}
assert all(names[sid] == original_names[sid] for sid in names)
children = {sid: [] for sid in species}
for sid, row in species.items():
    previous = row['species']['evolves_from_species_id']
    if previous:
        children[int(previous)].append(sid)

rows = []
for sid, row in species.items():
    family = families[sid]
    type_text = ' / '.join(types_ru[t] for t in row['types'])
    description = f'Тип: {type_text}. '
    previous = row['species']['evolves_from_species_id']
    if previous:
        description += f'Развивается из {names[int(previous)]}. '
    elif len(family['memberIds']) > 1:
        description += 'Начальное звено эволюционной семьи. '
    else:
        description += 'Не имеет эволюционных родственников в каталоге. '
    if children[sid]:
        description += 'Следующая эволюция: ' + ', '.join(names[x] for x in children[sid]) + '. '
    elif previous:
        description += 'Завершающая стадия этой ветви. '
    description += f'В PokéCorner редкость семьи: {rarity_ru[family["rarity"]]}.'
    rows.append(dict(id=sid, displayName=names[sid], aliases=[], descriptionRu=description,
                     nameSource='pokeapi-en', descriptionSource='pokecorner-factual-overview-v1'))

payload = dict(schemaVersion=1, locale='ru', namingPolicy='International English display names from PokeAPI language 9. Cyrillic aliases only from verified supplied sources; none available in this snapshot.',
               descriptionPolicy='Authored Russian factual overview of types and evolution data plus explicitly labeled PokéCorner rarity. Not a translation of canonical Pokédex flavor text.',
               sources={'pokeapi-en': {'dataRevision': catalog['dataRevision'],
                                      'names': catalog['sources']['pokemon_species_names'],
                                      'species': catalog['sources']['pokemon_species'],
                                      'types': catalog['sources']['pokemon_types']},
                        'pokecorner-factual-overview-v1': {'generator': 'prepare-runtime-content.py',
                           'raritySource': '../rarity-draft-v2.json',
                           'raritySha256': hashlib.sha256(rarity_path.read_bytes()).hexdigest()}}, species=rows)
assert len(rows) == 1025 and len({r['id'] for r in rows}) == 1025
assert all(r['displayName'] == names[r['id']] and r['descriptionRu'] and 'Тип: ' in r['descriptionRu'] for r in rows)
assert all(r['nameSource'] in payload['sources'] and r['descriptionSource'] in payload['sources'] for r in rows)
assert all(r['aliases'] == [] for r in rows), 'Do not fabricate Russian official names'
(ROOT / 'runtime-content.json').write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8')
checks = dict(rows=len(rows), russianDescriptions=1025, officialEnglishNames=1025, verifiedCyrillicAliases=0,
              offline=True, canonicalFlavorTranslation=False,
              runtimeBytes=(ROOT / 'runtime-content.json').stat().st_size)
(ROOT / 'runtime-content-checks.json').write_text(json.dumps(checks, indent=2) + '\n', encoding='utf-8')
print(json.dumps(checks))
