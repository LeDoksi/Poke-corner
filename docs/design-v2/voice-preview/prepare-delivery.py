"""Prepare the approved audio only; runtime/check mode needs no external archive."""
import argparse
import array
import hashlib
import json
import math
import random
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import wave
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent
AUDIO = ROOT / 'audio'
MANIFEST = ROOT / 'delivery.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect(path):
    with wave.open(str(path), 'rb') as wav:
        assert wav.getcomptype() == 'NONE' and wav.getsampwidth() == 2
        assert wav.getframerate() == 44100 and wav.getnchannels() in (1, 2)
        assert wav.getnframes() > 0
        assert len(wav.readframes(wav.getnframes())) == wav.getnframes() * wav.getnchannels() * 2
        return dict(container='WAV', encoding='PCM16', sampleRate=44100,
                    channels=wav.getnchannels(), frames=wav.getnframes())


def check():
    manifest = json.loads(MANIFEST.read_text('utf-8'))
    selected = json.loads((ROOT / 'selected-sources.json').read_text('utf-8'))
    assert len(selected) == 868 and len({r['id'] for r in selected}) == 868
    expected = {r['id']: r['source'] for r in selected}
    assert {r['id']: r['source'] for r in manifest['voices']} == expected
    catalog = json.loads((ROOT.parent / 'catalog-package/catalog.json').read_text('utf-8'))
    assert set(expected) == {r['id'] for r in catalog['species'] if r['assets']['normal']['animated']}
    ready = [r for r in manifest['voices'] if r['status'] == 'ready']
    for row in ready + manifest['effects']:
        path = ROOT / row['path']
        assert path.resolve().is_relative_to(AUDIO.resolve())
        assert path.is_file() and sha(path) == row['sha256']
        assert path.stat().st_size == row['bytes'] and inspect(path) == row['format']
    pending = [r for r in manifest['voices'] if r['status'] != 'ready']
    assert all(r['source'] == 'pokeapi-latest' and r['reason'] for r in pending)
    assert sum(r['source'] == 'itch' for r in ready) == 717
    assert sum(r['source'] == 'pixelmon' for r in ready) == 127
    assert len(ready) + len(pending) == 868
    assert sum(r['temporary'] for r in manifest['voices']) == 24
    assert all(r['sourceUrl'].startswith('https://raw.githubusercontent.com/PokeAPI/cries/')
               and r['apiUrl'] == f'https://pokeapi.co/api/v2/pokemon/{r["id"]}/'
               for r in ready if r['temporary'])
    assert all(row['resourceStatus'] == delivery['status']
               for row, delivery in zip(selected, manifest['voices']))
    report = dict(voices=len(ready), pending=len(pending), effects=len(manifest['effects']),
                  sha256AndPcmChecked=True, runtimeDownloadsDependency=False,
                  bytes=sum(r['bytes'] for r in ready + manifest['effects']))
    (ROOT / 'delivery-checks.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report), flush=True)


def fetch_pending():
    manifest = json.loads(MANIFEST.read_text('utf-8'))
    ffmpeg = shutil.which('ffmpeg')
    assert ffmpeg
    with tempfile.TemporaryDirectory(prefix='pokecorner-pending-cries-', dir=ROOT) as temp:
        for row in manifest['voices']:
            if row['status'] == 'ready':
                continue
            assert row['source'] == 'pokeapi-latest'
            try:
                request = urllib.request.Request(row['apiUrl'], headers={'User-Agent': 'PokeCorner-approved-audio-delivery'})
                with urllib.request.urlopen(request, timeout=30) as response:
                    url = json.load(response)['cries']['latest']
                assert url and url.startswith('https://raw.githubusercontent.com/PokeAPI/cries/')
                row['sourceUrl'] = url
                original = Path(temp) / f'{row["id"]}.ogg'
                with urllib.request.urlopen(url, timeout=30) as response:
                    original.write_bytes(response.read())
                output = AUDIO / f'{row["id"]}.wav'
                subprocess.run([ffmpeg, '-nostdin', '-v', 'error', '-y', '-i', str(original),
                                '-ar', '44100', '-c:a', 'pcm_s16le', str(output)], check=True, capture_output=True)
                row.update(status='ready', path='audio/' + output.name, originalSha256=sha(original),
                           sha256=sha(output), bytes=output.stat().st_size, format=inspect(output))
                row.pop('reason', None)
            except Exception as error:
                row['reason'] = f'{type(error).__name__}: {error}'
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    deliveries = {r['id']: r for r in manifest['voices']}
    selected = json.loads((ROOT / 'selected-sources.json').read_text('utf-8'))
    for row in selected:
        delivery = deliveries[row['id']]
        row['resourceStatus'] = delivery['status']
        if delivery['status'] == 'ready':
            row['deliveryPath'] = delivery['path']
            row.pop('pendingReason', None)
        else:
            row['pendingReason'] = delivery['reason']
    (ROOT / 'selected-sources.json').write_text(json.dumps(selected, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    check()


def prepare(archive):
    selected = json.loads((ROOT / 'selected-sources.json').read_text('utf-8'))
    itch = json.loads((ROOT / 'extra-itch/inventory.json').read_text('utf-8'))
    assert sha(archive) == itch['archiveSha256'], 'Unapproved itch archive'
    ffmpeg = shutil.which('ffmpeg')
    assert ffmpeg, 'FFmpeg is required only when preparing delivery'
    members = subprocess.run(['tar', '-tf', str(archive)], check=True, capture_output=True, text=True).stdout.splitlines()
    indexed = {}
    for member in members:
        match = re.fullmatch(r'Pokemon Anime Cries/[^/]+/([0-9]+)\.wav', member, re.I)
        if match:
            sid = int(match.group(1))
            assert sid not in indexed
            indexed[sid] = member
    pixelmon = json.loads((ROOT / 'pixelmon/inventory.json').read_text('utf-8'))
    pixelmon_rows = {r['id']: r for r in pixelmon['candidates']}
    AUDIO.mkdir(exist_ok=True)

    def fetch(url):
        request = urllib.request.Request(url, headers={'User-Agent': 'PokeCorner-approved-audio-delivery'})
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.read()

    with tempfile.TemporaryDirectory(prefix='pokecorner-approved-audio-', dir=ROOT) as temp:
        temp = Path(temp)
        wanted = [indexed[r['id']] for r in selected if r['source'] == 'itch']
        # Extract only verified regular audio paths; avoid arbitrary members/links from the archive.
        for start in range(0, len(wanted), 150):
            subprocess.run(['tar', '-xf', str(archive), '-C', str(temp), *wanted[start:start+150]], check=True)

        def convert(row):
            sid, source = row['id'], row['source']
            output = AUDIO / f'{sid}.wav'
            entry = dict(id=sid, source=source, temporary=source == 'pokeapi-latest')
            if source == 'itch':
                original = temp / indexed[sid]
                entry.update(sourceUrl=itch['sourcePage'], archiveMember=indexed[sid],
                             archiveSha256=itch['archiveSha256'])
            elif source == 'pixelmon':
                candidate = pixelmon_rows[sid]
                original = ROOT / 'pixelmon' / candidate['localPath']
                assert original.stat().st_size == candidate['bytes']
                entry.update(sourceUrl=pixelmon['sourceUrl'], archiveMember=candidate['path'])
            else:
                api_url = f'https://pokeapi.co/api/v2/pokemon/{sid}/'
                entry.update(apiUrl=api_url, replacementTask='POKE-31')
                try:
                    cry_url = json.loads(fetch(api_url))['cries']['latest']
                    assert cry_url and cry_url.startswith('https://raw.githubusercontent.com/PokeAPI/cries/')
                    entry['sourceUrl'] = cry_url
                    original = temp / f'{sid}.ogg'
                    original.write_bytes(fetch(cry_url))
                except Exception as error:
                    return dict(entry, status='pending-fetch', reason=f'{type(error).__name__}: {error}')
            assert original.exists() and original.resolve().is_relative_to((temp if source != 'pixelmon' else ROOT).resolve())
            entry['originalSha256'] = sha(original)
            subprocess.run([ffmpeg, '-nostdin', '-v', 'error', '-y', '-i', str(original),
                            '-ar', '44100', '-c:a', 'pcm_s16le', str(output)], check=True, capture_output=True)
            return dict(entry, status='ready', path='audio/' + output.name, sha256=sha(output),
                        bytes=output.stat().st_size, format=inspect(output))

        with ThreadPoolExecutor(max_workers=4) as executor:
            voices = list(executor.map(convert, selected))

    # Original deterministic quiet granular noise, no samples from copyrighted recordings.
    rng = random.Random(20261011)
    samples = array.array('h')
    for i in range(7938):
        t = i / 44100
        envelope = sum(math.exp(-((t - center) / .013) ** 2) for center in (.02, .055, .105))
        samples.append(round(rng.uniform(-1, 1) * envelope * 2200))
    if sys.byteorder != 'little':
        samples.byteswap()
    crunch = AUDIO / 'feeding-crunch.wav'
    with wave.open(str(crunch), 'wb') as wav:
        wav.setparams((1, 2, 44100, 0, 'NONE', 'not compressed'))
        wav.writeframes(samples.tobytes())
    effect = dict(id='feeding-crunch', path='audio/' + crunch.name, sha256=sha(crunch),
                  bytes=crunch.stat().st_size, format=inspect(crunch),
                  source='Original deterministic procedural granular noise; no third-party audio',
                  generator='prepare-delivery.py', durationSeconds=.18, peakAmplitudeMax=2200/32768)
    manifest = dict(schemaVersion=1, voices=voices, effects=[effect],
                    runtimePolicy='Use bundled relative paths only; normal and shiny share species voice. No network or Downloads at runtime.',
                    listening='Structure and decoding verified; not individually reviewed by listening.',
                    publicDistribution='Source reuse rights remain unverified; prepared for the authorized private-use scope.')
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for row, delivery in zip(selected, voices):
        row['resourceStatus'] = delivery['status']
        if delivery['status'] == 'ready':
            row['deliveryPath'] = delivery['path']
        else:
            row['pendingReason'] = delivery['reason']
    (ROOT / 'selected-sources.json').write_text(json.dumps(selected, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    check()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, help='Explicit approved itch archive, only for preparation')
    parser.add_argument('--check', action='store_true', help='Offline validation of portable delivery')
    parser.add_argument('--fetch-pending', action='store_true', help='Retry approved PokéAPI cries only, no external archive')
    args = parser.parse_args()
    if args.check:
        check()
    elif args.fetch_pending:
        fetch_pending()
    else:
        assert args.archive, 'Pass --archive to prepare or --check for offline validation'
        prepare(args.archive)
