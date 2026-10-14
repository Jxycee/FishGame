# fg_envtextures.py (quality-first round 1, 2026-10-14): the painted surface layers 9-14 of the env detail array T_EnvDetailArray.png for
# FishGame/Toon (EnvTextureLayers). Made by us, procedurally: no downloaded or third-party image is used.
#
# Run:  blender -b --factory-startup --python Art/Blender/fg_envtextures.py
# Reads Assets/_Project/Content/Textures/T_EnvDetailArray.png (2048 x 2048, 4 x 4 tiles of 512, linear detail centred on 0.5; layers 0-8 come
# from the look-dev bake, Art/Blender/fishgame_lookdev.blend text block fg_textures.py, and are copied through unchanged), repaints tiles 9-14
# and writes the sheet back (RGB, 8 bit). Deterministic: every random draw comes from numpy's Generator(PCG64) seeded from a named stream
# (crc32); every layer tiles in u and v (periodic lattices and wrapped brush dabs). Layer 15 stays neutral.
#
# The PEAK / RV idea (research 2026-10-14-reference-style-notes.md): greyscale painted masks, colour from the palette slot and the grade.
#   9  CliffRock  big broken rock planes (a few voronoi chunks per tile, each with its own light plane), dark fracture lines, a hint of
#                 horizontal strata and horizontal brush strokes. For cliffs, sea stacks, ledges and glen banks (triplanar: v = world up).
#   10 StoneWall  stacked stone courses: rounded stones of varied width and height, a lit upper bevel and a dark lower lip, recessed mortar.
#   11 Plaster    soft limewash: broad blotches, faint brush sweeps, a few drip streaks from the top, fine speckle.
#   12 Dirt       a packed-earth path: small rounded pebbles with a lit top and a contact shadow, darker damp patches, grit.
#   13 Meadow     ground grass seen from above: short blade strokes in all directions, clover clumps, light/dark drifts.
#   14 RoofTile   clay tile rows along u (courses stacked along v): each tile lighter at its lower lip, a dark overlap shadow line.
import math, os, zlib
import numpy as np

ROOT = os.environ.get('FG_ROOT') or os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))   # repo root, wherever the branch is checked out
PATH = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Textures', 'T_EnvDetailArray.png')
TILE = 512
COLS = ROWS = 4
SEED = 0xE5D7A11


def gen(stream):
    return np.random.Generator(np.random.PCG64(SEED ^ zlib.crc32(stream.encode('utf-8'))))


# ---- periodic primitives (n x n, rows = v from the TOP of the tile, cols = u) ----

def grid(n=TILE):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n, (y + 0.5) / n


def value_noise(r, k, n=TILE):
    # periodic value noise: a k x k random lattice, smoothstep-interpolated with wrap
    lat = r.random((k, k)).astype(np.float32)
    x, y = grid(n)
    fx, fy = x * k, y * k
    ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    x0, y0, x1, y1 = ix % k, iy % k, (ix + 1) % k, (iy + 1) % k
    a = lat[y0, x0] * (1 - tx) + lat[y0, x1] * tx
    b = lat[y1, x0] * (1 - tx) + lat[y1, x1] * tx
    return a * (1 - ty) + b * ty


def fbm(r, k, octaves=4, gain=0.5, n=TILE):
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += amp * value_noise(r, k * (2 ** o), n)
        tot += amp
        amp *= gain
    return out / tot


def voronoi(r, k, jitter=0.9, n=TILE, warp=None, sx=1.0, sy=1.0):
    # periodic voronoi on a k x k jittered lattice: returns F1, F2 (in cell units), the nearest point's cell id and its offset (dx, dy)
    pts = r.random((k, k, 2)).astype(np.float32) * jitter + (1 - jitter) * 0.5
    x, y = grid(n)
    if warp is not None:
        x, y = x + warp[0], y + warp[1]
    fx, fy = x * k * sx, y * k * sy
    kx, ky = int(round(k * sx)), int(round(k * sy))
    kx, ky = max(kx, 1), max(ky, 1)
    ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    f1 = np.full((n, n), 9.0, np.float32)
    f2 = np.full((n, n), 9.0, np.float32)
    cid = np.zeros((n, n), np.int64)
    ddx = np.zeros((n, n), np.float32)
    ddy = np.zeros((n, n), np.float32)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cx, cy = ix + ox, iy + oy
            wx, wy = cx % kx, cy % ky
            p = pts[wy % k, wx % k]
            px, py = cx + p[..., 0], cy + p[..., 1]
            dx, dy = fx - px, fy - py
            d = np.sqrt(dx * dx + dy * dy)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            f1 = np.where(closer, d, f1)
            cid = np.where(closer, wy * kx + wx, cid)
            ddx = np.where(closer, dx, ddx)
            ddy = np.where(closer, dy, ddy)
    return f1, f2, cid, ddx, ddy


