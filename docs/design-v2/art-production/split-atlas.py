"""Approved technical atlas cutting only: crop alpha padding, no painting/recoloring."""
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
from pathlib import Path
from PIL import Image, ImageDraw, PngImagePlugin

PRODUCTION = Path(__file__).resolve().parent
EMBED = Path('C:/Users/Georgy/.agents/skills/impeccable/scripts/embed-prompt.mjs')


def slices(image, grid, cells):
    image = image.convert('RGBA')
    alpha = image.getchannel('A')
    if alpha.getextrema()[0] != 0:
        raise ValueError('Atlas has no real transparent background')
    rows, cols = grid
    if rows < 1 or cols < 1:
        raise ValueError('Invalid grid')
    seen_ids, seen_slots, result, used_rects = set(), set(), [], []
    for cell in cells:
        asset_id, row, col = cell['id'], cell['row'], cell['col']
        if not re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+)*', asset_id):
            raise ValueError('Invalid asset id')
        if asset_id in seen_ids or (row, col) in seen_slots:
            raise ValueError('Duplicate asset or cell')
        if not (0 <= row < rows and 0 <= col < cols):
            raise ValueError('Cell outside atlas')
        seen_ids.add(asset_id); seen_slots.add((row, col))
        rect = tuple(cell.get('cellRect', (round(col*image.width/cols), round(row*image.height/rows),
                round((col+1)*image.width/cols), round((row+1)*image.height/rows))))
        if len(rect)!=4 or any(not isinstance(n,int) for n in rect) or not (
                0<=rect[0]<rect[2]<=image.width and 0<=rect[1]<rect[3]<=image.height):
            raise ValueError('Invalid explicit cell rectangle')
        if any(rect[0]<r[2] and rect[2]>r[0] and rect[1]<r[3] and rect[3]>r[1] for r in used_rects):
            raise ValueError('Overlapping atlas cells')
        used_rects.append(rect)
        tile = image.crop(rect)
        # Ignore near-invisible alpha noise only for locating the crop; pixels are unchanged.
        bounds = tile.getchannel('A').point(lambda v: 255 if v >= 8 else 0).getbbox()
        if not bounds:
            raise ValueError(f'Empty asset: {asset_id}')
        # Cutting across neighboring art must fail rather than silently truncate it.
        solid = tile.getchannel('A').point(lambda v: 255 if v >= 32 else 0).getbbox()
        if solid and (solid[0] < 1 or solid[1] < 1 or solid[2] > tile.width-1 or solid[3] > tile.height-1):
            raise ValueError(f'Art touches cell boundary: {asset_id}')
        inset = (max(0,bounds[0]-8),max(0,bounds[1]-8),min(tile.width,bounds[2]+8),min(tile.height,bounds[3]+8))
        cropped = tile.crop(inset)
        result.append((cell, cropped, rect, bounds, inset))
    return result


def run(spec_path):
    spec_path = Path(spec_path).resolve()
    spec = json.loads(spec_path.read_text(encoding='utf-8'))
    resolve = lambda value: (spec_path.parent / value).resolve()
    source, prompt_file = resolve(spec['source']), resolve(spec['promptFile'])
    output = resolve(spec.get('outputDir', 'assets'))
    if not output.is_relative_to(PRODUCTION):
        raise ValueError('Output must stay inside art-production')
    prompt_file.read_text(encoding='utf-8')
    image = Image.open(source)
    results = slices(image, spec.get('grid', [2,2]), spec['cells'])
    output.mkdir(parents=True,exist_ok=True)
    records = []
    for cell, cropped, rect, bounds, inset in results:
        path = output / (cell['id']+'.png')
        meta = PngImagePlugin.PngInfo()
        meta.add_text('asset:id',cell['id'])
        meta.add_text('asset:source',source.name)
        cropped.save(path, pnginfo=meta)
        subprocess.run(['rtk','proxy','node',str(EMBED),str(path),'--prompt-file',str(prompt_file)],
                       check=True,capture_output=True,text=True,encoding='utf-8')
        records.append({**cell,'file':str(path.relative_to(PRODUCTION)).replace('\\','/'),
                        'size':list(cropped.size),'atlasSize':list(image.size),'cellRect':list(rect),
                        'alphaBoundsInCell':list(bounds),'cropInCell':list(inset),
                        'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                        'source':str(source.relative_to(PRODUCTION)).replace('\\','/'),
                        'promptFile':str(prompt_file.relative_to(PRODUCTION)).replace('\\','/'),
                        'status':'cut-alpha-verified-placement-review-pending'})
    manifest = {'status':'generated-and-cut-not-yet-all-combinations-tested',
                'grid':spec.get('grid',[2,2]),'assets':records}
    manifest_path = spec_path.with_name(spec_path.stem+'.manifest.json')
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'assets':len(records),'manifest':str(manifest_path)},ensure_ascii=False))


def self_test():
    image = Image.new('RGBA',(128,128))
    draw = ImageDraw.Draw(image)
    cells=[]
    for i in range(4):
        row,col=divmod(i,2)
        draw.rectangle((col*64+16,row*64+12,col*64+48,row*64+52),fill=(50+i*30,80,110,255))
        cells.append({'id':f'test-{i}','row':row,'col':col})
    results=slices(image,[2,2],cells)
    assert len(results)==4 and all(tile.size==(49,57) for _,tile,*_ in results)
    for broken in [Image.new('RGBA',(128,128),(255,255,255,255)),Image.new('RGBA',(128,128))]:
        try: slices(broken,[2,2],cells)
        except ValueError: pass
        else: raise AssertionError('Rejected atlas was accepted')
    for bad_cells in [[cells[0],cells[0]],[{'id':'../escape','row':0,'col':0}],
                      [{'id':'test-a','row':2,'col':0}],
                      [{**cells[0],'cellRect':[-1,0,64,64]}],
                      [{**cells[0],'cellRect':[0,0,70,64]}, {**cells[1],'cellRect':[64,0,128,64]}]]:
        try: slices(image,[2,2],bad_cells)
        except ValueError: pass
        else: raise AssertionError('Invalid cell was accepted')
    edge = image.copy()
    ImageDraw.Draw(edge).rectangle((0,10,30,30),fill=(255,0,0,255))
    try: slices(edge,[2,2],cells)
    except ValueError: pass
    else: raise AssertionError('Clipped art was accepted')
    uneven=[{**cells[0],'cellRect':[0,0,64,60]}]
    assert len(slices(image,[2,2],uneven))==1
    print('Atlas crop checks OK: alpha, dimensions, empty/opaque/boundary, ids and grid')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('spec',nargs='?')
    parser.add_argument('--self-test',action='store_true')
    args=parser.parse_args()
    if args.self_test: self_test()
    elif args.spec: run(args.spec)
    else: parser.error('spec or --self-test required')
