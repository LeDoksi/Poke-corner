"""Canonical fixed-room +56-award export; source pixels untouched."""
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, PngImagePlugin

ROOT = Path(__file__).resolve().parent
LAST = ROOT.parent
BANK = LAST / 'trophies'
REVISION = LAST / 'trophy-revision'
for name in ('assets/trophies', 'assets/masks', 'previews'):
    (ROOT / name).mkdir(parents=True, exist_ok=True)
profile = json.loads((BANK / 'profile.json').read_text(encoding='utf-8'))
source_rows = json.loads((BANK / 'manifest.json').read_text(encoding='utf-8'))['direct']
bank = {row['id']: row for row in source_rows}
revision = {row['id']: row for row in json.loads((REVISION / 'manifest.json').read_text(encoding='utf-8'))['direct']}
assert len(revision) == 20 and set(revision) <= set(bank)
retired = [f'trophy-decor-{tier}' for tier in range(1,5)] + ['trophy-special-all-plants', 'trophy-special-three-pictures', 'trophy-special-mixed-furniture']
active = [row for row in source_rows if row['id'] not in retired]
assert len(bank) == 63 and len(active) == 56
assert len(list((BANK / 'assets').glob('*.png'))) == 63


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rgba_sha(image):
    return hashlib.sha256(image.convert('RGBA').tobytes()).hexdigest()


metadata_checks = []


def save(image, path, prompt, origin):
    meta = PngImagePlugin.PngInfo()
    meta.add_text('impeccable:prompt', prompt)
    meta.add_text('impeccable:prompt-json', json.dumps(prompt, ensure_ascii=True))
    meta.add_text('impeccable:origin', json.dumps(origin, ensure_ascii=True))
    image.save(path, pnginfo=meta)
    decoded = Image.open(path)
    assert decoded.convert('RGBA').tobytes() == image.convert('RGBA').tobytes()
    assert decoded.info['impeccable:prompt'] == prompt
    assert json.loads(decoded.info['impeccable:prompt-json']) == prompt
    assert json.loads(decoded.info['impeccable:origin']) == json.loads(json.dumps(origin))
    metadata_checks.append({'file': str(path.relative_to(ROOT)).replace('\\','/'), 'rgbaDecodedExact': True, 'promptAndOriginReadable': True, 'sha256': sha(path)})


master_source = LAST / 'master.png'
master_raw = Image.open(master_source)
master = master_raw.convert('RGBA')
assert master.size == (1448,582)
assert sha(master_source) == profile['masterSha256']
master_prompt = master_raw.info.get('impeccable:prompt') or (LAST / 'metadata-prompt.txt').read_text(encoding='utf-8')
save(master, ROOT / 'assets/room-master.png', master_prompt, {'source': '../master.png', 'sourceSha256': sha(master_source), 'method': 'Exact source pixels; metadata-only export', 'sourceMetadata': master_raw.info})
lip_rect = tuple(profile['frontlipRect'])
lip = master.crop(lip_rect)
save(lip, ROOT / 'assets/shelf-frontlip.png', master_prompt, {'source': '../master.png', 'sourceSha256': sha(master_source), 'cropRect': lip_rect, 'method': 'Exact intrinsic physical shelf-edge pixels'})

