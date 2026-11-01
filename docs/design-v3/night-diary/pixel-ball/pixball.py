"""Pixel-art Poké Ball, closed to open, drawn by a tiny ray caster and quantised to the Night Diary palette.
Frames share one canvas and one ball centre, so the lower half never moves between them."""
import math, sys
from collections import Counter
from PIL import Image

G = 40                      # canvas, pixels
CX, CY, R = 19.5, 26.0, 12.4  # ball centre and radius in pixels
PITCH = math.radians(16)    # viewer looks down onto the ball
BAND = 0.11                 # half-height of the dark band, in radii
BTN, RING = 0.27, 0.36      # button and ring angular radius, radians
L = (-0.55, -0.65, 0.52); n = math.sqrt(sum(v * v for v in L)); L = tuple(v / n for v in L)

def hx(c): return tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) + (255,)
OUT = hx('#2A1F3D')
RED = [hx(c) for c in ('#6E2846', '#A23C55', '#D2565F', '#EC7A74')]
APRICOT = hx('#FFB574')
WHT = [hx(c) for c in ('#7E68B4', '#B39BE6', '#DCD1F5', '#F6F1FD')]
BANDC = [hx(c) for c in ('#2A1F3D', '#3B2C55')]
INNER = [hx(c) for c in ('#1C1430', '#2A1F45', '#3B2D5E')]
LIDIN = [hx(c) for c in ('#5E4C8C', '#7D69B0', '#9C88D0', '#B9A6E6')]
RIM = hx('#C9B6F2')
BTNC = [hx(c) for c in ('#B39BE6', '#E4DBF7', '#FFFFFF')]

def rotx(p, a):
    x, y, z = p; c, s = math.cos(a), math.sin(a)
    return (x, y * c - z * s, y * s + z * c)

def band_of(v, levels): return levels[max(0, min(len(levels) - 1, int(v * len(levels))))]

def sphere_hits(o, d):
    # unit sphere, ray o + t d, returns (t_near, t_far) or None
    b = sum(o[i] * d[i] for i in range(3)); c = sum(v * v for v in o) - 1
    disc = b * b - c
    if disc < 0: return None
    s = math.sqrt(disc); return (-b - s, -b + s)

def angle_to_front(p):  # angular distance of a surface point from the button axis (0,0,1)
    return math.acos(max(-1, min(1, p[2])))

def shade(normal):
    return max(0.0, sum(normal[i] * L[i] for i in range(3)))

def sample(px, py, lid_angle):
    # view space: x right, y down, z toward viewer; orthographic ray along -z
    x = (px - CX) / R; y = (py - CY) / R
    o_view, d_view = (x, y, 5.0), (0, 0, -1.0)
    # ball space = view rotated back by the pitch
    o, d = rotx(o_view, PITCH), rotx(d_view, PITCH)
    best = None  # (t, colour, object)

    hits = sphere_hits(o, d)
    if hits:
        for k, t in enumerate(hits):
            p = tuple(o[i] + d[i] * t for i in range(3))
            if p[1] < 0: continue                                   # lower shell only
            in_notch = angle_to_front(p) < RING and p[1] < RING     # semicircle cut where the button sat
            if k == 0 and not in_notch:
                nv = rotx(p, -PITCH)
                col = band_of(shade(nv) ** .8, BANDC) if p[1] < BAND else band_of(.35 + .65 * shade(nv), WHT)
                best = (t, col, 'low'); break
            if k == 1:
                nv = rotx(tuple(-v for v in p), -PITCH)
                best = (t, RIM if p[1] < 0.09 else band_of(.25 + .75 * shade(nv), INNER), 'low'); break

    # lid: hemisphere y <= 0 (plus the button disc) hinged at the back point (0,0,-1), lifted by lid_angle
    hinge = (0.0, 0.14, -0.86)
    def to_lid(p): # inverse of lid rotation, applied around the hinge
        q = (p[0] - hinge[0], p[1] - hinge[1], p[2] - hinge[2]); q = rotx(q, -lid_angle)
        return (q[0] + hinge[0], q[1] + hinge[1], q[2] + hinge[2])
    ol = to_lid(o); dl = rotx(d, -lid_angle)
    hits = sphere_hits(ol, dl)
    if hits:
        for k, t in enumerate(hits):
            p = tuple(ol[i] + dl[i] * t for i in range(3))
            a = angle_to_front(p)
            on_lid = p[1] <= 0 or (a < RING)
            if not on_lid: continue
            if best and t >= best[0]: break
            # back to view space normal: lid frame -> ball frame -> view
            def world(nrm): return rotx(rotx(nrm, lid_angle), -PITCH)
            if k == 0:
                nv = world(p)
                if a < BTN: col = band_of(.45 + .55 * shade(nv), BTNC)
                elif a < RING: col = OUT
                elif p[1] > -BAND: col = band_of(shade(nv) ** .8, BANDC)
                else:
                    s = shade(nv)
                    col = APRICOT if s > .93 else band_of(.2 + .8 * s, RED)
                best = (t, col, 'lid'); break
            else:
                nv = world(tuple(-v for v in p))
                best = (t, LIDIN[0] if p[1] > -0.06 else band_of(.15 + .45 * shade(nv) + .4 * (1 + p[1]), LIDIN), 'lid'); break
    return best

def render(lid_deg, ss=3):
    a = math.radians(lid_deg)
    cols, objs, depth = {}, {}, {}
    for py in range(G):
        for px in range(G):
            votes, ts, ob = Counter(), [], Counter()
            for sy in range(ss):
                for sx in range(ss):
                    h = sample(px + (sx + .5) / ss, py + (sy + .5) / ss, a)
                    if h: votes[h[1]] += 1; ob[h[2]] += 1; ts.append(h[0])
                    else: votes[None] += 1
            c, _ = votes.most_common(1)[0]
            if c is not None:
                cols[(px, py)] = c; objs[(px, py)] = ob.most_common(1)[0][0]; depth[(px, py)] = sum(ts) / len(ts)
    img = Image.new('RGBA', (G, G), (0, 0, 0, 0)); pix = img.load()
    for (px, py), c in cols.items():
        edge = False
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (px + dx, py + dy)
            if q not in cols: edge = True
            elif objs[q] != objs[(px, py)] and depth[(px, py)] > depth[q] + 0.02: edge = True
        pix[px, py] = OUT if edge else c
    return img

def frames():
    return [render(d) for d in (0, 16, 32, 48)]

if __name__ == '__main__':
    out = sys.argv[1]
    for deg in (0, 16, 32, 48):
        render(deg).save(f'{out}/pixball-{deg}.png')
    print('ok')
