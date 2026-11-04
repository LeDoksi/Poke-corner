import sys, numpy as np
from PIL import Image
from scipy import ndimage
SRC='/mnt/user-data/uploads/reactions-v1/assets/'
OUT=sys.argv[1]
def frames(a): return [a[(i//4)*64:(i//4)*64+64,(i%4)*64:(i%4)*64+64] for i in range(16)]
def clean(a, ncolors=14):
    a=a.copy()
    al=a[...,3]
    on=al>=110
    # drop specks: components under 2 px
    lab,n=ndimage.label(on,structure=np.ones((3,3)))
    sizes=ndimage.sum(on,lab,range(1,n+1))
    for k,sz in enumerate(sizes,1):
        if sz<2: on[lab==k]=False
    rgb=a[...,:3][on]
    # palette from opaque-ish pixels, weighted by alpha
    img=Image.fromarray(rgb.reshape(-1,1,3).astype(np.uint8))
    q=img.quantize(colors=ncolors,method=Image.Quantize.MEDIANCUT)
    pal=np.array(q.getpalette()[:ncolors*3]).reshape(-1,3)
    idx=np.array(q).reshape(-1)
    out=np.zeros_like(a)
    out[...,:3][on]=pal[idx]
    out[...,3][on]=255
    return out
def run(name, ncolors=14, post=None):
    a=np.array(Image.open(SRC+name+'.png').convert('RGBA'))
    c=clean(a,ncolors)
    if post: c=post(c)
    Image.fromarray(c).save(f'{OUT}/{name}.png')
    return c
if __name__=='__main__':
    import neglect3
    run('feeding',32)
    for n in ['petting','neglect-1','neglect-2']: run(n)
    run('neglect-3', post=neglect3.redraw)
