# fg_fishgenome_masks.py (FishGenome art, 2026-10-17): the 8 greyscale pattern masks in the "Pat" UV frame of fg_fishgenome.py, plus the packed atlas.
# Pure numpy, procedural (no photo source, so nothing to record beyond Art/Textures/FishGenome/SOURCES.md). Deterministic: every noise field comes
# from RandomState(SEED ^ crc32(stream)). Runs under python3 or Blender's python: python3 Art/Blender/fg_fishgenome_masks.py
#
# Frame (tile 0..1, v = 1 at the top row): body nose at u = 0.02, body end at u = 0.70 (PAT_U0 / PAT_LEN), body axis at v = 0.50, isotropic, so a feature
# of 0.05 tile units is 7% of the body length on every species. The tail lies at u 0.70..0.87, fins above/below the body. u > 0.945 or v > 0.945 is
# forced to 0: the eye texel (0.975, 0.975) must stay 0. Ink 1 = pattern colour, 0 = base skin.
# Atlas: 4 columns x 2 rows of 512 px tiles, layer = row * 4 + column (row 0 at the top), the same flipbook convention as T_FishSkinArray_m3.png.
import os, sys, zlib, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fg_genome_common as G

N = 512
G_U0, G_LEN = 0.02, 0.68                                    # PAT_U0, PAT_LEN of fg_fishgenome.py
OUT = os.path.join(G.root(), 'Art', 'Textures', 'FishGenome')
NAMES = ['Stripes', 'Bars', 'Spots', 'Rosettes', 'Marbled', 'Mottled', 'Saddles', 'TwoTone']

yy, xx = np.mgrid[0:N, 0:N]
U = (xx + 0.5) / N
V = 1.0 - (yy + 0.5) / N

def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0); return t * t * (3 - 2 * t)

def rs(stream):
    return np.random.RandomState((G.SEED ^ zlib.crc32(stream.encode())) & 0xffffffff)

def vnoise(r, f, ox=0.0, oy=0.0):
    """smooth value noise, f lattice cells across the tile, 0..1"""
    g = r.random_sample((f + 3, f + 3))
    x = (U + ox) * f; y = (V + oy) * f
    i = np.floor(x).astype(int); j = np.floor(y).astype(int); fx = x - i; fy = y - j
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[j, i] * (1 - fx) + g[j, i + 1] * fx; b = g[j + 1, i] * (1 - fx) + g[j + 1, i + 1] * fx
    return a * (1 - fy) + b * fy

def fbm(r, f, oct_=4):
    s = np.zeros((N, N)); amp = 1.0; tot = 0.0
    for o in range(oct_):
        s += amp * vnoise(r, f * 2 ** o); tot += amp; amp *= 0.5
    return s / tot

def stamp(img, cx, cy, fn, rad):
    """evaluate fn(dx, dy) in a window of +-rad tile units around (cx, cy) and max it into img"""
    x0 = max(int((cx - rad) * N), 0); x1 = min(int((cx + rad) * N) + 1, N)
    y0 = max(int((1 - cy - rad) * N), 0); y1 = min(int((1 - cy + rad) * N) + 1, N)
    if x0 >= x1 or y0 >= y1: return
    dx = U[y0:y1, x0:x1] - cx; dy = V[y0:y1, x0:x1] - cy
    img[y0:y1, x0:x1] = np.maximum(img[y0:y1, x0:x1], fn(dx, dy))

def stripes():
    r = rs('stripes')
    w = fbm(r, 5, 3); w2 = fbm(r, 14, 2)
    y = (V - 0.5) + 0.005 * np.sin(2 * np.pi * U * 2.6 + 1.3) + 0.010 * (w - 0.5)
    per = 0.050
    p = np.abs(((y / per) % 1.0) - 0.5) * 2          # 1 on the line centre... 0 between
    line = ss(0.50, 0.30, p)
    broken = ss(0.22, 0.34, w2)                       # a few gaps in the lines
    fade = ss(0.08, 0.17, U) * (1 - ss(0.80, 0.88, U))
    return line * broken * fade

def bars():
    r = rs('bars')
    n = 7; per = G_LEN / n                              # 7 bars over the body length
    w = fbm(r, 6, 3)
    x = (U - G_U0) + 0.010 * (w - 0.5) + 0.003 * np.sin(2 * np.pi * V * 5.0)
    p = np.abs(((x / per) % 1.0) - 0.5) * 2           # 1 mid-gap .. 0 bar centre
    bar = ss(0.52, 0.34, p)
    span = ss(G_U0 + 0.15, G_U0 + 0.22, U) * (1 - ss(G_U0 + G_LEN - 0.12, G_U0 + G_LEN - 0.04, U))
    belly = ss(0.28, 0.38, V)                         # bars fade out under the lower flank (v < 0.30..0.38)
    return bar * span * belly

