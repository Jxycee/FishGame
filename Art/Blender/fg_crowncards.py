# fg_crowncards.py (harbour round 2, 2026-10-16; user unlock "painterly crown trees"): the painted crown-card atlas for FishGame/Toon's
# _ALPHATEST_ON path (M_Toon_Foliage). Made by us, procedurally: no downloaded or third-party image is used.
#
# Run:  blender -b --factory-startup --python Art/Blender/fg_crowncards.py
# Writes Assets/_Project/Content/Textures/T_CrownCards.png (1024 x 1024 RGBA, 8 bit). Deterministic: every draw comes from random.Random(seed)
# on a named stream (crc32); numpy only rasterises.
#
# Atlas (UV space, v up; the cards in fg_harbour.py use these rectangles: CARD_UV there):
#   A  u 0.0-0.5, v 0.5-1.0   broadleaf clump: ~110 scalloped leaf clusters on a dome, lit from the upper left, darker underside, a ragged rim
#   B  u 0.5-1.0, v 0.5-1.0   conifer silhouette (side view): a trunk spire with six drooping, needle-edged tiers
#   C  u 0.0-0.5, v 0.0-0.5   frond burst (top-down tier, PEAK's crown card idea): 14 forked fronds radiating from the centre
#   D  u 0.5-0.75, v 0.0-0.5  grass tuft: ~46 tapered, curving blades       E  u 0.75-1.0, v 0.0-0.5  flower tuft: stalks and petal heads
# RGB is greyscale paint centred near 0.5 (the material multiplies it by 2 x the palette colour); A is the cutout mask (clip at 0.5).
import math, os, random, zlib
import numpy as np

ROOT = r'C:\Dev\FishGame-harbour-art'
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Textures', 'T_CrownCards.png')
SIZE = 1024
SEED = 0xC40C0C5

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode('utf-8')))

class Cell:
    # one atlas cell: value (grey) and alpha arrays, coordinates x right, y up in [0, 1] x [0, aspect]
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.val = np.zeros((h, w), np.float32)
        self.alpha = np.zeros((h, w), np.float32)

    def grid(self, x0, y0, x1, y1):
        # pixel index box covering [x0, x1] x [y0, y1] (cell units: x in 0..1, y in 0..h/w)
        s = self.w
        i0, i1 = max(0, int(math.floor(x0 * s))), min(self.w, int(math.ceil(x1 * s)) + 1)
        j0, j1 = max(0, int(math.floor(y0 * s))), min(self.h, int(math.ceil(y1 * s)) + 1)
        if i0 >= i1 or j0 >= j1:
            return None
        xs = (np.arange(i0, i1, dtype=np.float32) + 0.5) / s
        ys = (np.arange(j0, j1, dtype=np.float32) + 0.5) / s
        X, Y = np.meshgrid(xs, ys)
        return (slice(j0, j1), slice(i0, i1)), X, Y

    def blob(self, cx, cy, r, k=7, amp=0.16, phase=0.0, light=(-0.5, 0.62, 0.6), base=0.3, gain=0.5, lift=0.0, rim=0.12, spiky=False):
        # a scalloped leaf cluster: radius r (1 + amp sin(k theta + phase)), shaded as a dome; drawn over what is there (painter's order)
        g = self.grid(cx - r * (1 + amp), cy - r * (1 + amp), cx + r * (1 + amp), cy + r * (1 + amp))
        if g is None:
            return
        sl, X, Y = g
        dx, dy = X - cx, Y - cy
        th = np.arctan2(dy, dx)
        wave = np.sin(k * th + phase)
        if spiky:
            wave = np.sign(wave) * np.abs(wave) ** 0.45
        rr = r * (1.0 + amp * wave)
        d = np.sqrt(dx * dx + dy * dy) / rr
        inside = d < 1.0
        if not inside.any():
            return
        dd = np.clip(d, 0.0, 1.0)
        nz = np.sqrt(1.0 - dd * dd)
        nx, ny = dx / r, dy / r
        L = np.array(light, np.float32); L /= np.linalg.norm(L)
        lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0.0, 1.0)
        v = base + gain * lam + lift
        # a painted rim: darker on the lower edge of each cluster (the gap shadow), lighter on the upper edge
        edge = np.clip((dd - (1.0 - rim)) / rim, 0.0, 1.0)
        v = v - 0.16 * edge * np.clip(-ny * 1.4 + 0.3, 0.0, 1.0) + 0.06 * edge * np.clip(ny, 0.0, 1.0)
        # paint grain: soft value noise (three rotated low-frequency waves per cluster, its own phase), no stripes
        v = v + 0.03 * (np.sin(X * 41.0 + phase * 5.0) * np.sin(Y * 37.0 - phase * 3.0) + 0.6 * np.sin((X - Y) * 73.0 + phase))
        cur = self.val[sl]
        cur[inside] = v[inside]
        self.alpha[sl][inside] = 1.0

    def blade(self, x0, y0, x1, y1, bend, w0, v0, v1, segs=24):
        # a tapered, curving blade from (x0, y0) to (x1, y1) (quadratic bend sideways), width w0 at the base to 0 at the tip
        pts = []
        for i in range(segs + 1):
            t = i / segs
            x = x0 + (x1 - x0) * t + bend * t * t
            y = y0 + (y1 - y0) * t
            pts.append((x, y, w0 * (1.0 - t) ** 0.85 + 0.0015, v0 + (v1 - v0) * t))
        for (xa, ya, wa, va), (xb, yb, wb, vb) in zip(pts, pts[1:]):
            g = self.grid(min(xa, xb) - wa, min(ya, yb) - wa, max(xa, xb) + wa, max(ya, yb) + wa)
            if g is None:
                continue
            sl, X, Y = g
            ex, ey = xb - xa, yb - ya
            L2 = ex * ex + ey * ey + 1e-9
            t = np.clip(((X - xa) * ex + (Y - ya) * ey) / L2, 0.0, 1.0)
            px, py = xa + ex * t, ya + ey * t
            d = np.sqrt((X - px) ** 2 + (Y - py) ** 2)
            w = wa + (wb - wa) * t
            inside = d < w
            side = np.clip(d / np.maximum(w, 1e-4), 0.0, 1.0)
            v = va + (vb - va) * t + 0.06 * (1.0 - side)            # a lighter midrib
            self.val[sl][inside] = v[inside]
            self.alpha[sl][inside] = 1.0