def dab(canvas, cx, cy, rx, ry, ang, value, soft=0.5):
    # a soft elliptical brush dab added to the canvas (wraps in u and v); cx, cy, rx, ry in pixels
    n = canvas.shape[0]
    rr = int(math.ceil(max(rx, ry))) + 2
    xs = np.arange(int(cx) - rr, int(cx) + rr + 1)
    ys = np.arange(int(cy) - rr, int(cy) + rr + 1)
    X, Y = np.meshgrid(xs + 0.5 - cx, ys + 0.5 - cy)
    c, s = math.cos(ang), math.sin(ang)
    u = (X * c + Y * s) / max(rx, 0.5)
    v = (-X * s + Y * c) / max(ry, 0.5)
    d = np.sqrt(u * u + v * v)
    m = np.clip((1.0 - d) / max(soft, 1e-3), 0.0, 1.0)
    m = m * m * (3 - 2 * m)
    canvas[np.ix_(ys % n, xs % n)] += value * m


def strokes(r, canvas, count, rmin, rmax, aspect, ang_mean, ang_spread, vmin, vmax, soft=0.6):
    n = canvas.shape[0]
    for _ in range(count):
        cx, cy = r.random() * n, r.random() * n
        rx = rmin + (rmax - rmin) * r.random()
        ry = rx / aspect
        ang = ang_mean + (r.random() * 2 - 1) * ang_spread
        dab(canvas, cx, cy, rx, ry, ang, vmin + (vmax - vmin) * r.random(), soft)


# ---- layers (each returns an n x n float array, detail centred on 0.5) ----

def cliff_rock():
    r = gen('cliff_rock')
    n = TILE
    warp = ((fbm(r, 3, 3) - 0.5) * 0.12, (fbm(r, 3, 3) - 0.5) * 0.06)
    # big chunks: each voronoi cell is a broken rock face with its own light plane (lit from the upper left, varied per chunk)
    f1, f2, cid, dx, dy = voronoi(r, 3, 0.85, warp=warp, sx=1.0, sy=1.0)
    nc = int(cid.max()) + 1
    ang = r.random(nc).astype(np.float32) * 1.2 + 2.0          # light-plane direction per chunk (mostly from the upper left)
    off = (r.random(nc).astype(np.float32) - 0.5) * 0.10
    plane = (np.cos(ang)[cid] * dx + np.sin(ang)[cid] * dy) * 0.10 + off[cid]
    out = 0.5 + plane
    # fracture lines between the chunks: a dark crack and a lit lip just above it
    edge = f2 - f1
    out -= 0.20 * np.clip(1.0 - edge / 0.06, 0, 1) ** 1.5
    out += 0.05 * np.clip(1.0 - np.abs(edge - 0.09) / 0.05, 0, 1)
    # smaller secondary fractures
    g1, g2, _, _, _ = voronoi(r, 7, 0.95, warp=warp)
    e2 = g2 - g1
    out -= 0.09 * np.clip(1.0 - e2 / 0.05, 0, 1)
    # gentle horizontal strata (warped), the rock's bedding
    x, y = grid(n)
    out += 0.035 * np.sin(2 * math.pi * (y * 4 + (fbm(r, 4, 3) - 0.5) * 0.9))
    # horizontal brush strokes (painted)
    canvas = np.zeros((n, n), np.float32)
    strokes(r, canvas, 900, 10, 34, 4.0, 0.0, 0.35, -0.05, 0.05)
    strokes(r, canvas, 500, 4, 12, 2.5, 0.0, 1.2, -0.04, 0.04)
    out += canvas
    out += (fbm(r, 32, 2) - 0.5) * 0.05
    return out


