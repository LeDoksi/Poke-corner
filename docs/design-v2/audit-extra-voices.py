"""Inspect selected audio members without extracting arbitrary archive paths."""
import hashlib
import io
import json
import re
import shutil
import subprocess
import wave
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ARCHIVE = Path('C:/Users/Georgy/Downloads/Pokemon Anime Cries.rar')
TARGET = ROOT / 'voice-preview/extra-itch'
TARGET.mkdir(exist_ok=True)
PREVIEWS = TARGET / 'pcm'
PREVIEWS.mkdir(exist_ok=True)
FFMPEG = shutil.which('ffmpeg')
assert FFMPEG
members = subprocess.run(['tar', '-tf', str(ARCHIVE)], capture_output=True, text=True, check=True).stdout.splitlines()
indexed = {}
for member in members:
    match = re.search(r'/([0-9]+)\.wav$', member, re.I)
    if match:
        sid = int(match.group(1))
        assert sid not in indexed, f'Ambiguous species: {sid}'
        indexed[sid] = member
missing = json.loads((ROOT / 'voice-preview/missing-bw-voices.json').read_text('utf-8'))
wanted = [s for s in missing if s['id'] in indexed]

def inspect(species):
    sid = species['id']
    member = indexed[sid]
    audio = subprocess.run(['tar', '-xOf', str(ARCHIVE), member], capture_output=True, check=True).stdout
    assert audio, f'Empty audio: {sid}'
    target = TARGET / f'{sid}.wav'
    target.write_bytes(audio)
    row = dict(species, archiveMember=member, bytes=len(audio), sha256=hashlib.sha256(audio).hexdigest(), path=target.name)
    try:
        with wave.open(io.BytesIO(audio), 'rb') as stream:
            row.update(duration=stream.getnframes()/stream.getframerate(), channels=stream.getnchannels(),
                       sampleRate=stream.getframerate(), sampleWidth=stream.getsampwidth(), pcm=True)
            assert stream.getnframes() > 0
            assert stream.getnchannels() in (1, 2)
            assert len(stream.readframes(stream.getnframes())) == stream.getnframes()*stream.getnchannels()*stream.getsampwidth()
    except wave.Error as error:
        row.update(pcm=False, review=str(error))
    preview = PREVIEWS / f'{sid}.wav'
    subprocess.run([FFMPEG, '-v', 'error', '-y', '-i', str(target), '-ar', '44100', '-c:a', 'pcm_s16le', str(preview)], check=True)
    with wave.open(str(preview), 'rb') as stream:
        assert stream.getnframes() > 0 and stream.getsampwidth() == 2
        row.update(preview='pcm/' + preview.name, decoded=True, duration=stream.getnframes()/stream.getframerate())
    return row

with ThreadPoolExecutor(max_workers=4) as executor:
    rows = list(executor.map(inspect, wanted))
assert len(rows) == len(wanted)
assert len(set(r['id'] for r in rows)) == len(rows)
found = {r['id'] for r in rows}
report = {'sourcePage': 'https://swonevial.itch.io/alizarinredscvpacks',
    'archiveBytes': ARCHIVE.stat().st_size, 'archiveSha256': hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),
    'packSpeciesFiles': len(indexed), 'newBwCandidates': len(rows),
    'pcmReadable': sum(r['pcm'] for r in rows), 'remainingBw': [s for s in missing if s['id'] not in found],
    'decoded': sum(r['decoded'] for r in rows),
    'rows': rows, 'quality': 'Unreviewed by listening; presence and PCM structure only.'}
(TARGET / 'inventory.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: v for k, v in report.items() if k not in ('rows', 'remainingBw')}, ensure_ascii=False), flush=True)
print(json.dumps({'remainingCount': len(report['remainingBw']), 'sampleIds': [r['id'] for r in rows[:15]],
    'floatingPointWav': sum(not r['pcm'] for r in rows)}, ensure_ascii=False), flush=True)