def broadleaf(c):
    r = rng('broadleaf')
    cx, cy, R = 0.5, 0.5, 0.43
    # the ragged rim first (behind): small clusters around the outline, a few loose leaf groups poking out
    rim = []
    for i in range(70):
        a = r.uniform(0, 2 * math.pi)
        d = R * r.uniform(0.78, 1.02)
        rim.append((cx + d * math.cos(a), cy + d * math.sin(a) * 0.92, r.uniform(0.028, 0.05)))
    for x, y, rad in sorted(rim, key=lambda p: p[1]):
        c.blob(x, y, rad, k=r.randint(7, 10), amp=0.24, phase=r.uniform(0, 6.3), base=0.3, gain=0.4, lift=0.12 * (y - 0.5), spiky=True)
    # the body: clusters on a dome, drawn bottom to top so the upper, lighter clusters overlap the lower ones
    body = []
    for i in range(120):
        a = r.uniform(0, 2 * math.pi)
        d = R * 0.84 * math.sqrt(r.random())
        body.append((cx + d * math.cos(a), cy + d * math.sin(a) * 0.9, r.uniform(0.055, 0.105)))
    for x, y, rad in sorted(body, key=lambda p: p[1] + 0.25 * (p[0] - 0.5) * 0.0):
        lift = 0.2 * (y - 0.5) - 0.06 * max(0.0, x - 0.62)
        c.blob(x, y, rad, k=r.randint(11, 15), amp=r.uniform(0.06, 0.1), phase=r.uniform(0, 6.3), base=0.32, gain=0.44, lift=lift, rim=0.1)
    # a few highlight clusters near the top left (sun side)
    for i in range(14):
        a = r.uniform(math.radians(95), math.radians(170))
        d = R * r.uniform(0.35, 0.75)
        c.blob(cx + d * math.cos(a), cy + d * math.sin(a) * 0.9, r.uniform(0.04, 0.07), k=12, amp=0.08, phase=r.uniform(0, 6.3),
               base=0.42, gain=0.42, lift=0.06)

def conifer(c):
    r = rng('conifer')
    tiers = 7
    # trunk spire (thin, dark), then the tiers from the bottom up (each upper tier overlaps the one below)
    c.blade(0.5, 0.0, 0.5, 0.97, 0.0, 0.016, 0.22, 0.3, segs=8)
    for i in range(tiers):
        f = i / (tiers - 1)
        yt = 0.16 + 0.76 * f
        half = 0.47 * (1.0 - f) ** 0.95 + 0.05
        droop = 0.10 + 0.05 * (1.0 - f)
        n = int(10 + 16 * (1.0 - f))
        pts = []
        for s in (-1, 1):
            for j in range(n):
                t = (j + r.uniform(0.0, 0.8)) / n
                x = 0.5 + s * half * t
                y = yt - droop * t ** 1.5 + r.uniform(-0.012, 0.012)
                rad = (0.04 * (1.0 - 0.55 * t) + 0.016) * (0.85 + 0.3 * r.random())
                pts.append((x, y, rad, t))
        for x, y, rad, t in sorted(pts, key=lambda p: -p[3]):
            c.blob(x, y, rad, k=r.randint(9, 13), amp=0.3, phase=r.uniform(0, 6.3), base=0.28, gain=0.42,
                   lift=0.1 * f + 0.08 * t - 0.04, spiky=True, rim=0.18)
    c.blob(0.5, 0.955, 0.03, k=9, amp=0.3, phase=1.0, base=0.3, gain=0.4, spiky=True)