rows = []
images = {}
masks = {}
asset_checks = []
for canonical in active:
    asset_id = canonical['id']
    source_id = asset_id
    source_row = revision.get(asset_id, bank[source_id])
    source_root = REVISION if asset_id in revision else BANK
    source_relative = '../trophy-revision/' if asset_id in revision else '../trophies/'
    source_file = source_root / source_row['output_path']
    raw = Image.open(source_file)
    image = raw.convert('RGBA')
    alpha = image.getchannel('A')
    mask = Image.new('RGBA', image.size, (255,255,255,255))
    mask.putalpha(alpha)
    category = 'special' if asset_id.startswith('trophy-special-') else 'chain'
    chain_id = None if category == 'special' else asset_id.split('-')[1]
    tier = None if category == 'special' else int(asset_id.rsplit('-',1)[1])
    origin = {'canonicalId': asset_id, 'sourceId': source_id, 'sourceFile': source_relative + source_row['output_path'],
              'sourceSha256': sha(source_file), 'sourceRGBASha256': rgba_sha(image),
              'sourceMetadata': raw.info, 'method': 'Metadata-only canonical export; exact source RGBA pixels',
              'active': True, 'mappingReason': 'User-approved semantic art revision' if asset_id in revision else ('Original growing companion group restored' if asset_id.startswith('trophy-population-') else 'Identity mapping')}
    prompt = raw.info.get('impeccable:prompt', source_row['prompt_used'])
    target = ROOT / 'assets/trophies' / f'{asset_id}.png'
    mask_target = ROOT / 'assets/masks' / f'{asset_id}.png'
    save(image, target, prompt, origin)
    save(mask, mask_target, prompt, {**origin, 'method': 'White RGB255 opacity mask with exact unchanged source alpha'})
    assert Image.open(target).convert('RGBA').tobytes() == image.tobytes()
    assert Image.open(mask_target).getchannel('A').tobytes() == alpha.tobytes()
    assert all(channel.getextrema() == (255,255) for channel in mask.split()[:3])
    assert alpha.getextrema()[0] == 0 and alpha.getbbox()
    images[asset_id], masks[asset_id] = image, mask
    row = {'id': asset_id, 'category': category, 'chainId': chain_id, 'tier': tier, 'active': True,
           'sourceId': source_id, 'sourceFile': source_relative + source_row['output_path'],
           'sourceSha256': sha(source_file), 'sourceRGBASha256': rgba_sha(image),
           'file': f'assets/trophies/{asset_id}.png', 'maskFile': f'assets/masks/{asset_id}.png',
           'footAnchor': source_row['footAnchor'], 'logicalSize': source_row['dimensions'],
           'layers': ['room-master', 'independent-award', 'shelf-frontlip'], 'destinationAnchors': profile['anchors'],
           'qa_status': 'needs_parent_review', 'role': 'Active award in fixed detailed room; final user acceptance not inferred from export',
           'imagePixelsIdenticalToSource': True, 'maskAlphaIdenticalToSource': True}
    rows.append(row)
    asset_checks.append({'id': asset_id, 'sourceId': source_id, 'rgbaPixelIdentity': True, 'whiteMaskRGB': True, 'maskAlphaIdentity': True})

assert len(rows) == 56 and len({row['id'] for row in rows}) == 56
assert len(list((ROOT / 'assets/trophies').glob('*.png'))) == len(list((ROOT / 'assets/masks').glob('*.png'))) == 56
chains = [row for row in rows if row['category'] == 'chain']
specials = [row for row in rows if row['category'] == 'special']
assert len(chains) == 40 and len(specials) == 16 and len({row['chainId'] for row in chains}) == 10
album = [row for row in rows if row['category'] == 'special' or row['tier'] == 1]
late = [row for row in rows if row['category'] == 'special' or row['tier'] == 4]
assert len(album) == len(late) == 26
by_id = {row['id']: row for row in rows}
placements = []


def place(scene, row, slot):
    image = images[row['id']]
    anchor = profile['anchors'][slot]
    xy = tuple(round(anchor[i]-image.size[i]*row['footAnchor'][i]) for i in range(2))
    bbox = image.getchannel('A').getbbox()
    box = [xy[0]+bbox[0], xy[1]+bbox[1], xy[0]+bbox[2], xy[1]+bbox[3]]
    bounds = profile['slotBounds'][slot]
    assert bounds[0] <= box[0] < box[2] <= bounds[2] and bounds[1] <= box[1] < box[3] <= bounds[3]
    scene.alpha_composite(image, xy)
    return {'id': row['id'], 'sourceId': row['sourceId'], 'slot': slot+1, 'position': xy, 'actualAlphaBounds': box, 'slotBounds': bounds, 'fits': True}


for row in rows:
    for slot in range(3):
        placements.append(place(master.copy(), row, slot))
assert len(placements) == 168


def room(ids):
    scene = master.copy()
    union = Image.new('L', master.size)
    for slot, asset_id in enumerate(ids):
        box = place(scene, by_id[asset_id], slot)['actualAlphaBounds']
        ImageDraw.Draw(union).rectangle((box[0],box[1],box[2]-1,box[3]-1), fill=255)
    scene.paste(lip, lip_rect[:2])
    for channel in ImageChops.difference(scene, master).split():
        assert ImageChops.multiply(channel, ImageChops.invert(union)).getbbox() is None
    return scene


examples = {'empty': [], 'one-trophy': ['trophy-population-1'],
            'first-tiers': ['trophy-population-1','trophy-expeditions-1','trophy-care-1'],
            'last-tiers': ['trophy-population-4','trophy-expeditions-4','trophy-care-4']}
for name, ids in examples.items():
    save(room(ids), ROOT / 'previews' / f'room-{name}.png', master_prompt, {'method': 'Exact room/independent-award/frontlip composition', 'awardIds': ids, 'sourceIds': [by_id[asset_id]['sourceId'] for asset_id in ids], 'masterSha256': sha(master_source), 'outsideAwardsUnchanged': True, 'pet': None})

font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 12)


