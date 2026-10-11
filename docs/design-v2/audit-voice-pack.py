"""Download a public candidate for inspection; do not install it in the game."""
import hashlib
import io
import json
import re
import shutil
import subprocess
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
TARGET = ROOT / 'voice-preview'
TARGET.mkdir(exist_ok=True)
versions = json.loads((ROOT / 'voice-source-modrinth.json').read_text('utf-8'))
source = versions[0]['files'][0]
archive_path = TARGET / source['filename']
if archive_path.exists():
    payload = archive_path.read_bytes()
else:
    request = urllib.request.Request(source['url'], headers={'User-Agent': 'PokeCorner-voice-audit'})
    with urllib.request.urlopen(request, timeout=60) as response:
        payload = response.read()
assert len(payload) == source['size'], 'Download size mismatch'
assert hashlib.sha512(payload).hexdigest() == source['hashes']['sha512'], 'Download hash mismatch'
(TARGET / source['filename']).write_bytes(payload)
with zipfile.ZipFile(io.BytesIO(payload)) as archive:
    files = archive.infolist()
    audio = [f for f in files if f.filename.lower().endswith(('.ogg', '.wav', '.mp3'))]
    configs = {}
    for entry in files:
        if entry.filename.lower().endswith('.json') and entry.file_size < 2_000_000:
            configs[entry.filename] = json.loads(archive.read(entry))
    report = {'sourceUrl': source['url'], 'archiveBytes': len(payload),
        'audioCount': len(audio), 'audioBytes': sum(f.file_size for f in audio),
        'files': [{'name': f.filename, 'bytes': f.file_size} for f in audio], 'configs': configs}
    catalog = json.loads((ROOT / 'catalog-package/catalog.json').read_text('utf-8'))['species']
    normalize = lambda name: re.sub(r'[^a-z0-9]', '', name.lower())
    names = {normalize(f.filename.split('/')[-2]): f.filename for f in audio if f.filename.endswith('/cry.ogg')}
    def path_for(species):
        for label in (species['defaultPokemon']['identifier'], species['species']['identifier'], 'pokemon_' + str(species['id'])):
            if normalize(label) in names:
                return names[normalize(label)]
        return None
    matched = [s for s in catalog if path_for(s)]
    report['matchedSpecies'] = len(matched)
    report['matchedBwNormalSpecies'] = sum(s['assets']['normal']['animated'] for s in matched)
    report['unmatchedFirst649'] = [{'id': s['id'], 'name': s['species']['identifier']} for s in catalog
        if s['id'] <= 649 and not path_for(s)]
    report['speciesMapping'] = [{'id': s['id'], 'path': path_for(s)} for s in matched]
    assert not report['unmatchedFirst649'], 'Claimed first-649 coverage incomplete'
    report['samples'] = []
    ffmpeg = shutil.which('ffmpeg')
    assert ffmpeg, 'Existing FFmpeg not found'
    ffprobe = Path(ffmpeg).with_name('ffprobe.exe')
    assert ffprobe.exists()
    for name in ['pikachu', 'eevee', 'bulbasaur', 'charizard', 'magikarp', 'gastly']:
        entry = names[normalize(name)]
        ogg = TARGET / (name + '.ogg')
        ogg.write_bytes(archive.read(entry))
        probe = subprocess.run([str(ffprobe), '-v', 'error', '-show_entries',
            'format=duration:stream=codec_name,sample_rate,channels', '-of', 'json', str(ogg)],
            capture_output=True, text=True, check=True)
        metadata = json.loads(probe.stdout)
        wav = TARGET / (name + '.wav')
        subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(ogg), '-c:a', 'pcm_s16le', str(wav)], check=True)
        report['samples'].append({'name': name, 'archiveEntry': entry, 'metadata': metadata, 'preview': wav.name})
    (TARGET / 'inventory.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('files', 'configs', 'speciesMapping')}))
