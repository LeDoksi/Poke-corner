"""Technical context crop, mask, registration and nearest resize. No painted pixels."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, PngImagePlugin

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent.parent
SIZE = (480, 193)
GENERATED = Path('C:/Users/Georgy/.codex/generated_images/01a127f2-61c1-75d0-8b25-c5e4fe11f6a9')
FILES = {'t3':'exec-27535706-b2e4-4812-a16c-e134d3fb4966.png',
         't4':'exec-44d5f24e-e654-4d51-8041-777c3cc3d115.png',
         'a3':'exec-6509ed7c-ed43-4740-b059-37e17a515d03.png',
         'a4':'exec-3e7cdd1d-b278-48f0-9aed-bfad9147d965.png',
         'c2':'exec-a0578108-3cb0-4040-a498-88b6a41aa5d8.png',
         'c4':'exec-44ab8a38-ebaa-40d3-bae1-30251d50af36.png'}

def save(im, path, prompt, origin):
    info = PngImagePlugin.PngInfo()
    info.add_text('impeccable:prompt', prompt)
    info.add_text('impeccable:origin', json.dumps(origin, ensure_ascii=False))
    im.save(path, pnginfo=info)

def normalize(im):
    return im.crop((0, 0, im.width, round(im.width * 193/480))).resize(SIZE, Image.Resampling.NEAREST)

if __name__ == '__main__':
    (ROOT/'previews').mkdir(exist_ok=True)
    images = {}
    for key, filename in FILES.items():
        raw = Image.open(GENERATED/filename).convert('RGBA')
        prompt = (ROOT/'_sources'/f'{key}-prompt.txt').read_text(encoding='utf-8').strip()
        save(raw, ROOT/'_sources'/f'{key}-context.png', prompt,
             {'tool':'builtin image_gen.imagegen','generatedOriginal':str(GENERATED/filename), 'rawDimensions':raw.size})
        im = normalize(raw)
        save(im, ROOT/'_sources'/f'{key}-logical.png', prompt, {'source':f'{key}-context.png','method':'nearest grid normalization'})
        images[key]=im
    contact = Image.new('RGBA',(1440,579*3))
    for i,(key,im) in enumerate(images.items()):
        contact.alpha_composite(im.resize((720,290),Image.Resampling.NEAREST), ((i%2)*720,(i//2)*579))
    save(contact, ROOT/'previews'/'raw-contact.png', 'Technical contact sheet of six contextual generations.',{})
    print(json.dumps({'saved':list(images),'rawDimensions':Image.open(GENERATED/FILES['t3']).size}))