def spots():
    r = rs('spots'); img = np.zeros((N, N)); cell = 0.054
    for gy in range(int(1 / cell) + 1):
        for gx in range(int(1 / cell) + 1):
            cx = (gx + 0.5 + (r.random_sample() - 0.5) * 0.7) * cell; cy = (gy + 0.5 + (r.random_sample() - 0.5) * 0.7) * cell
            rad = 0.010 + 0.009 * r.random_sample(); keep = r.random_sample() > 0.15
            if not keep or cx < 0.09 or cx > 0.90 or cy < 0.04 or cy > 0.92: continue
            stamp(img, cx, cy, lambda dx, dy, rad=rad: ss(rad, rad * 0.62, np.sqrt(dx * dx + dy * dy)), rad + 0.01)
    return img

def rosettes():
    r = rs('rosettes'); img = np.zeros((N, N)); cell = 0.082
    for gy in range(int(1 / cell) + 1):
        for gx in range(int(1 / cell) + 1):
            cx = (gx + 0.5 + (r.random_sample() - 0.5) * 0.55) * cell + (0.041 if gy % 2 else 0.0)
            cy = (gy + 0.5 + (r.random_sample() - 0.5) * 0.55) * cell
            R = 0.022 + 0.010 * r.random_sample(); th = 0.0065 + 0.0035 * r.random_sample()
            p1, p2 = r.random_sample() * 6.28, r.random_sample() * 6.28; keep = r.random_sample() > 0.12
            if not keep or cx < 0.09 or cx > 0.90 or cy < 0.04 or cy > 0.92: continue
            def ring(dx, dy, R=R, th=th, p1=p1, p2=p2):
                d = np.sqrt(dx * dx + dy * dy); a = np.arctan2(dy, dx)
                Re = R * (1 + 0.20 * np.sin(2 * a + p1) + 0.12 * np.sin(3 * a + p2))
                return ss(th, th * 0.45, np.abs(d - Re))
            stamp(img, cx, cy, ring, R * 1.45 + 0.012)
            if r.random_sample() > 0.5:                       # a speck inside some rosettes
                stamp(img, cx, cy, lambda dx, dy: ss(0.006, 0.003, np.sqrt(dx * dx + dy * dy)) * 0.9, 0.012)
    return img

def marbled():
    r = rs('marbled')
    wx = fbm(r, 4, 3); wy = fbm(r, 4, 3)
    # domain-warped ridged noise -> thin veins; isotropic in tile units
    a = vnoise(r, 9, 0.30 * (wx - 0.5), 0.30 * (wy - 0.5)) * 0.65 + vnoise(r, 19, 0.15 * (wy - 0.5), 0.15 * (wx - 0.5)) * 0.35
    ridge = 1 - np.abs(2 * a - 1)
    vein = ss(0.935, 0.985, ridge)
    b = fbm(r, 12, 3); vein2 = ss(0.955, 0.99, 1 - np.abs(2 * b - 1)) * 0.55
    return np.maximum(vein, vein2)

def mottled():
    r = rs('mottled')
    m = fbm(r, 7, 4); fine = fbm(r, 30, 2)
    return ss(0.47, 0.58, m + 0.10 * (fine - 0.5)) * (0.78 + 0.22 * fine)

def saddles():
    r = rs('saddles'); img = np.zeros((N, N))
    for k, x0 in enumerate([0.17, 0.28, 0.40, 0.52, 0.62, 0.745]):       # the last one sits on the tail stalk
        cx = x0 + (r.random_sample() - 0.5) * 0.03
        half = (0.038 if k < 5 else 0.022) * (0.85 + 0.3 * r.random_sample())
        low = 0.495 - 0.05 * r.random_sample(); arch = 0.045 if k < 5 else 0.02
        dx = U - cx
        edge = low + arch * np.minimum(np.abs(dx) / half, 1.5) ** 2           # the saddle hangs lowest in its middle
        img = np.maximum(img, ss(half, half * 0.55, np.abs(dx)) * ss(edge - 0.012, edge + 0.012, V))
    return img

def twotone():
    r = rs('twotone'); w = fbm(r, 5, 2)
    line = 0.535 - 0.035 * ss(0.05, 0.70, U) + 0.006 * (w - 0.5) + 0.004 * np.sin(2 * np.pi * U * 5)
    return ss(line - 0.010, line + 0.010, V)


MAKERS = [stripes, bars, spots, rosettes, marbled, mottled, saddles, twotone]

def finish(a):
    a = np.clip(a, 0.0, 1.0)
    a[(U > 0.945) | (V > 0.945)] = 0.0                      # the eye texel and the clamp margin stay 0
    return a

def run():
    os.makedirs(OUT, exist_ok=True)
    tiles = [finish(mk()) for mk in MAKERS]
    res = []
    for i, (nm, t) in enumerate(zip(NAMES, tiles)):
        res.append(G.write_png(os.path.join(OUT, 'PatternMask_%d_%s.png' % (i, nm)), t))
    atlas = np.zeros((2 * N, 4 * N))
    for i, t in enumerate(tiles):
        r, c = divmod(i, 4); atlas[r * N:(r + 1) * N, c * N:(c + 1) * N] = t
    res.append(G.write_png(os.path.join(OUT, 'T_FishPatternArray.png'), atlas))
    print('FGMASKS', ' '.join(res), 'coverage', [round(float(t.mean()), 3) for t in tiles])

if __name__ == '__main__': run()
