from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parent
files=sorted((root/'assets').glob('*.png'))
sheet=Image.new('RGB',(1200,((len(files)+7)//8)*140),(230,224,207))
d=ImageDraw.Draw(sheet)
for i,p in enumerate(files):
    im=Image.open(p).convert('RGBA'); im.thumbnail((100,100),Image.Resampling.NEAREST)
    x=(i%8)*150; y=(i//8)*140
    sheet.paste(im,(x+(150-im.width)//2,y),im)
    d.text((x+2,y+102),p.stem.replace('trophy-',''),fill=(20,25,20))
sheet.save(root/'_sources'/'contact.png')
