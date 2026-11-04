import numpy as np
from scipy import ndimage
def redraw(sheet):
    s=sheet.copy()
    for i in range(16):
        y0,x0=(i//4)*64,(i%4)*64
        f=s[y0:y0+64,x0:x0+64]
        op=f[...,3]==255
        r,g,b=[f[...,k].astype(int) for k in range(3)]
        coral=op&(r>190)&(g<130)&(b<140)
        if coral.sum()<4: continue
        ys,xs=np.nonzero(coral)
        # the X sits in the cloud centre; keep only the big central blob
        lab,n=ndimage.label(coral,structure=np.ones((3,3)))
        k=np.argmax(ndimage.sum(coral,lab,range(1,n+1)))+1
        blob=lab==k
        ys,xs=np.nonzero(blob)
        cy,cx=int(round(ys.mean())),int(round(xs.mean()))
        # cream sample: most common light colour in the frame
        light=op&(r>220)&(g>200)&(b>150)
        cols,cnt=np.unique(f[light].reshape(-1,4),axis=0,return_counts=True)
        cream=cols[cnt.argmax()]
        dark=op&(r<120)&(g<100)
        cols,cnt=np.unique(f[dark].reshape(-1,4),axis=0,return_counts=True)
        ink=cols[cnt.argmax()]
        blush=np.array([232,120,120,255])
        grown=ndimage.binary_dilation(blob,iterations=1)&op
        f[grown]=cream
        soft = i in (12,13)
        def put(dx,dy,c=ink):
            y,x=cy+dy,cx+dx
            if 0<=y<64 and 0<=x<64 and f[y,x,3]==255: f[y,x]=c
        if soft:
            # attention: the sulk melts — calm closed eyes and a tiny mouth
            for dx in (-7,-6,-5,5,6,7): put(dx,1)
            for dx in (-1,0,1): put(dx,4)
        else:
            # "ну всё, я обиделся": brows pulled down to the centre over demonstratively shut eyes, a pout
            for dx,dy in [(-8,-3),(-7,-3),(-6,-2),(-5,-2),(-4,-1)]: put(dx,dy); put(-dx,dy)   # brows
            for dx,dy in [(-7,1),(-6,1),(-5,1)]: put(dx,dy); put(-dx,dy)                     # shut eyes
            for dx,dy in [(-2,5),(-1,4),(0,4),(1,4),(2,5)]: put(dx,dy)                        # pout
        s[y0:y0+64,x0:x0+64]=f
    return s
