"""Prepare the same six species from both sources without normalizing loudness."""
import hashlib
import json
import re
import shutil
import subprocess
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ARCHIVE = Path('C:/Users/Georgy/Downloads/Pokemon Anime Cries.rar')
TARGET = ROOT / 'voice-preview/comparison'
TARGET.mkdir(exist_ok=True)
ffmpeg = shutil.which('ffmpeg')
assert ffmpeg
members = subprocess.run(['tar', '-tf', str(ARCHIVE)], capture_output=True, text=True, check=True).stdout.splitlines()
rows = []
for sid, name in [(25, 'pikachu'), (133, 'eevee'), (1, 'bulbasaur'), (6, 'charizard'), (129, 'magikarp'), (92, 'gastly')]:
    matches = [m for m in members if re.search(r'/' + f'{sid:03d}' + r'\.wav$', m, re.I)]
    assert len(matches) == 1, f'Ambiguous or missing: {sid}'
    source = TARGET / f'itch-{name}-source.wav'
    source.write_bytes(subprocess.run(['tar', '-xOf', str(ARCHIVE), matches[0]], capture_output=True, check=True).stdout)
    preview = TARGET / f'itch-{name}.wav'
    subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(source), '-ar', '44100', '-c:a', 'pcm_s16le', str(preview)], check=True)
    previous = ROOT / f'voice-preview/{name}.wav'
    row = {'id': sid, 'name': name, 'newArchiveMember': matches[0], 'oldPath': str(previous), 'newPath': str(preview)}
    digests = []
    for label, path in [('old', previous), ('new', preview)]:
        with wave.open(str(path), 'rb') as stream:
            audio = stream.readframes(stream.getnframes())
            assert len(audio) == stream.getnframes()*stream.getnchannels()*stream.getsampwidth()
            digest = hashlib.sha256(audio).hexdigest()
            digests.append(digest)
            row[label] = {'duration': stream.getnframes()/stream.getframerate(), 'channels': stream.getnchannels(),
                'sampleRate': stream.getframerate(), 'sampleWidth': stream.getsampwidth(), 'pcmSha256': digest}
    row['identicalDecodedAudio'] = digests[0] == digests[1]
    rows.append(row)
(TARGET / 'inventory.json').write_text(json.dumps(rows, indent=2) + '\n', encoding='utf-8')
print(json.dumps([{'name': r['name'], 'oldDuration': r['old']['duration'], 'newDuration': r['new']['duration'],
    'identicalDecodedAudio': r['identicalDecodedAudio']} for r in rows]))
