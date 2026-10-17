# fg_fishgenome_morphs.py (FishGenome art, 2026-10-17): two rare colour-morph skin tiles per species, built the way skins_batch1-4 were: one 512 x 512
# sRGB tile on the species' own UV0 layout (body box bottom-left, fins/tail above, eye strip on the top 7%), staged as separate PNGs and composed into
# a sheet with the same flipbook convention (4 columns, layer = row * 4 + column, row 0 at the top).
# The morphs are recolours of the committed tile in OKLab (painted detail, scales, stripes and fin rays stay), so they sit on the same UV layout, the
# same eye strip (rows 0..35 of the image are copied untouched: white | black eye texels and the species swatches) and keep the baked AO in vertex
# colour valid. Recipes: morph 1 is a colour swap, morph 2 a "special tone" (ink, pearl, koi, dusk, candy, ...). Deterministic (no random numbers).
# Output: Art/Export/FishGenome/Skins/<Species>_Morph<N>.png, Art/Export/FishGenome/T_FishMorphArray.png (50 tiles, layer = (skinLayer - 1) * 2 + N - 1),
# fishgenome_morphs.json (recipe names and layers). Run: python3 Art/Blender/fg_fishgenome_morphs.py (numpy + Pillow, or inside Blender).
import os, sys, json, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fg_genome_common as G

OUT = os.path.join(G.root(), 'Art', 'Export', 'FishGenome')
BG = np.array([0.1708, 0.1708, 0.1001], dtype=np.float32)      # the hole fill of the baked tiles (stored value of lin(0.45, 0.45, 0.35))
STRIP_ROWS = int(round(G.TILE * 0.07))          # image rows 0..35 hold the eye strip (v >= 0.93) and are never recoloured

# ---- OKLab -------------------------------------------------------------------------------------------------------------------------------------
M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566], [0.0883024619, 0.2817188376, 0.6299787005]])
M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099], [0.0259040371, 0.7827717662, -0.8086757660]])

def srgb_to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin_to_srgb(l): return np.where(l <= 0.0031308, 12.92 * l, 1.055 * np.power(np.maximum(l, 0.0), 1 / 2.4) - 0.055)

def to_lch(rgb):
    lms = np.cbrt(srgb_to_lin(rgb) @ M1.T)
    L, a, b = np.moveaxis(lms @ M2.T, -1, 0)
    return L, np.hypot(a, b), np.degrees(np.arctan2(b, a)) % 360.0

def from_lch(L, C, h):
    a = C * np.cos(np.radians(h)); b = C * np.sin(np.radians(h))
    lab = np.stack([L, a, b], axis=-1)
    inv2 = np.linalg.inv(M2); inv1 = np.linalg.inv(M1)
    lin = (lab @ inv2.T) ** 3 @ inv1.T
    return np.clip(lin_to_srgb(np.clip(lin, 0.0, 1.0)), 0.0, 1.0)

def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0); return t * t * (3 - 2 * t)

def hue_mix(h0, h1, t):
    d = ((h1 - h0 + 180.0) % 360.0) - 180.0
    return (h0 + d * t) % 360.0

# ---- recipes -----------------------------------------------------------------------------------------------------------------------------------
# kind 'swap'  : hue -> target (+ keep fraction of the original hue spread), chroma = max(C * gain, floor), L = L * lgain + lift
# kind 'split' : hue and chroma ramp from the dark colour (back) to the light colour (belly) by lightness
RECIPES = {
    'Sunburst': dict(kind='swap', hue=78, keep=0.30, gain=1.30, floor=0.085, lgain=1.12, lift=0.05),
    'Ember':    dict(kind='swap', hue=38, keep=0.30, gain=1.35, floor=0.10, lgain=1.00, lift=0.02),
    'Crimson':  dict(kind='swap', hue=18, keep=0.20, gain=1.15, floor=0.12, lgain=0.90, lift=0.0),
    'Violet':   dict(kind='swap', hue=312, keep=0.25, gain=1.20, floor=0.085, lgain=1.00, lift=0.03),
    'Cobalt':   dict(kind='swap', hue=255, keep=0.30, gain=1.25, floor=0.09, lgain=1.00, lift=0.03),
    'Teal':     dict(kind='swap', hue=198, keep=0.30, gain=1.25, floor=0.08, lgain=1.05, lift=0.03),
    'Lime':     dict(kind='swap', hue=128, keep=0.30, gain=1.30, floor=0.10, lgain=1.10, lift=0.04),
    'Ink':      dict(kind='swap', hue=268, keep=0.20, gain=0.70, floor=0.035, lgain=0.62, lift=0.0),
    'Pearl':    dict(kind='swap', hue=72, keep=0.15, gain=0.30, floor=0.015, lgain=0.55, lift=0.42),
    'Koi':      dict(kind='koi'),
    'Dusk':     dict(kind='split', dark=(305, 0.095), light=(68, 0.11), lgain=1.05, lift=0.04),
    'Candy':    dict(kind='split', dark=(10, 0.13), light=(205, 0.09), lgain=0.95, lift=0.0),
}
COLOUR = ['Sunburst', 'Ember', 'Crimson', 'Violet', 'Cobalt', 'Teal', 'Lime']
COLOUR_HUE = {'Sunburst': 78, 'Ember': 38, 'Crimson': 18, 'Violet': 312, 'Cobalt': 255, 'Teal': 198, 'Lime': 128}
SPECIAL = ['Ink', 'Pearl', 'Koi', 'Dusk', 'Candy']

