# fg_genome_common.py (FishGenome art, 2026-10-17): helpers shared by fg_fishgenome*.py. No side effects on import.
# Runs under plain python3 (numpy; Pillow optional) or Blender's python (numpy; falls back to bpy images for reading PNGs).
import os, zlib, struct, random
import numpy as np

SEED = 0xF15ED0C5                      # same seed as fg_harbour.py; every random stream is SEED ^ crc32(stream name)

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode('utf-8')))

def root():
    r = os.environ.get('FG_ROOT')
    if r: return r
    return os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

# Species roster in skin-array layer order (T_FishSkinArray_m3.png: layer = row * 4 + column, row 0 at the top; layer 0 is the white tile).
SPECIES = ['Bluegill', 'Perch', 'Carp', 'Trout', 'Catfish', 'MirrorMinnow', 'BucketmouthBass', 'SilverMullet', 'LakePike', 'FrostChar',
           'GlimmerEel', 'SleepySturgeon', 'GildedCarp', 'OldBarnacle', 'PierSardine', 'IceHerring', 'GrumbleMackerel', 'GawpingGurnard',
           'RedSnapper', 'GrumpyPuffer', 'MoonSquid', 'SneakyFlounder', 'BruiserTuna', 'StormRay', 'Lanternfish']
LAYER = {n: i + 1 for i, n in enumerate(SPECIES)}      # Bluegill 1 .. Lanternfish 25 (matches FishSkin_L06_MirrorMinnow.png etc.)

TILE = 512

def read_rgb(path):
    """-> float32 array (h, w, 3) in 0..1, row 0 = top of the image. Stored bytes, no colour management."""
    try:
        from PIL import Image
        return np.asarray(Image.open(path).convert('RGB'), dtype=np.float32) / 255.0
    except ImportError:
        import bpy
        img = bpy.data.images.load(path, check_existing=False)
        w, h = img.size
        a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]       # bpy rows run bottom-up
        if img.colorspace_settings.name != 'sRGB': pass
        bpy.data.images.remove(img)
        return a

def write_png(path, arr):
    """arr: (h, w) or (h, w, 3|4) float 0..1 or uint8, row 0 = top. Plain zlib PNG, deterministic bytes."""
    a = np.asarray(arr)
    if a.dtype != np.uint8: a = np.clip(np.rint(a * 255.0), 0, 255).astype(np.uint8)
    if a.ndim == 2: a = a[:, :, None]
    h, w, c = a.shape
    ctype = {1: 0, 3: 2, 4: 6}[c]
    raw = b''.join(b'\x00' + a[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ctype, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        with open(path, 'rb') as f:
            if f.read() == data: return 'kept'
    with open(path, 'wb') as f: f.write(data)
    return 'written'

def skin_tile(layer, sheet=None):
    """The 512 x 512 baseline skin tile of a species layer, cut from the committed sheet (4 columns, layer = row * 4 + column)."""
    if sheet is None: sheet = read_rgb(os.path.join(root(), 'Art', 'Export', 'T_FishSkinArray_m3.png'))
    r, c = divmod(layer, 4)
    return sheet[r * TILE:(r + 1) * TILE, c * TILE:(c + 1) * TILE]
