from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parent
sheet=Image.new('RGB',(1680,330),'#f6f0e3')
for i in range(4):
    p=root/f'dresser-d{i+1}-context.png'
    im=Image.open(p).convert('RGBA').resize((480,193),Image.Resampling.NEAREST)
    im.save(root/f'dresser-d{i+1}-logical.png')
    crop=im.crop((320,50,460,150)).resize((420,300),Image.Resampling.NEAREST)
    sheet.paste(crop,(420*i,25))
    d=ImageDraw.Draw(sheet);d.text((420*i+5,5),f'D{i+1}: source crop x320..460 y50..150',fill='black')
sheet.save(root/'dresser-inspection.png')