def stone_wall():
    r = gen('stone_wall')
    n = TILE
    out = np.full((n, n), 0.30, np.float32)          # mortar (recessed, dark)
    # course heights summing to the tile (wraps in v)
    heights = []
    while sum(heights) < n - 150:
        heights.append(int(r.integers(92, 150)))
    heights.append(n - sum(heights))
    if heights[-1] < 70:
        heights[-2] += heights[-1]
        heights.pop()
    y0 = 0
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    # irregular stones: warp the lookup a few pixels so the outlines wobble (periodic noise keeps the tile seamless)
    x = x + (fbm(r, 6, 3) - 0.5) * 22.0
    y = y + (fbm(r, 6, 3) - 0.5) * 14.0
    for h in heights:
        xpos = r.random() * n
        start = xpos
        while xpos < start + n:
            w = float(r.integers(120, 300))
            if xpos + w > start + n - 60:
                w = start + n - xpos
            # a rounded stone in [xpos, xpos+w] x [y0, y0+h] with a 5 px mortar gap, wrapped in u
            gap = 5.0 + r.random() * 3.0
            cx, cy = xpos + w / 2.0, y0 + h / 2.0
            hx, hy = w / 2.0 - gap, h / 2.0 - gap
            rad = min(hx, hy) * (0.35 + 0.3 * r.random())
            ddx = np.abs(((x - cx + n / 2) % n) - n / 2)
            ddy = np.abs(((y - cy + n / 2) % n) - n / 2)
            qx, qy = np.maximum(ddx - (hx - rad), 0), np.maximum(ddy - (hy - rad), 0)
            sd = np.sqrt(qx * qx + qy * qy) - rad          # < 0 inside
            inside = sd < 0
            if inside.any():
                val = 0.5 + (r.random() - 0.5) * 0.16
                depth = np.clip(-sd / 10.0, 0, 1)            # bevel ramp
                # light from above: the upper bevel lighter, the lower lip darker
                ny = np.clip((y - cy) / max(hy, 1), -1, 1)
                bevel = (1 - depth) * (-ny) * 0.10
                stone = val + bevel - (1 - depth) * 0.04
                out = np.where(inside, stone, out)
            xpos += w
        y0 += h
    canvas = np.zeros((n, n), np.float32)
    strokes(r, canvas, 700, 6, 22, 2.5, 0.0, 0.6, -0.04, 0.04)
    out += canvas
    out += (fbm(r, 16, 3) - 0.5) * 0.08
    out += (fbm(r, 64, 2) - 0.5) * 0.04
    return out


def plaster():
    r = gen('plaster')
    n = TILE
    out = 0.5 + (fbm(r, 3, 4) - 0.5) * 0.10
    canvas = np.zeros((n, n), np.float32)
    strokes(r, canvas, 260, 30, 80, 2.2, 0.0, 0.5, -0.03, 0.035)
    strokes(r, canvas, 600, 6, 18, 1.6, 0.0, 3.1, -0.025, 0.025)
    out += canvas
    # drip streaks: a few soft vertical darker runs
    x, y = grid(n)
    for _ in range(7):
        sx, w, a = r.random(), 0.006 + r.random() * 0.01, 0.025 + r.random() * 0.03
        du = np.abs(((x - sx + 0.5) % 1.0) - 0.5)
        out -= a * np.clip(1 - du / w, 0, 1) * (0.4 + 0.6 * fbm(r, 6, 2))
    out += (fbm(r, 96, 1) - 0.5) * 0.035
    return out


def dirt():
    r = gen('dirt')
    n = TILE
    out = 0.5 + (fbm(r, 4, 4) - 0.5) * 0.16
    f1, f2, cid, dx, dy = voronoi(r, 14, 0.9, warp=((fbm(r, 24, 2) - 0.5) * 0.03, (fbm(r, 24, 2) - 0.5) * 0.03))
    nc = int(cid.max()) + 1
    size = (r.random(nc).astype(np.float32) * 0.2 + 0.14)
    keep = r.random(nc) < 0.35
    rad = size[cid]
    peb = (f1 < rad) & keep[cid]
    t = np.clip(f1 / rad, 0, 1)
    shade = 0.58 + (r.random(nc).astype(np.float32) - 0.5)[cid] * 0.14 - 0.08 * t * t - dy * 0.10
    out = np.where(peb, shade, out)
    # a contact shadow ring just outside each pebble
    ring = keep[cid] & (f1 >= rad) & (f1 < rad + 0.08)
    out = np.where(ring, out - 0.06, out)
    canvas = np.zeros((n, n), np.float32)
    strokes(r, canvas, 600, 3, 10, 1.4, 0.0, 3.1, -0.06, 0.05)
    out += canvas
    out += (fbm(r, 128, 1) - 0.5) * 0.05
    return out


