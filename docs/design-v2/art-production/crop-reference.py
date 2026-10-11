from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parent
source=root.parent/'room-layout-v2.png'
image=Image.open(source)
assert image.size==(1536,1024)
image.crop((44,231,1492,813)).save(root/'architecture/_sources/reference-room.png')
print('Reference-only room crop:1448x582. Never ship as production architecture.')
