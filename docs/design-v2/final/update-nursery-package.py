#!/usr/bin/env python3
"""After build-nursery-families.py: copies the gift candidates into catalog-rules.json and refreshes the
size and SHA-256 of both files in runtime-package.json (the package is verified byte for byte)."""
import json, hashlib
from pathlib import Path

BASE = Path(__file__).resolve().parent.parent
rules_path = BASE / 'final/catalog-rules.json'
rules = json.loads(rules_path.read_text(encoding='utf-8'))
recipes = json.loads((BASE / 'nursery-recipes-draft.json').read_text(encoding='utf-8'))
rules['giftRecipeCandidates'] = sorted(recipes['acceptedStarterGifts']['candidateRecipeIds'])
rules_path.write_text(json.dumps(rules, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
package_path = BASE / 'final/runtime-package.json'
package = json.loads(package_path.read_text(encoding='utf-8'))
for entry in package['files']:
    if entry['target'] in ('final/catalog-rules.json', 'nursery-recipes-draft.json'):
        data = (BASE.parent.parent / entry['path']).read_bytes()
        entry['bytes'] = len(data); entry['sha256'] = hashlib.sha256(data).hexdigest()
package_path.write_text(json.dumps(package, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
print('gift candidates', len(rules['giftRecipeCandidates']))