def contact(selected, name, locked=False):
    width,height,columns = 190,155,7
    sheet = Image.new('RGB',(width*columns,height*math.ceil(len(selected)/columns)), '#eee7d8')
    for index,row in enumerate(selected):
        x,y = (index%columns)*width,(index//columns)*height
        image = images[row['id']]
        if locked:
            image = Image.new('RGBA',image.size,(43,53,45,255)); image.putalpha(masks[row['id']].getchannel('A'))
        sheet.paste(image,(x+(width-image.width)//2,y+5),image)
        text = row['id'].removeprefix('trophy-')
        if len(text)>25:
            split = text.rfind('-',0,26);text = text[:split]+'\n'+text[split+1:]
        ImageDraw.Draw(sheet).multiline_text((x+4,y+108),text,font=font,fill='#29372f',spacing=2)
    save(sheet,ROOT/'previews'/f'{name}.png', 'Review template only: first-tier/special silhouettes for locked album, never future chain tiers.', {'canonicalIds':[row['id'] for row in selected], 'sourceIds':[row['sourceId'] for row in selected], 'locked':locked, 'futureChainTiersShown':not locked and any(row['tier'] == 4 for row in selected)})


contact(album,'album-open')
contact(album,'album-locked',True)
contact([row for row in chains if row['tier']==1],'first-tiers-open')
contact(late,'late-tiers-review-only')

manifest = {'version':1, 'scope':'One fixed detailed room and56independent active awards; full decor customization retired.',
            'room': {'file':'assets/room-master.png','dimensions':list(master.size),'sourceFile':'../master.png','sourceSha256':sha(master_source),'rgbaPixelIdentity':True},
            'frontlip': {'file':'assets/shelf-frontlip.png','rect':lip_rect,'dimensions':list(lip.size),'intrinsicSourcePixels':True},
            'shelf': {'anchors':profile['anchors'],'slotBounds':profile['slotBounds'],'maxAwardDimensions':profile['maxDimensions'],'renderOrder':['room-master','independent-awards','shelf-frontlip','pet-root-owned']},
            'trophies': rows, 'albumCards':[row['id'] for row in album], 'lockedCardSources':{row['id']:row['sourceId'] for row in album},
            'lateTierReviewOnlyCards':[row['id'] for row in late], 'retiredIds':retired,
            'sourceBank':{'file':'../trophies/manifest.json','count':63,'preserved':True,'oldPopulationVisuals':'Restored as active population chain; original source bank unchanged'},
            'artRevision':{'file':'../trophy-revision/manifest.json','newSources':20,'restoredPopulationAppearances':4,'canonicalIdsUnchanged':True},
            'counts':{'activeTrophies':56,'chainAppearances':40,'chains':10,'specials':16,'masks':56,'albumCards':26,'retiredAppearances':7},
            'semantic': [{'id':'locked-opacity-brush','implementation':'Use white RGBA mask as opacity brush, with UI-owned dark fill for locked card. Chain card uses first tier only while locked; do not use tier4to hint future appearance. Only active canonical id selects awarded source art. Room positions/lip stay independent; root owns pet and controls.','qa_status':'needs_parent_review'}],
            'execution_order':['Read detailed master/original bank/revised sources','Export canonical56with original population and20revised sources','Copy exact alpha into56white masks','Check168slot placements and metadata','Inspect final room and locked/open contacts'],
            'blockers':[],'assumptions':['All source63and retired art retained separately.','20new sources generated with builtin imagegen; technical cropping/fitting only.','No app/WPF/pet integration in this export.','Asset export is not user visual approval.']}
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
proof = {'status':'export-checks-passed-user-visual-acceptance-not-inferred','counts':manifest['counts'],
         'exportedRoomRGBAExact':True,'lipPixelsExact':True,'canonicalTrophyRGBAExact':True,'whiteMasksAlphaExact':True,
         'activePlacementsChecked':168,'retiredIdsAbsentFromExport':all(asset_id not in by_id for asset_id in retired),
         'originalBankStill63':len(bank)==63,'lockedAlbumUsesOnlyFirstTierAndSpecials':all(row['tier'] in (None,1) for row in album),
         'masterSha256':sha(master_source),'assetChecks':asset_checks,'placements':placements,'metadataChecks':metadata_checks}
proof['visualInspection'] = {'files':['previews/room-first-tiers.png','previews/album-open.png','previews/album-locked.png'],
                             'observed':'Population uses original growing companion group;20revised sources replace ambiguous botanical/camp art. Locked cards retain exact-alpha first-tier/special silhouettes.',
                             'userVisualApprovalClaimed':False}
(ROOT/'proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'counts':manifest['counts'],'slotChecks':168,'metadataFiles':len(metadata_checks),'allSourcePixelsExact':True,'allMaskAlphaExact':True}))