def fronds(c):
    r = rng('fronds')
    n = 14
    for i in range(n):
        a = 2 * math.pi * i / n + r.uniform(-0.12, 0.12)
        curve = r.uniform(-0.35, 0.35)
        L = r.uniform(0.36, 0.46)
        steps = 16
        for j in range(steps):
            t = j / (steps - 1)
            aa = a + curve * t * t
            d = 0.04 + L * t
            x, y = 0.5 + d * math.cos(aa), 0.5 + d * math.sin(aa)
            rad = 0.055 * (1.0 - 0.6 * t) + 0.012
            c.blob(x, y, rad, k=11, amp=0.3, phase=r.uniform(0, 6.3), base=0.3, gain=0.4, lift=0.12 * (1.0 - t) - 0.04, spiky=True, rim=0.2)
            if t > 0.7 and j % 3 == 0:                       # a forked tip
                for s in (-1, 1):
                    fa = aa + s * 0.45
                    c.blob(x + 0.04 * math.cos(fa), y + 0.04 * math.sin(fa), rad * 0.8, k=9, amp=0.3, phase=r.uniform(0, 6.3),
                           base=0.26, gain=0.4, spiky=True)
    c.blob(0.5, 0.5, 0.07, k=8, amp=0.2, phase=0.4, base=0.34, gain=0.4)

def grass(c):
    r = rng('grass')
    for i in range(46):
        x0 = 0.5 + r.uniform(-0.26, 0.26)
        h = r.uniform(0.42, 0.93)
        lean = (x0 - 0.5) * r.uniform(0.6, 1.4) + r.uniform(-0.12, 0.12)
        c.blade(x0, 0.0, x0 + lean * 0.5, h, lean * 0.35, r.uniform(0.012, 0.022), r.uniform(0.2, 0.3), r.uniform(0.6, 0.78))

def flowers(c):
    r = rng('flowers')
    for i in range(18):
        x0 = 0.5 + r.uniform(-0.3, 0.3)
        h = r.uniform(0.3, 0.7)
        c.blade(x0, 0.0, x0 + r.uniform(-0.08, 0.08), h, r.uniform(-0.05, 0.05), 0.016, 0.18, 0.4)
    heads = []
    for i in range(9):
        heads.append((0.5 + r.uniform(-0.34, 0.34), r.uniform(0.48, 0.92), r.uniform(0.05, 0.08)))
    for x, y, rad in sorted(heads, key=lambda p: p[1]):
        c.blade(x, 0.0, x, y, r.uniform(-0.03, 0.03), 0.012, 0.18, 0.34, segs=10)
        c.blob(x, y, rad, k=r.choice((5, 6)), amp=0.38, phase=r.uniform(0, 6.3), base=0.55, gain=0.4, rim=0.25)
        c.blob(x, y, rad * 0.32, k=6, amp=0.1, phase=0.0, base=0.2, gain=0.2)          # the dark centre

def main():
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    half = SIZE // 2
    jobs = [(broadleaf, 0, half, half, half), (conifer, half, half, half, half), (fronds, 0, 0, half, half),
            (grass, half, 0, half // 2, half), (flowers, half + half // 2, 0, half // 2, half)]
    for fn, x0, y0, w, h in jobs:
        c = Cell(w, h)
        if fn in (grass, flowers):
            # the tufts are tall cells: paint in a square cell of the height and squeeze x
            sq = Cell(h, h)
            fn(sq)
            xs = (np.arange(w) + 0.5) / w * h
            idx = np.clip(xs.astype(int), 0, h - 1)
            c.val, c.alpha = sq.val[:, idx], sq.alpha[:, idx]
        else:
            fn(c)
        v = np.clip(c.val, 0.0, 1.0)
        img[y0:y0 + h, x0:x0 + w, 0] = v
        img[y0:y0 + h, x0:x0 + w, 1] = v
        img[y0:y0 + h, x0:x0 + w, 2] = v
        img[y0:y0 + h, x0:x0 + w, 3] = c.alpha
    # transparent pixels: the cell's mean grey (Unity's alphaIsTransparency dilates the colour too; this keeps the PNG tidy)
    a = img[..., 3]
    for ch in range(3):
        img[..., ch] = np.where(a > 0.0, img[..., ch], 0.45)
    import bpy
    name = 'FG_CrownCards'
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    im = bpy.data.images.new(name, SIZE, SIZE, alpha=True, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(img.reshape(-1))
    im.filepath_raw = OUT
    im.file_format = 'PNG'
    im.save()
    print('CROWNCARDS', OUT, 'coverage', [round(float(img[y:y + half, x:x + half, 3].mean()), 3) for x, y in ((0, half), (half, half), (0, 0), (half, 0))])

if __name__ == '__main__':
    main()
