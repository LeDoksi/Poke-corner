"""Read ZIP directory/audio using HTTP ranges; never execute the mod."""
import io
import json
import shutil
import subprocess
from concurrent.futures import ThreadPoolExecutor
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
TARGET = ROOT / 'voice-preview/pixelmon'
TARGET.mkdir(exist_ok=True)
version = json.loads((ROOT / 'voice-preview/pixelmon-source.json').read_text('utf-8'))
source = version['files'][0]

class RemoteZip(io.RawIOBase):
    def __init__(self):
        self.position, self.blocks, self.received = 0, {}, 0
    def seekable(self):
        return True
    def tell(self):
        return self.position
    def seek(self, offset, whence=0):
        self.position = offset + (0 if whence == 0 else self.position if whence == 1 else source['size'])
        assert 0 <= self.position <= source['size']
        return self.position
    def read(self, size=-1):
        end = min(source['size'], self.position + size) if size >= 0 else source['size']
        assert end - self.position < 32_000_000, 'Refusing full archive download'
        parts = []
        while self.position < end:
            start = self.position // 524288 * 524288
            if start not in self.blocks:
                stop = min(start + 524288, source['size']) - 1
                request = urllib.request.Request(source['url'], headers={
                    'Range': f'bytes={start}-{stop}', 'User-Agent': 'PokeCorner-voice-audit'})
                with urllib.request.urlopen(request, timeout=60) as response:
                    assert response.status == 206, 'Server does not support range reads'
                    assert response.headers['Content-Range'] == f'bytes {start}-{stop}/{source["size"]}'
                    payload = response.read()
                assert len(payload) == stop - start + 1
                self.blocks[start] = payload
                self.received += len(payload)
            take = min(end - self.position, len(self.blocks[start]) - (self.position - start))
            assert take > 0
            parts.append(self.blocks[start][self.position-start:self.position-start+take])
            self.position += take
        return b''.join(parts)

stream = RemoteZip()
assert stream.read(4) == b'PK\x03\x04'
stream.seek(0)
with zipfile.ZipFile(stream) as archive:
    audio = [entry for entry in archive.infolist() if entry.filename.lower().endswith(('.ogg', '.wav', '.mp3'))]
    report = {'sourceUrl': source['url'], 'version': version['version_number'], 'archiveBytes': source['size'],
        'readBytes': stream.received, 'audio': [{'name': e.filename, 'bytes': e.file_size} for e in audio]}
    (TARGET / 'inventory.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'audioCount': len(audio), 'readBytes': stream.received, 'paths': [e.filename for e in audio[:30]]}), flush=True)
    targets_path = TARGET / 'targets.json'
    if targets_path.exists():
        targets = json.loads(targets_path.read_text('utf-8'))
        assert len({s['id'] for s in targets}) == len(targets)
        available = {e.filename for e in audio}
        for index, species in enumerate(targets):
            assert species['path'] in available
            path = TARGET / f'{species["id"]}.ogg'
            payload = path.read_bytes() if path.exists() else archive.read(species['path'])
            assert payload.startswith(b'OggS'), 'Expected OGG container'
            if not path.exists():
                path.write_bytes(payload)
            species['localPath'] = path.name
            species['bytes'] = len(payload)
            if (index + 1) % 25 == 0:
                print(f'Extracted {index + 1}/{len(targets)}', flush=True)
        ffmpeg = shutil.which('ffmpeg')
        assert ffmpeg
        samples = {727, 785, 810, 813, 831, 1000}
        def decode(species):
            path = TARGET / species['localPath']
            if species['id'] in samples:
                preview = TARGET / f'{species["id"]}.wav'
                args = ['-ar', '44100', '-c:a', 'pcm_s16le', str(preview)]
                species['preview'] = preview.name
            else:
                args = ['-f', 'null', '-']
            subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(path), *args], check=True, capture_output=True)
            species['decoded'] = True
            return species
        with ThreadPoolExecutor(max_workers=4) as executor:
            targets = list(executor.map(decode, targets))
        report.update(readBytes=stream.received, candidates=targets)
        (TARGET / 'inventory.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(json.dumps({'extracted': len(targets), 'decoded': sum(s['decoded'] for s in targets),
            'readBytes': stream.received, 'samples': [s for s in targets if 'preview' in s]}), flush=True)