def apply(rgb, rec):
    L, C, h = to_lch(rgb)
    prot = 1.0 - ss(0.86, 0.96, L) * (1.0 - ss(0.02, 0.06, C))             # near-white outlines and glints keep their white
    kind = rec['kind']
    if kind == 'swap':
        w = C + 1e-6
        mean_h = math.degrees(math.atan2((w * np.sin(np.radians(h))).sum(), (w * np.cos(np.radians(h))).sum())) % 360.0
        dh = ((h - mean_h + 180.0) % 360.0) - 180.0
        h2 = (rec['hue'] + dh * rec['keep']) % 360.0
        C2 = np.maximum(C * rec['gain'], rec['floor'] * ss(0.05, 0.25, L))
        L2 = L * rec['lgain'] + rec['lift']
    elif kind == 'split':
        t = ss(0.25, 0.70, L)
        h2 = hue_mix(rec['dark'][0], rec['light'][0], t)
        C2 = np.maximum(C * 0.8, rec['dark'][1] + (rec['light'][1] - rec['dark'][1]) * t) * ss(0.05, 0.25, L)
        L2 = L * rec['lgain'] + rec['lift']
    else:                                                                  # koi: white belly, strong orange blotches, black accents
        live = np.abs(rgb - BG).max(axis=-1) > 0.02
        flat = np.sort(L[live]); rank = np.searchsorted(flat, L) / max(len(flat), 1)      # rank within this tile: koi pattern is relative
        t = ss(0.52, 0.68, rank)
        L2 = 0.50 + 0.45 * t + 0.1 * (L - 0.5)
        h2 = hue_mix(42, 90, t); C2 = (0.17 * (1 - t) + 0.01 * t) * ss(0.05, 0.25, L)
    L2 = np.clip(L2, 0.0, 1.0)
    out = from_lch(L2, C2, h2)
    orig = rgb
    return orig * (1 - prot[..., None]) + out * prot[..., None]

def natural_hue(rgb):
    L, C, h = to_lch(rgb[STRIP_ROWS:])
    m = (L > 0.25) & (C > 0.03)
    w = C[m]; hh = np.radians(h[m])
    return math.degrees(math.atan2((w * np.sin(hh)).sum(), (w * np.cos(hh)).sum())) % 360.0 if m.any() else 60.0

def pick(i, nat):
    """morph 1: a colour swap whose hue is >= 50 degrees away from the species' own; morph 2: a special tone, cycled so neighbours differ."""
    order = [COLOUR[(i * 3 + 1 + k) % len(COLOUR)] for k in range(len(COLOUR))]
    for nm in order:
        d = abs(((COLOUR_HUE[nm] - nat + 180.0) % 360.0) - 180.0)
        if d >= 50.0: m1 = nm; break
    else: m1 = order[0]
    m2 = SPECIAL[(i * 2 + 1) % len(SPECIAL)]
    return m1, m2

def run():
    sheet = G.read_rgb(os.path.join(G.root(), 'Art', 'Export', 'T_FishSkinArray_m3.png'))
    os.makedirs(os.path.join(OUT, 'Skins'), exist_ok=True)
    layers = {}
    info = []
    nslots = 2 * len(G.SPECIES)
    rows = (nslots + 3) // 4
    morph_sheet = np.ones((rows * G.TILE, 4 * G.TILE, 3), dtype=np.float32)
    for i, n in enumerate(G.SPECIES):
        base = G.skin_tile(G.LAYER[n], sheet)
        nat = natural_hue(base)
        for N, rname in enumerate(pick(i, nat), start=1):
            t = base.copy()
            t[STRIP_ROWS:] = apply(base[STRIP_ROWS:], RECIPES[rname])
            res = G.write_png(os.path.join(OUT, 'Skins', '%s_Morph%d.png' % (n, N)), t)
            layer = (G.LAYER[n] - 1) * 2 + (N - 1)
            r, c = divmod(layer, 4)
            morph_sheet[r * G.TILE:(r + 1) * G.TILE, c * G.TILE:(c + 1) * G.TILE] = t
            info.append(dict(species=n, morph=N, name='%s_Morph%d' % (n, N), recipe=rname, file='Skins/%s_Morph%d.png' % (n, N), morphLayer=layer,
                             baseSkinLayer=G.LAYER[n], naturalHueDeg=round(nat)))
    G.write_png(os.path.join(OUT, 'T_FishMorphArray.png'), morph_sheet)
    with open(os.path.join(OUT, 'fishgenome_morphs.json'), 'w') as f:
        json.dump(dict(sheet='T_FishMorphArray.png', columns=4, rows=rows, tile=G.TILE, layerFormula='(skinLayer - 1) * 2 + (morph - 1)', morphs=info), f, indent=1)
    print('FGMORPH', len(info), 'tiles;', ', '.join('%s:%s/%s' % (n, info[2 * i]['recipe'], info[2 * i + 1]['recipe']) for i, n in enumerate(G.SPECIES)))

if __name__ == '__main__': run()