def meadow():
    r = gen('meadow')
    n = TILE
    out = 0.5 + (fbm(r, 3, 4) - 0.5) * 0.14
    canvas = np.zeros((n, n), np.float32)
    # short blade strokes in every direction, light ones more than dark (sun on the tips)
    strokes(r, canvas, 5200, 5, 13, 4.5, 0.0, 3.14159, -0.09, 0.11, soft=0.7)
    # clover clumps: small round leaves in clusters
    for _ in range(90):
        cx, cy = r.random() * n, r.random() * n
        for _ in range(int(r.integers(4, 10))):
            a, d = r.random() * 6.283, r.random() * 9
            dab(canvas, cx + math.cos(a) * d, cy + math.sin(a) * d, 4.5, 4.5, 0.0, 0.07, 0.5)
    out += canvas
    out += (fbm(r, 48, 2) - 0.5) * 0.05
    return out


def roof_tile():
    r = gen('roof_tile')
    n = TILE
    out = np.zeros((n, n), np.float32)
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    x = x + (fbm(r, 4, 2) - 0.5) * 8.0
    rows = 8                                   # tile courses per tile repeat (along v)
    rh = n / rows
    cols = 6
    cw = n / cols
    for k in range(rows):
        shift = (cw / 2.0) * (k % 2) + r.random() * 6
        y0 = k * rh
        local_y = (y - y0)
        in_row = (local_y >= 0) & (local_y < rh)
        lx = ((x - shift) % cw) / cw              # 0..1 across one tile
        ly = local_y / rh                         # 0 top of course .. 1 lower lip
        # barrel: lighter in the middle of the tile, darker at its sides; lighter toward the lower lip; dark overlap line at the top
        barrel = 0.08 * np.cos((lx - 0.5) * math.pi * 1.4)
        lip = 0.10 * ly
        overlap = -0.20 * np.clip(1 - ly / 0.12, 0, 1)
        joint = -0.12 * np.clip(1 - np.minimum(lx, 1 - lx) / 0.05, 0, 1)
        tcol = np.floor((x - shift) / cw).astype(int) % cols
        var = (gen('roof_tile.%d' % k).random(cols).astype(np.float32) - 0.5) * 0.12
        val = 0.46 + barrel + lip + overlap + joint + var[tcol]
        out = np.where(in_row, val, out)
    canvas = np.zeros((n, n), np.float32)
    strokes(r, canvas, 500, 5, 16, 2.0, 0.0, 0.4, -0.04, 0.04)
    out += canvas
    out += (fbm(r, 32, 2) - 0.5) * 0.05
    return out


LAYERS = {9: cliff_rock, 10: stone_wall, 11: plaster, 12: dirt, 13: meadow, 14: roof_tile}


def balance(t):
    # keep the tile's mean at 0.5 (the shader's x2 gain: a mean of 0.5 keeps the palette colour) and clip to [0.08, 0.92]
    t = t - float(t.mean()) + 0.5
    return np.clip(t, 0.08, 0.92)


def load_sheet():
    import bpy
    im = bpy.data.images.load(PATH, check_existing=False)
    im.colorspace_settings.name = 'Non-Color'
    w, h = im.size
    px = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(px)
    bpy.data.images.remove(im)
    px = px.reshape(h, w, 4)[::-1, :, :3]          # top row first
    return np.round(px * 255.0).astype(np.uint8), w, h


def write_png(path, rgb):
    import struct
    h, w, _ = rgb.shape
    raw = b''.join(b'\x00' + rgb[j].tobytes() for j in range(h))
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF)
    body = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    with open(path, 'wb') as fh:
        fh.write(body)


def main():
    sheet, w, h = load_sheet()
    assert w == COLS * TILE and h == ROWS * TILE, (w, h)
    for layer, fn in sorted(LAYERS.items()):
        t = balance(fn())
        v = np.round(t * 255.0).astype(np.uint8)
        r, c = divmod(layer, COLS)
        sheet[r * TILE:(r + 1) * TILE, c * TILE:(c + 1) * TILE, :] = v[..., None]
        print('ENVTEX layer', layer, fn.__name__, 'mean', round(float(t.mean()), 3), 'sd', round(float(t.std()), 3))
    write_png(PATH, sheet)
    print('ENVTEX wrote', PATH)


if __name__ == '__main__':
    main()
