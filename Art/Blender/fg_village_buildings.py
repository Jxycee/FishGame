# fg_village_buildings.py (2026-10-17, branch harbour-village-buildings): the Georama-style placeable village buildings, the aquarium tank set,
# the village lots and the lot marker for the Harbour (design 2026-10-17-open-world-pivot, 3.2 and 5.3), modelled procedurally in Blender.
#
# Run it in an EMPTY Blender file:   FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_village_buildings.py
# It imports Art/Blender/fg_harbour.py as a module (the import has no side effects: main() is behind __name__ == '__main__') and reuses its
# primitives (rbox, prism, lathe, tube, blob, text_geo, window2, hull ...), its palette slots S / S2 and texture layers L, the G2 group class and
# the AO bake / FBX export helpers, so the encoding is the harbour's exactly:
#   authored in UNITY axes (x east, y up, z out to sea), mapped once with to_blender; UV0 "UVMap" in metres, UV1 "Layer" = (palette slot,
#   texture layer), vertex colour "Col" = 0.5 x baked AO, smooth / analytic normals, no materials exported, deterministic (every random draw is
#   random.Random(SEED ^ crc32(stream))).
#
# Every placeable building is a LOCAL model: it sits at the origin, yaw 0, FRONT (door side) = +z, ground at y = 0 (the origin is the centre of its
# footprint at ground level). Its ground pad is part of the mesh (a worn-earth apron, top at y +0.03, buried 0.45 m so it hides a slope of the
# lot). The Unity builder places it at a lot's centre with the lot's yaw (or yaw + 180, the lots allow a flip).
import bpy, bmesh, math, json, os, sys, zlib, random
from mathutils import Vector, Matrix


def _repo_root():
    if os.environ.get('FG_ROOT'):
        return os.environ['FG_ROOT']
    if '__file__' in globals():
        return os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    d = os.getcwd()
    while True:
        if os.path.exists(os.path.join(d, 'Art', 'Blender', 'fg_harbour.py')):
            return d
        if os.path.dirname(d) == d:
            return r'C:\Dev\FishGame'
        d = os.path.dirname(d)


ROOT = _repo_root()
sys.path.insert(0, os.path.join(ROOT, 'Art', 'Blender'))
import fg_harbour as H           # noqa: E402  (no side effects on import)
from fg_harbour import S, S2, L  # noqa: E402

EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'VillageBuildings')
PREVIEW_DIR = os.path.join(ROOT, 'Art', 'Previews', 'VillageBuildings')
MANIFEST_PATH = os.path.join(EXPORT_DIR, 'villagebuildings_manifest.json')
SCRIPT_REL = 'Art/Blender/fg_village_buildings.py'
SEED = H.SEED


def vrng(stream):
    return random.Random(SEED ^ zlib.crc32(('village.' + stream).encode('utf-8')))


# ---------------------------------------------------------------------------------------------------------------------------------------
# Frames and part helpers (everything is authored in a building frame: x right, y up, z = front/door side)
# ---------------------------------------------------------------------------------------------------------------------------------------
class Fr:
    def __init__(self, o=(0.0, 0.0, 0.0), yaw=0.0, par=None):
        if par is None:
            self.R, self.o, self.yaw = H.ry(yaw), Vector(o), yaw
        else:
            self.R, self.o, self.yaw = par.R @ H.ry(yaw), par.P(*o), par.yaw + yaw

    def P(self, x, y=0.0, z=0.0):
        return self.o + self.R @ Vector((x, y, z))


def cube(size):
    # flat-shaded 12-triangle box (hidden or tiny parts: boards, pickets, chair legs)
    V, N, F, UV = [], [], [], []
    for axis in range(3):
        for sgn in (1, -1):
            n = [0.0, 0.0, 0.0]
            n[axis] = float(sgn)
            a1, a2 = [i for i in range(3) if i != axis]
            pts = []
            for s1, s2 in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = [0.0, 0.0, 0.0]
                p[axis] = sgn * size[axis] / 2
                p[a1] = s1 * size[a1] / 2
                p[a2] = s2 * size[a2] / 2
                pts.append(Vector(p))
            b = len(V)
            V += pts
            N += [Vector(n)] * 4
            F.append([b, b + 1, b + 2, b + 3])
            UV.append([H.box_uv(p, n, size) for p in pts])
    return V, N, F, UV


def put(g, f, geo, c, slot, layer, ao=0.5, rot=None, tint=1.0, fsl=None):
    R = f.R if rot is None else f.R @ rot
    g.add(H.xform(geo, f.P(*c), R), slot, layer, ao, fsl=fsl, tint=tint)


def bx(g, f, c, size, slot, layer, ao=0.5, rot=None, r=0.03, segs=1, tint=1.0):
    put(g, f, H.rbox(size, r, segs), c, slot, layer, ao, rot, tint)


def cb(g, f, c, size, slot, layer, ao=0.5, rot=None, tint=1.0):
    put(g, f, cube(size), c, slot, layer, ao, rot, tint)


def lt(g, f, c, prof, slot, layer, ao=0.5, sides=8, rot=None, cap0=False, cap1=False, ring_slots=None, tint=1.0):
    geo, fs = H.lathe(prof, sides, cap0, cap1, ring_slots=ring_slots)
    fsl = [(x if x is not None else slot, layer) for x in fs] if ring_slots else None
    put(g, f, geo, c, slot, layer, ao, rot, tint=tint, fsl=fsl)


def bl(g, f, c, rad, r, slot, layer=None, scale=(1.0, 0.8, 1.0), sub=1, rough=0.2, ao=0.6, yaw=0.0, flat=-0.55):
    put(g, f, H.blob(rad, r, scale, subdiv=sub, rough=rough, flat=flat, yaw=yaw), c, slot, L['Foliage'] if layer is None else layer, ao)


def tb(g, f, pts, rad, slot, layer, ao=0.55, sides=4, caps=False, closed=False):
    g.add(H.tube([f.P(*p) for p in pts], rad, sides, caps=caps, closed=closed), slot, layer, ao)


def label(g, f, text, size, c, slot, depth=0.008, rot=None):
    R = f.R if rot is None else f.R @ rot
    H.sign_text(g, text, size, f.P(*c), R, slot, depth)


def sag_pts(a, b, sag, n=10):
    a, b = Vector(a), Vector(b)
    return [a.lerp(b, i / n) - Vector((0, sag * 4 * (i / n) * (1 - i / n), 0)) for i in range(n + 1)]


def tuft(g, f, x, z, r, n=4, h=0.4, slot=None, y=0.0):
    for k in range(n):
        a = r.uniform(0, 2 * math.pi)
        base = f.P(x + r.uniform(-0.08, 0.08), y, z + r.uniform(-0.08, 0.08))
        d = f.R @ Vector((math.cos(a), 0.0, math.sin(a)))
        g.add(H.blade_geo(base, d, h * r.uniform(0.6, 1.1), 0.05, r.uniform(0.25, 0.6), segs=2), S['Reed'] if slot is None else slot, L['Grass'], 0.6)


def make_group(name, note, rays=14, ao_dist=0.9, material=None, lmap=None):
    g = H.G2(name, 'core', local=True, rays=rays, ao_dist=ao_dist, note=note, material=material)
    g.lmap = lmap if lmap is not None else {5: 11}        # plaster on the sand layer, as every harbour building
    return g


# ---------------------------------------------------------------------------------------------------------------------------------------
# Shared building parts
# ---------------------------------------------------------------------------------------------------------------------------------------
Y0 = 0.25                       # wall base height (plinth top 0.31)
PAD_TOP = 0.03


def pad(g, f, w, d, slot=None, layer=None):
    # the ground pad: worn earth, top at +0.03, buried to -0.45 (hides up to ~0.4 m of slope under the building), rounded edge
    g.extra['pad'] = dict(width_x=w, depth_z=d, top_y=PAD_TOP, bottom_y=round(PAD_TOP - 0.48, 2), centre_xz=[round(f.o.x, 2), round(f.o.z, 2)],
                          note='worn-earth apron owned by the model; the Unity builder puts the building origin on the lot centre at ground height')
    bx(g, f, (0, PAD_TOP - 0.24, 0), (w, 0.48, d), S['Dock'] if slot is None else slot, L['Dirt'] if layer is None else layer, 0.6, r=0.1, segs=1)


def plinth(g, f, w, d, slot=None):
    bx(g, f, (0, 0.1, 0), (w + 0.24, 0.42, d + 0.24), S['Rock'] if slot is None else slot, L['StoneWall'], 0.55, r=0.05)


def shell(g, f, W, D, eave, ridge, wall, wlay, roof, rlay, trim, front_gable=False, over=0.35, y0=Y0, posts=True):
    # walls (a house-shaped prism), corner posts, a two-slab roof and a ridge roll. Ridge along x (side gable) or along z (front_gable).
    if not front_gable:
        poly = [(-D / 2, 0.0), (D / 2, 0.0), (D / 2, eave), (0.0, ridge), (-D / 2, eave)]
        put(g, f, H.prism(poly, W, 0.03, segs=1), (0, y0, 0), wall, wlay, H.AO_WOOD)
    else:
        poly = [(-W / 2, 0.0), (W / 2, 0.0), (W / 2, eave), (0.0, ridge), (-W / 2, eave)]
        put(g, f, H.prism(poly, D, 0.03, segs=1), (0, y0, 0), wall, wlay, H.AO_WOOD, rot=H.ry(90))
    if posts:
        for sx in (-1, 1):
            for sz in (-1, 1):
                cb(g, f, (sx * W / 2, y0 + eave / 2, sz * D / 2), (0.15, eave, 0.15), trim, L['Wood'], 0.45)
    if not front_gable:
        run = D / 2 + over
        ang = math.degrees(math.atan2(ridge - eave, D / 2))
        slab = run / math.cos(math.radians(ang)) + 0.05
        for sz in (-1, 1):
            mid = (0, y0 + ridge - (ridge - eave) * (run / 2) / (D / 2) + 0.08, sz * run / 2)
            bx(g, f, mid, (W + 0.55, 0.13, slab), roof, rlay, 0.5, rot=H.rx(ang * sz), r=0.05)
        bx(g, f, (0, y0 + ridge + 0.12, 0), (W + 0.6, 0.13, 0.26), roof, L['Wood'], 0.5, r=0.05)
    else:
        run = W / 2 + over
        ang = math.degrees(math.atan2(ridge - eave, W / 2))
        slab = run / math.cos(math.radians(ang)) + 0.05
        for sx in (-1, 1):
            mid = (sx * run / 2, y0 + ridge - (ridge - eave) * (run / 2) / (W / 2) + 0.08, 0)
            bx(g, f, mid, (slab, 0.13, D + 0.55), roof, rlay, 0.5, rot=H.rz(-ang * sx), r=0.05)
        bx(g, f, (0, y0 + ridge + 0.12, 0), (0.26, 0.13, D + 0.6), roof, L['Wood'], 0.5, r=0.05)
    return ang


def roof_y(y0, eave, ridge, D, zz):
    # height of the roof's top face at depth zz from the ridge (side gable)
    return y0 + ridge - (ridge - eave) * (abs(zz) / (D / 2)) + 0.13


def door(g, f, x, z, w=0.95, h=1.95, slot=None, trim=None, y=0.31, step=True, knob=None, rd=False):
    slot = S['Dock'] if slot is None else slot
    bx(g, f, (x, y + h / 2, z + 0.04), (w, h, 0.08), slot, L['Wood'], 0.5, r=0.03)
    for by in (0.4, 1.4):
        cb(g, f, (x, y + by, z + 0.09), (w - 0.06, 0.1, 0.04), S['WoodDark'], L['Wood'], 0.45)
    bx(g, f, (x, y + h + 0.07, z + 0.07), (w + 0.3, 0.14, 0.16), S['WoodDark'] if trim is None else trim, L['Wood'], 0.5, r=0.03)
    bx(g, f, (x + w / 2 - 0.15, y + 0.95, z + 0.1), (0.06, 0.06, 0.06), S['Accent'] if knob is None else knob, L['none'], 0.5, r=0.025)
    if step:
        bx(g, f, (x, 0.12, z + 0.35), (w + 0.5, 0.2, 0.62), S['Rock'], L['StoneWall'], 0.55, r=0.05)


def wsub(f, side):
    # a sub-frame whose local +z is the outward normal of a side wall: side 'back' (-z), 'E' (+x), 'W' (-x)
    return Fr(yaw={'back': 180.0, 'E': 90.0, 'W': -90.0}[side], par=f)


def win(g, f, x, y, z, w, h, frame, shutters=None):
    H.window2(g, f.P, f.R, x, y, z, w, h, frame, shutters)


def board_up(g, f, x, y, z, w, h, r, long=0.25):
    # planks nailed across an opening on a wall facing +z in frame f (centre x, y; plane z)
    n = 3 if h > 1.3 else 2
    for k in range(n):
        yy = y - h / 2 + h * (k + 0.5) / n + r.uniform(-0.05, 0.05)
        ln = w + long + r.uniform(-0.1, 0.25)
        cb(g, f, (x + r.uniform(-0.05, 0.05), yy, z + 0.07), (ln, 0.17, 0.045), S['WoodDark'] if k % 2 else S['Wood'], L['Wood'], 0.5,
           rot=H.rz(r.uniform(-7, 7)))
    cb(g, f, (x + r.uniform(-0.1, 0.1), y, z + 0.1), (0.13, h + 0.15, 0.04), S['Wood'], L['Wood'], 0.5, rot=H.rz(r.uniform(-14, 14)))


def flowerbox(g, f, x, y, z, w, r, slot_box=None, cols=None):
    bx(g, f, (x, y, z), (w, 0.18, 0.22), S['Wood'] if slot_box is None else slot_box, L['Wood'], 0.55, r=0.03)
    cols = cols or (S['Accent'], S['Roof'], S2['Snow'], S2['Blossom'])
    n = max(3, int(w / 0.17))
    for k in range(n):
        bl(g, f, (x - w / 2 + 0.12 + k * (w - 0.24) / (n - 1), y + 0.14, z + 0.01), 0.09, r, cols[(k + int(x * 10)) % len(cols)] if k % 2 == 0 else S['Foliage'],
           L['Foliage'], (1.0, 0.8, 1.0), 1, 0.25, 0.65)


def chimney(g, f, x, y_base, y_top, z=-0.5, w=0.55):
    bx(g, f, (x, (y_base + y_top) / 2, z), (w, y_top - y_base, w), S['Rock'], L['Rock'], 0.55, r=0.05)
    bx(g, f, (x, y_top + 0.06, z), (w + 0.15, 0.12, w + 0.15), S['Rock'], L['Rock'], 0.55, r=0.04)
    return f.P(x, y_top + 0.2, z)


def tarp(g, f, c, size, r, rot):
    # a patched orange tarp, tacky on purpose, with four rope ties
    bx(g, f, c, size, S['Accent'], L['Cloth'], 0.6, rot=rot, r=0.02)


def dim_patch(g, f, c, size, rot):
    # a "missing tiles" patch: dark rough boards showing through the roof
    cb(g, f, c, size, S['WoodDark'], L['Wood'], 0.4, rot=rot, tint=0.55)


def lean_to(g, f, x0, x1, z0, z1, h_hi, h_lo, slot_post, slot_roof, wall_slot=None, closed_back=True, y0=Y0):
    # a lean-to against a gable end at x0 (the house wall): posts, a sloped slab roof falling away to x1; optional boarded back/front
    w = abs(x1 - x0)
    xs = 1 if x1 > x0 else -1
    zc, d = (z0 + z1) / 2, abs(z1 - z0)
    slope = math.degrees(math.atan2(h_hi - h_lo, w))
    for zz in (z0, z1):
        cb(g, f, (x1, y0 + h_lo / 2, zz), (0.12, h_lo, 0.12), slot_post, L['Wood'], 0.45)
    bx(g, f, ((x0 + x1) / 2, y0 + (h_hi + h_lo) / 2 + 0.06, zc), (w / math.cos(math.radians(slope)) + 0.4, 0.1, d + 0.3), slot_roof, L['RoofTile'], 0.5,
       rot=H.rz(slope * xs), r=0.04)
    if wall_slot is not None:
        cb(g, f, ((x0 + x1) / 2, y0 + h_lo * 0.5, z0), (w, h_lo, 0.06), wall_slot, L['Wood'], 0.5)
        cb(g, f, ((x0 + x1) / 2, y0 + h_lo * 0.5, z1), (w, h_lo, 0.06), wall_slot, L['Wood'], 0.5)
    return slope


def logs(g, f, x, z, rows, cols, r, length=0.9, rad=0.09, y=0.12, axis_x=False):
    # a log stack of horizontal cylinders (6-sided)
    for j in range(rows):
        for i in range(cols - (j % 2)):
            ox = (i + 0.5 * (j % 2)) * rad * 2.1 - cols * rad
            geo, _ = H.lathe([(-length / 2, 0.0), (-length / 2 + 0.01, rad), (length / 2 - 0.01, rad), (length / 2, 0.0)], 6, cap0=False, cap1=False)
            rot = H.rz(90) if axis_x else H.rx(90)
            c = (x + (0 if axis_x else ox), y + rad + j * rad * 1.85, z + (ox if axis_x else 0))
            put(g, f, geo, c, S['Wood'] if (i + j) % 3 else S['Dock'], L['Bark'], 0.5, rot=rot @ H.ry(r.uniform(0, 60)))


def smoke(g, p, name):
    g.extra.setdefault('smoke_points', []).append(dict(name=name, position=[round(p.x, 2), round(p.y, 2), round(p.z, 2)]))


def body_collider(g, name='Body'):
    return g.solid(name)


def weeds(g, f, r, spots, n=4, h=0.5):
    for x, z in spots:
        tuft(g, f, x, z, r, n, h * r.uniform(0.7, 1.2), S['Reed'] if r.random() < 0.5 else S['Grass'], y=PAD_TOP - 0.03)


# ---------------------------------------------------------------------------------------------------------------------------------------
# Cottage variants. Each: (group, info). `run` = the run-down "come back" state: boarded openings, no flowers, weeds, a tarp and a dropped shutter.
# ---------------------------------------------------------------------------------------------------------------------------------------
def cottage_A(run=False):
    # "Fisher's Row": a side-gabled plastered cottage with a porch hood, a dormer and a log lean-to on the east gable.
    g = make_group('Village_Cottage_A' + ('_Rundown' if run else ''), 'Cottage A (sand plaster, red roof, green trim, woodshed lean-to).')
    r = vrng('cottageA' + ('r' if run else ''))
    f = Fr((-0.65, 0, 0))
    W, D, eave, ridge = 5.2, 4.2, 2.45, 3.95
    trim = S['Foliage'] if not run else S['WoodDark']
    pad(g, Fr(), 7.6, 5.8)
    g.mark()
    plinth(g, f, W, D)
    shell(g, f, W, D, eave, ridge, S['Sand'], L['Sand'], S['Roof'], L['RoofTile'], trim)
    ck = chimney(g, f, W / 2 - 0.7, Y0 + ridge - 0.9, Y0 + ridge + 0.85)
    # lean-to woodshed on the east gable
    lean_to(g, f, W / 2, W / 2 + 1.3, -1.45, 1.45, 1.9, 1.35, S['WoodDark'], S['Dock'], wall_slot=S['Wood'] if not run else None)
    bx(g, f, (W / 2 + 0.65, 0.1, 0), (1.3, 0.2, 3.0), S['Dock'], L['Wood'], 0.5, r=0.03)
    if not run:
        logs(g, f, W / 2 + 0.65, 0.0, 3, 5, r, length=2.7, axis_x=False)
    fz = D / 2
    dxd = -1.5
    door(g, f, dxd, fz, slot=S['Roof'] if not run else S['Wood'], trim=trim)
    for wx in (0.35, 1.75):
        win(g, f, wx, Y0 + 1.5, fz, 0.72, 0.8, trim, None if run else (trim if wx > 1.0 else None))
    for side in ('back',):
        sb = wsub(f, side)
        win(g, sb, 0.9, Y0 + 1.5, D / 2, 0.7, 0.7, trim)
        door(g, sb, -1.0, D / 2, slot=S['WoodDark'], step=False)
    wg = wsub(f, 'W')
    win(g, wg, 0.0, Y0 + eave + 0.45, W / 2, 0.5, 0.5, trim)
    # a gabled dormer on the front slope
    zr = D / 4 + 0.1
    H.gable_house(g, tuple(f.P(0.9, roof_y(Y0, eave, ridge, D, zr) - 0.62, zr)), 1.25, 1.4, 1.05, 1.6, S['Sand'], S['Roof'], trim, roof_kind='slab', overhang=0.14,
                  yaw=f.yaw + 90.0, wall_layer=L['Plaster'])
    win(g, f, 0.9, roof_y(Y0, eave, ridge, D, zr) - 0.0, zr + 0.71, 0.5, 0.5, trim)
    if not run:
        hood = H.rx(-18.0)
        bx(g, f, (dxd, Y0 + 2.3, fz + 0.36), (1.5, 0.08, 0.7), S['Roof'], L['RoofTile'], 0.5, rot=hood, r=0.03)
        for s_ in (-1, 1):
            cb(g, f, (dxd + s_ * 0.66, Y0 + 2.1, fz + 0.3), (0.06, 0.06, 0.62), trim, L['Wood'], 0.5, rot=H.rx(35.0))
        for wx in (0.35, 1.75):
            flowerbox(g, f, wx, Y0 + 0.85, fz + 0.16, 0.86, r)
        # lobster pots stacked at the corner, a rain barrel under the downpipe, a name plaque
        for k in range(2):
            H.lobster_pot(g, 'x', f.P(-W / 2 - 0.0 - 0.5 + 0.15, 0.04 + 0.36 * k, fz - 0.2), f.yaw + r.uniform(-20, 20), 0.0)
            g.items.pop()
        lt(g, f, (-W / 2 + 0.3, 0.0, fz + 0.4), [(0.0, 0.23), (0.02, 0.25), (0.35, 0.28), (0.7, 0.25), (0.72, 0.23)], S['WoodDark'], L['Wood'], 0.45, 10, cap1=True)
        label(g, f, 'MIND THE GULL', 0.085, (dxd, Y0 + 2.27, fz + 0.13), S['WoodDark'])
    else:
        board_up(g, f, dxd, Y0 + 1.15, fz, 0.95, 1.95, r, 0.2)
        for wx in (0.35, 1.75):
            board_up(g, f, wx, Y0 + 1.5, fz, 0.72, 0.8, r)
        board_up(g, f, 0.9, roof_y(Y0, eave, ridge, D, zr), zr + 0.71, 0.5, 0.5, r, 0.1)
        tarp(g, f, (-1.2, roof_y(Y0, eave, ridge, D, 1.0) + 0.04, 1.1), (1.9, 0.03, 1.5), r, H.rx(math.degrees(math.atan2(ridge - eave, D / 2))))
        dim_patch(g, f, (1.4, roof_y(Y0, eave, ridge, D, -0.9) + 0.02, -0.9), (0.9, 0.03, 0.8), H.rx(-math.degrees(math.atan2(ridge - eave, D / 2))))
        cb(g, f, (-W / 2 - 0.1, 0.5, fz + 0.05), (0.04, 1.0, 0.3), S['Foliage'], L['Wood'], 0.5, rot=H.rz(8) @ H.ry(30))      # dropped shutter
        weeds(g, f, r, [(-2.5, 2.4), (-1.0, 2.6), (1.0, 2.5), (2.9, 2.4), (-3.3, 0.5), (3.4, -2.1)], 5, 0.6)
    body_collider(g)
    if not run:
        smoke(g, ck, 'Chimney_CottageA')
    return g, dict(role='cottage', lot_class='S', door_offset_x=round(dxd - 0.65, 2), variant='A', rundown=run)


def cottage_B(run=False):
    # "Boat-loft Cottage": a front-gabled weatherboard house with a hoist beam, a porch with two posts and a dinghy leaning on the west wall.
    g = make_group('Village_Cottage_B' + ('_Rundown' if run else ''), 'Cottage B (weatherboard, slate roof, front gable with loft hoist).')
    r = vrng('cottageB' + ('r' if run else ''))
    f = Fr((0, 0, -0.65))
    W, D, eave, ridge = 4.4, 3.8, 2.45, 4.4
    trim = S['Sand'] if not run else S['WoodDark']
    pad(g, Fr(), 6.8, 6.0)
    g.mark()
    plinth(g, f, W, D)
    ang = shell(g, f, W, D, eave, ridge, S['Wood'], L['Wood'], S['Rock'], L['RoofTile'], trim, front_gable=True)
    # weatherboard laps
    for k in range(5):
        cb(g, f, (0, Y0 + 0.5 + 0.42 * k, D / 2 + 0.015), (W + 0.02, 0.04, 0.03), S['WoodDark'], L['Wood'], 0.5)
    ck = chimney(g, f, 1.3, Y0 + 1.6, Y0 + ridge + 0.5, z=-1.2)
    fz = D / 2
    # porch: floor, two posts, shed roof
    pz = fz + 0.6
    bx(g, f, (0, 0.14, pz), (3.0, 0.2, 1.2), S['Dock'], L['Wood'], 0.5, r=0.03)
    for sx in (-1.3, 1.3):
        lt(g, f, (sx, 0.24, pz + 0.45), [(0.0, 0.07), (2.0, 0.065), (2.05, 0.0)], trim, L['Wood'], 0.45, 8, cap1=True)
    bx(g, f, (0, 2.38, pz + 0.05), (3.2, 0.1, 1.6), S['Rock'], L['RoofTile'], 0.5, rot=H.rx(9), r=0.04)
    door(g, f, 0.0, fz, w=1.0, slot=S['Foliage'] if not run else S['Wood'], trim=trim, step=False, y=0.31)
    win(g, f, -1.4, Y0 + 1.5, fz, 0.7, 0.8, trim, trim if not run else None)
    win(g, f, 1.4, Y0 + 1.5, fz, 0.7, 0.8, trim, trim if not run else None)
    # the loft: a hatch door under the apex, a hoist beam and block
    bx(g, f, (0, Y0 + eave + 0.75, fz + 0.04), (0.95, 1.05, 0.07), S['Dock'], L['Wood'], 0.5, r=0.03)
    cb(g, f, (0, Y0 + eave + 0.75, fz + 0.09), (0.9, 0.06, 0.03), S['WoodDark'], L['Wood'], 0.45, rot=H.rz(38))
    cb(g, f, (0, Y0 + eave + 0.75, fz + 0.09), (0.9, 0.06, 0.03), S['WoodDark'], L['Wood'], 0.45, rot=H.rz(-38))
    bx(g, f, (0, Y0 + ridge + 0.05, fz + 0.45), (0.1, 0.12, 1.1), S['WoodDark'], L['Wood'], 0.5, r=0.03)
    cb(g, f, (0, Y0 + ridge - 0.3, fz + 0.1), (0.08, 0.6, 0.08), S['WoodDark'], L['Wood'], 0.5)
    lt(g, f, (0, Y0 + ridge - 0.25, fz + 0.95), [(0.0, 0.0), (0.1, 0.07), (0.25, 0.07), (0.35, 0.0)], S['Rock'], L['none'], 0.5, 8, cap0=True, cap1=True, rot=H.rx(90))
    tb(g, f, [(0, Y0 + ridge - 0.25, fz + 0.95), (0, Y0 + eave * 0.55, fz + 1.0)], 0.012, S['Sand'], L['Cloth'], 0.55, 3)
    bl(g, f, (0, Y0 + eave * 0.55 - 0.15, fz + 1.0), 0.17, r, S['Accent'], L['none'], (1.0, 1.3, 1.0), 1, 0.12)     # a hanging buoy as the hoist weight
    # side walls and back
    for side in ('E', 'W'):
        sw = wsub(f, side)
        win(g, sw, 0.8, Y0 + 1.5, W / 2, 0.7, 0.8, trim, None)
    sb = wsub(f, 'back')
    win(g, sb, -0.9, Y0 + 1.5, D / 2, 0.7, 0.7, trim)
    door(g, sb, 0.9, D / 2, slot=S['WoodDark'], step=False)
    if not run:
        for wx in (-1.4, 1.4):
            flowerbox(g, f, wx, Y0 + 0.85, fz + 0.16, 0.86, r)
        # the dinghy leaning on the west wall: an upturned half-hull, an oar and a coil
        dn = H.hull(1.9, 0.85, 0.33)
        H.add_part(g, dn, f.P(-W / 2 - 0.55, 0.62, -0.3), f.R @ H.ry(90) @ H.rz(78), 0.5)
        lt(g, f, (-W / 2 - 0.2, 0.0, 1.0), [(0.0, 0.025), (1.7, 0.025), (1.72, 0.0)], S['Wood'], L['Wood'], 0.5, 6, rot=H.rz(8))
        for sx_ in (-0.4, 0.4):
            tb(g, f, [(sx_, 2.3, pz + 0.78), (sx_, 2.06, pz + 0.78)], 0.01, S['WoodDark'], L['none'], 0.5, 3)
        bx(g, f, (0.0, 1.95, pz + 0.78), (1.1, 0.24, 0.04), S['Sand'], L['Wood'], 0.5, r=0.015)
        label(g, f, 'FISH ALREADY.', 0.08, (0.0, 1.95, pz + 0.81), S['WoodDark'], depth=0.006)
    else:
        board_up(g, f, 0.0, Y0 + 1.15, fz, 1.0, 1.95, r, 0.2)
        for wx in (-1.4, 1.4):
            board_up(g, f, wx, Y0 + 1.5, fz, 0.7, 0.8, r)
        board_up(g, f, 0, Y0 + eave + 0.75, fz, 0.95, 1.05, r, 0.1)
        # sagging porch: one post missing, the roof dropped on one side
        tarp(g, f, (1.2, Y0 + 3.0, 0.8), (1.8, 0.03, 1.5), r, H.rz(-math.degrees(math.atan2(ridge - eave, W / 2))) @ H.rx(0))
        weeds(g, f, r, [(-2.4, 2.0), (-1.9, 2.3), (1.9, 2.3), (2.5, 1.9), (-3.0, -0.5)], 5, 0.6)
        cb(g, f, (-1.3, 0.4, pz + 0.45), (0.14, 0.8, 0.14), S['WoodDark'], L['Wood'], 0.45, rot=H.rx(80))      # the fallen porch post
    body_collider(g)
    if not run:
        smoke(g, ck, 'Chimney_CottageB')
    return g, dict(role='cottage', lot_class='S', door_offset_x=0.0, variant='B', rundown=run)


def cottage_C(run=False):
    # "Stone Cottage": thick stone walls, turf-green roof with a tufted ridge, a porthole window, a gable chimney and a bench.
    g = make_group('Village_Cottage_C' + ('_Rundown' if run else ''), 'Cottage C (rubble stone, mossy roof, porthole).', lmap={})
    r = vrng('cottageC' + ('r' if run else ''))
    f = Fr((0.0, 0, 0))
    W, D, eave, ridge = 5.4, 4.0, 2.3, 3.7
    trim = S['WoodDark']
    pad(g, Fr(), 7.4, 5.6)
    g.mark()
    plinth(g, f, W, D, S['Rock'])
    shell(g, f, W, D, eave, ridge, S['Rock'], L['StoneWall'], S['Foliage'], L['RoofTile'], trim)
    # mossy ridge tufts
    if not run:
        for k in range(7):
            bl(g, f, (-W / 2 + 0.5 + k * (W - 1.0) / 6, Y0 + ridge + 0.26, r.uniform(-0.1, 0.1)), 0.19, r, S['FoliageDark'] if k % 2 else S['Grass'], L['Foliage'],
               (1.2, 0.8, 1.1), 1, 0.3, 0.6)
    # the gable chimney: a stout stone stack on the west end wall, outside the wall plane
    cx = -W / 2 - 0.2
    bx(g, f, (cx, Y0 + (ridge + 0.55) / 2, 0.2), (0.6, ridge + 0.55, 1.1), S['Rock'], L['StoneWall'], 0.55, r=0.05)
    bx(g, f, (cx, Y0 + ridge + 0.75, 0.2), (0.75, 0.16, 1.25), S['Rock'], L['StoneWall'], 0.55, r=0.05)
    bx(g, f, (cx + 0.1, 0.55, 0.2), (0.45, 0.08, 1.4), S['Rock'], L['StoneWall'], 0.55, r=0.03)          # the footing step
    ck = f.P(cx, Y0 + ridge + 0.95, 0.2)
    fz = D / 2
    door(g, f, 1.5, fz, w=0.95, slot=S['Accent'] if not run else S['Wood'], trim=trim)
    win(g, f, -0.2, Y0 + 1.45, fz, 0.8, 0.85, trim, None)
    # porthole in the front gable-less wall: a round window with a ring
    for sgn, ccol in ((1, S['Rock']),):
        lt(g, f, (-1.8, Y0 + 1.5, fz + 0.03), [(0.0, 0.33), (0.07, 0.35), (0.1, 0.0)], trim, L['Wood'], 0.5, 12, cap0=True, rot=H.rx(90))
        lt(g, f, (-1.8, Y0 + 1.5, fz + 0.13), [(0.0, 0.0), (0.012, 0.27)], S['Rock'], L['none'], 0.5, 12, cap1=True, rot=H.rx(90), tint=0.5)
    sb = wsub(f, 'back')
    win(g, sb, 0.9, Y0 + 1.4, D / 2, 0.7, 0.7, trim)
    door(g, sb, -1.2, D / 2, slot=S['WoodDark'], step=False)
    we = wsub(f, 'E')
    win(g, we, 0.0, Y0 + eave + 0.35, W / 2, 0.5, 0.5, trim)
    if not run:
        # a stone bench with a driftwood top, a rain barrel and a hanging lantern at the door
        bx(g, f, (-0.1, 0.42, fz + 0.45), (1.6, 0.1, 0.4), S['Wood'], L['Wood'], 0.5, r=0.03)
        for sx in (-0.7, 0.7):
            bx(g, f, (sx - 0.1, 0.2, fz + 0.45), (0.2, 0.4, 0.34), S['Rock'], L['StoneWall'], 0.55, r=0.04)
        lt(g, f, (2.5, 0.0, fz + 0.3), [(0.0, 0.23), (0.02, 0.25), (0.35, 0.28), (0.7, 0.25), (0.72, 0.23)], S['WoodDark'], L['Wood'], 0.45, 10, cap1=True)
        bx(g, f, (2.0, Y0 + 2.25, fz + 0.2), (0.06, 0.28, 0.06), S['WoodDark'], L['Wood'], 0.5, r=0.02)
        lt(g, f, (2.0, Y0 + 1.95, fz + 0.2), [(0.0, 0.05), (0.04, 0.09), (0.22, 0.09), (0.26, 0.0)], S['Accent'], L['none'], 0.7, 6)
        label(g, f, 'KNOCK. THEN LEAVE.', 0.075, (1.5, Y0 + 2.2, fz + 0.13), S2['Snow'])
        # firewood against the west wall, tidy
        logs(g, f, -W / 2 - 0.55, -1.0, 3, 4, r, length=0.9, axis_x=True)
    else:
        board_up(g, f, 1.5, Y0 + 1.15, fz, 0.95, 1.95, r, 0.2)
        board_up(g, f, -0.2, Y0 + 1.45, fz, 0.8, 0.85, r)
        board_up(g, sb, 0.9, Y0 + 1.4, D / 2, 0.7, 0.7, r)
        lt(g, f, (-1.8, Y0 + 1.5, fz + 0.16), [(0.0, 0.0), (0.02, 0.4)], S['Wood'], L['Wood'], 0.5, 8, cap1=True, rot=H.rx(90))
        weeds(g, f, r, [(-2.6, 2.3), (-1.0, 2.5), (0.6, 2.5), (2.7, 2.4), (3.2, 0.2), (-3.5, -1.3)], 5, 0.7)
        # ivy-grey roof: a collapsed corner
        dim_patch(g, f, (-1.6, roof_y(Y0, eave, ridge, D, 0.9) + 0.02, 0.9), (1.2, 0.03, 0.9), H.rx(math.degrees(math.atan2(ridge - eave, D / 2))))
        tarp(g, f, (1.6, roof_y(Y0, eave, ridge, D, -0.8) + 0.04, -0.8), (1.9, 0.03, 1.4), r, H.rx(-math.degrees(math.atan2(ridge - eave, D / 2))))
    body_collider(g)
    if not run:
        smoke(g, ck, 'Chimney_CottageC')
    return g, dict(role='cottage', lot_class='S', door_offset_x=1.5, variant='C', rundown=run)


def cottage_D(run=False):
    # "Narrow Townhouse": two storeys, a first-floor balcony over the door, a bay window, a net-hoist arm at the gable and a flagpole.
    g = make_group('Village_Cottage_D' + ('_Rundown' if run else ''), 'Cottage D (narrow two-storey, pistachio walls, balcony).')
    r = vrng('cottageD' + ('r' if run else ''))
    f = Fr((0.0, 0, -0.35))
    W, D, eave, ridge = 4.0, 4.4, 4.2, 5.5
    trim = S['Sand'] if not run else S['WoodDark']
    pad(g, Fr(), 6.4, 6.0)
    g.mark()
    plinth(g, f, W, D)
    shell(g, f, W, D, eave, ridge, S['Reed'], L['Wood'], S['Dock'], L['RoofTile'], trim)
    cb(g, f, (0, Y0 + 2.3, 0), (W + 0.12, 0.14, D + 0.12), trim, L['Wood'], 0.5)                       # the storey band
    ck = chimney(g, f, W / 2 - 0.65, Y0 + 3.4, Y0 + ridge + 0.85, z=-0.4)
    fz = D / 2
    door(g, f, -0.75, fz, w=0.95, slot=S['Roof'] if not run else S['Wood'], trim=trim)
    # bay window on the ground floor: three angled panes
    bx(g, f, (1.0, Y0 + 0.95, fz + 0.25), (1.2, 1.5, 0.5), S['Reed'], L['Wood'], 0.5, r=0.05)
    win(g, f, 1.0, Y0 + 1.1, fz + 0.5, 0.85, 0.85, trim, None)
    bx(g, f, (1.0, Y0 + 1.82, fz + 0.3), (1.4, 0.07, 0.65), S['Dock'], L['RoofTile'], 0.5, r=0.03)
    # first-floor balcony over the door, with a railing
    bx(g, f, (-0.75, Y0 + 2.4, fz + 0.45), (1.5, 0.1, 0.9), S['Wood'], L['Wood'], 0.5, r=0.03)
    for k in range(7):
        cb(g, f, (-1.4 + 0.11 * k * 1.0 + 0.0, Y0 + 2.85, fz + 0.88), (0.04, 0.8, 0.04), trim, L['Wood'], 0.5)
    cb(g, f, (-0.75, Y0 + 3.25, fz + 0.88), (1.5, 0.07, 0.06), trim, L['Wood'], 0.5)
    for sx in (-1.45, -0.05):
        cb(g, f, (sx, Y0 + 2.9, fz + 0.88), (0.07, 0.9, 0.07), trim, L['Wood'], 0.5)
    # first-floor french window and a second window
    win(g, f, -0.75, Y0 + 3.15, fz, 0.8, 1.2, trim, None if run else trim)
    win(g, f, 1.0, Y0 + 3.3, fz, 0.72, 0.8, trim, None if run else trim)
    # gable attic window and the hoist arm with a pulley block
    for side in ('E', 'W'):
        sw = wsub(f, side)
        win(g, sw, 0.0, Y0 + 1.5, W / 2, 0.7, 0.8, trim)
        win(g, sw, 0.0, Y0 + 3.3, W / 2, 0.7, 0.8, trim)
    sb = wsub(f, 'back')
    win(g, sb, 0.7, Y0 + 1.5, D / 2, 0.7, 0.7, trim)
    door(g, sb, -0.8, D / 2, slot=S['WoodDark'], step=False)
    win(g, sb, 0.0, Y0 + 3.3, D / 2, 0.7, 0.8, trim)
    bx(g, f, (0.0, Y0 + eave + 0.95, fz + 0.3), (0.12, 0.12, 0.8), S['WoodDark'], L['Wood'], 0.5, r=0.03)
    cb(g, f, (0.0, Y0 + eave + 0.55, fz + 0.1), (0.1, 0.9, 0.1), S['WoodDark'], L['Wood'], 0.5, rot=H.rx(-8))
    lt(g, f, (0, Y0 + eave + 0.85, fz + 0.62), [(0.0, 0.0), (0.09, 0.07), (0.2, 0.07), (0.28, 0.0)], S['Rock'], L['none'], 0.5, 8, cap0=True, cap1=True, rot=H.rx(90))
    if not run:
        tb(g, f, [(0, Y0 + eave + 0.8, fz + 0.62), (0, Y0 + 3.7, fz + 0.7)], 0.012, S['Sand'], L['Cloth'], 0.55, 3)
        bl(g, f, (0, Y0 + 3.5, fz + 0.7), 0.15, r, S['Roof'], L['none'], (1.0, 1.3, 1.0), 1, 0.1)
        flowerbox(g, f, 1.0, Y0 + 1.72, fz + 0.62, 1.0, r)
        flowerbox(g, f, 1.0, Y0 + 2.85, fz + 0.14, 0.86, r)
        # the flagpole on the chimney side of the roof, a fish flag
        lt(g, f, (-W / 2 + 0.4, Y0 + eave, -D / 2 + 0.6), [(0.0, 0.04), (2.4, 0.03), (2.45, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
        cl = H.cloth_panel(0.8, 0.45, 4, 2, 0.05, 0.4)
        put(g, f, cl, (-W / 2 + 0.4, Y0 + eave + 2.35, -D / 2 + 0.6), S['Accent'], L['Cloth'], 0.6, rot=H.ry(90))
        label(g, f, 'NOT A PUB', 0.1, (-0.75, Y0 + 2.1, fz + 0.12), S['WoodDark'], depth=0.008)
    else:
        board_up(g, f, -0.75, Y0 + 1.15, fz, 0.95, 1.95, r, 0.2)
        board_up(g, f, 1.0, Y0 + 1.1, fz + 0.5, 0.85, 0.85, r)
        board_up(g, f, -0.75, Y0 + 3.15, fz, 0.8, 1.2, r)
        board_up(g, f, 1.0, Y0 + 3.3, fz, 0.72, 0.8, r)
        tarp(g, f, (0.5, roof_y(Y0, eave, ridge, D, 1.0) + 0.04, 1.0), (1.8, 0.03, 1.5), r, H.rx(math.degrees(math.atan2(ridge - eave, D / 2))))
        weeds(g, f, r, [(-2.2, 2.5), (-0.6, 2.5), (1.6, 2.5), (2.4, 2.3), (-2.6, -1.0)], 5, 0.7)
        cb(g, f, (1.3, Y0 + 2.6, fz + 0.9), (0.04, 0.8, 0.04), trim, L['Wood'], 0.5, rot=H.rz(25))      # a broken railing picket
    body_collider(g)
    if not run:
        smoke(g, ck, 'Chimney_CottageD')
    return g, dict(role='cottage', lot_class='S', door_offset_x=-0.75, variant='D', rundown=run)


# ---------------------------------------------------------------------------------------------------------------------------------------
# Pipeline: objects, AO bake, FBX export, preview scene
# ---------------------------------------------------------------------------------------------------------------------------------------
def to_object(g):
    ob = H.make_object(g)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    H.collection('VillageBuildings').objects.link(ob)
    return ob


def bake(groups, objs):
    raws = H.bake_ao2(groups, groups, None, core_occluders=groups)
    for ob, g, raw in zip(objs, groups, raws):
        H.write_ao2(ob, g, raw)


def entry(g, extra=None):
    lo, hi = H.u_bounds(g)
    d = dict(name=g.name, file='Art/Export/VillageBuildings/' + g.name + '.fbx', triangles=g.tris(), vertices=len(g.V), bounds_min=lo, bounds_max=hi,
             material=g.material or 'M_Toon_Base', slots=sorted(set(s for s, _ in g.SL)), layers=sorted(set(l for _, l in g.SL)), items=g.items)
    if g.note:
        d['note'] = g.note
    for k, v in g.extra.items():
        d[k] = v
    if extra:
        d.update(extra)
    return d


def _rgb_node(nt, rgb):
    n = nt.nodes.new('ShaderNodeRGB')
    n.outputs[0].default_value = H.srgb_to_lin(rgb) + (1.0,)
    return n.outputs[0]


def ground_material(name='FG_Village_Ground', rgb=(0.40, 0.58, 0.28), scale=0.35):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m, nt, out = H._fresh_material(name)
    H._toon_lighting(nt, H._value_breakup(nt, _rgb_node(nt, rgb), scale), out)
    return m


def glass_material():
    m = bpy.data.materials.get('FG_Village_Glass')
    if m:
        return m
    m = bpy.data.materials.new('FG_Village_Glass')
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = H.srgb_to_lin((0.62, 0.82, 0.86)) + (1,)
    b.inputs['Roughness'].default_value = 0.05
    b.inputs['Alpha'].default_value = 0.28
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m


def water_material():
    m = bpy.data.materials.get('FG_Village_Water')
    if m:
        return m
    m = bpy.data.materials.new('FG_Village_Water')
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = H.srgb_to_lin((0.12, 0.62, 0.66)) + (1,)
    b.inputs['Roughness'].default_value = 0.1
    b.inputs['Alpha'].default_value = 0.55
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m


def preview_scene(ground_half=(60.0, 40.0), ground_c=(0.0, 0.0), ground=True):
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    col = H.collection('VillagePreview')
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    if ground:
        hx, hz = ground_half
        cx, cz = ground_c
        me = bpy.data.meshes.new('FG_Village_GroundPlane')
        pts = [to_b((cx - hx, 0.0, cz - hz)), to_b((cx + hx, 0.0, cz - hz)), to_b((cx + hx, 0.0, cz + hz)), to_b((cx - hx, 0.0, cz + hz))]
        me.from_pydata(pts, [], [(0, 3, 2, 1)])
        me.materials.append(ground_material())
        col.objects.link(bpy.data.objects.new('FG_Village_GroundPlane', me))
    ld = bpy.data.lights.new('FG_Preview_Sun', 'SUN')
    ld.energy = 3.6
    ld.angle = math.radians(8)
    ld.color = H.srgb_to_lin(H.TOON_LIGHT)
    sun = bpy.data.objects.new('FG_Preview_Sun', ld)
    col.objects.link(sun)
    sun.rotation_euler = (math.radians(42), math.radians(8), math.radians(205))
    cd = bpy.data.cameras.new('FG_Preview_Cam')
    cd.clip_end = 900
    cam = bpy.data.objects.new('FG_Preview_Cam', cd)
    col.objects.link(cam)
    sc.camera = cam
    if not sc.world:
        sc.world = bpy.data.worlds.new('World')
    sc.world.use_nodes = True
    nt = sc.world.node_tree
    bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    if not any(n.type == 'TEX_SKY' for n in nt.nodes):
        sky = nt.nodes.new('ShaderNodeTexSky')
        for t in ('PREETHAM', 'HOSEK_WILKIE', 'NISHITA'):
            try:
                sky.sky_type = t
                break
            except Exception:
                pass
        try:
            sky.sun_direction = (0.35, -0.55, 0.62)
            sky.turbidity = 2.6
        except Exception:
            pass
        nt.links.new(sky.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 0.6
    try:
        sc.view_settings.view_transform = 'Standard'
    except Exception:
        pass
    return cam


def to_b(p):
    return tuple(H.to_blender(p))


def render(path, eye_u, target_u, lens=30, res=(1280, 720), levels=48, samples=24):
    sc = bpy.context.scene
    cam = sc.camera
    eye, tgt = H.to_blender(eye_u), H.to_blender(target_u)
    cam.location = eye
    cam.rotation_euler = (tgt - eye).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGB'
    sc.render.image_settings.color_depth = '8'
    sc.render.image_settings.compression = 100
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    if levels:
        H.shrink_png(path, levels)


def place(ob, pos, yaw=0.0):
    ob.location = to_b(pos)
    ob.rotation_euler = (0.0, 0.0, math.radians(-yaw))


def preview_materials(objs_groups, glass_names=(), water_names=()):
    tm = H.toon_material()
    for ob, g in objs_groups:
        ob.data.materials.clear()
        if g.name.endswith('_Glass'):
            ob.data.materials.append(glass_material())
        elif g.name.endswith('_Water'):
            ob.data.materials.append(water_material())
        else:
            ob.data.materials.append(tm)


# ---------------------------------------------------------------------------------------------------------------------------------------
# The Garys' cottages (Gary and Gary Nettle, net-menders who disagree on principle): IDENTICAL plans. Gary T (tidy) and Gary C (chaos).
# Their jumpers are brick red (S Roof) and orange (S Accent); the two houses wear the same two colours swapped: Tidy has orange trim and a red
# door and shutters, Chaos has red trim and an orange door and shutters. One run-down model serves both (the same plan, paint long gone).
# ---------------------------------------------------------------------------------------------------------------------------------------
JUMPER_A, JUMPER_B = S['Accent'], S['Roof']


def jumper(g, f, c, body, band, r, rot=None, sag=0.0):
    # a hanging knitted jumper: body, two sleeves hanging down at an angle, a coloured hem band, a neck (about 0.7 x 0.6 m with the sleeves)
    rot = rot if rot is not None else Matrix.Identity(3)
    parts = ((Vector((0.0, 0.0, 0.0)), (0.46, 0.5, 0.05), body, None), (Vector((0.0, -0.22, 0.0)), (0.48, 0.09, 0.055), band, None),
             (Vector((0.0, 0.27, 0.0)), (0.16, 0.05, 0.06), band, None),
             (Vector((-0.34, -0.02, 0.0)), (0.13, 0.42, 0.05), body, H.rz(22)), (Vector((0.34, -0.02, 0.0)), (0.13, 0.42, 0.05), body, H.rz(-22)))
    for off, size, slot, lrot in parts:
        v = rot @ off
        put(g, f, cube(size), (c[0] + v.x, c[1] + v.y, c[2] + v.z), slot, L['Cloth'], 0.6, rot=rot if lrot is None else rot @ lrot)


def gary_plan(g, f, trim, door_c, run, r):
    W, D, eave, ridge = 4.8, 4.2, 2.5, 4.0
    pad(g, Fr(), 7.2, 6.0)
    g.mark()
    plinth(g, f, W, D)
    shell(g, f, W, D, eave, ridge, S['Sand'], L['Sand'], S['Dock'], L['RoofTile'], trim)
    ck = chimney(g, f, -1.5, Y0 + ridge - 0.9, Y0 + ridge + 0.85)
    fz = D / 2
    door(g, f, 0.0, fz, w=1.0, slot=door_c, trim=trim, step=True)
    for wx in (-1.6, 1.6):
        win(g, f, wx, Y0 + 1.5, fz, 0.8, 0.9, trim, None)
    sb = wsub(f, 'back')
    win(g, sb, 0.9, Y0 + 1.5, D / 2, 0.7, 0.7, trim)
    door(g, sb, -1.0, D / 2, slot=S['WoodDark'], step=False)
    wg = wsub(f, 'W')
    win(g, wg, 0.0, Y0 + eave + 0.4, W / 2, 0.5, 0.5, trim)
    # the net store: a lean-to on the east gable with a closed front
    lean_to(g, f, W / 2, W / 2 + 1.3, -1.4, 1.4, 1.9, 1.4, S['WoodDark'], S['Dock'], wall_slot=S['Wood'])
    bx(g, f, (W / 2 + 0.65, 0.1, 0), (1.3, 0.2, 2.8), S['Dock'], L['Wood'], 0.5, r=0.03)
    return ck, fz, W, D, eave, ridge


def gary(kind, run=False):
    tidy = kind == 'tidy'
    name = 'Village_Gary_Rundown' if run else ('Village_Gary_Tidy' if tidy else 'Village_Gary_Chaos')
    g = make_group(name, 'The Garys\' cottage plan (' + ('run-down, shared by both' if run else ('tidy: orange trim, red door and shutters' if tidy else
                   'chaos: red trim, orange door and shutters, a boat on the roof')) + ').')
    r = vrng('gary.' + ('run' if run else kind))
    f = Fr((-0.6, 0, -0.5))
    if run:
        trim, door_c = S['WoodDark'], S['Wood']
    else:
        trim, door_c = (JUMPER_A, JUMPER_B) if tidy else (JUMPER_B, JUMPER_A)
    ck, fz, W, D, eave, ridge = gary_plan(g, f, trim, door_c, run, r)
    slope = math.degrees(math.atan2(ridge - eave, D / 2))
    sh = door_c
    if run:
        board_up(g, f, 0.0, Y0 + 1.15, fz, 1.0, 1.95, r, 0.2)
        for wx in (-1.6, 1.6):
            board_up(g, f, wx, Y0 + 1.5, fz, 0.8, 0.9, r)
        board_up(g, wsub(f, 'back'), 0.9, Y0 + 1.5, D / 2, 0.7, 0.7, r)
        cb(g, f, (W / 2 + 0.65, Y0 + 0.7, 1.43), (1.0, 1.4, 0.05), S['Wood'], L['Wood'], 0.5)
        tarp(g, f, (-1.0, roof_y(Y0, eave, ridge, D, 1.0) + 0.04, 1.0), (1.8, 0.03, 1.5), r, H.rx(slope))
        dim_patch(g, f, (1.2, roof_y(Y0, eave, ridge, D, -0.9) + 0.02, -0.9), (1.0, 0.03, 0.8), H.rx(-slope))
        weeds(g, f, r, [(-2.6, 2.4), (-1.3, 2.6), (1.3, 2.6), (3.0, 2.3), (3.4, -1.5), (-3.2, -1.2)], 5, 0.6)
        label(g, f, 'GARY. GARY.', 0.09, (0.0, Y0 + 2.37, fz + 0.08), S['Sand'])
        body_collider(g)
        return g, dict(role='cottage', lot_class='S', door_offset_x=-0.6, variant='Gary', rundown=True)
    # ---- shared: window boxes, a plaque
    if tidy:
        # --- the tidy Gary: everything in its place
        for wx in (-1.6, 1.6):
            flowerbox(g, f, wx, Y0 + 0.82, fz + 0.16, 0.9, r, slot_box=trim, cols=(JUMPER_A, JUMPER_B))
            for s_ in (-1, 1):                                                    # straight, matching shutters
                bx(g, f, (wx + s_ * 0.62, Y0 + 1.5, fz + 0.04), (0.36, 0.98, 0.04), sh, L['Wood'], 0.5, r=0.015)
        # buoys hung in an even row under the eave, alternating jumper colours
        for k, bxp in enumerate((-2.15, -1.65, -1.15, 1.15, 1.65, 2.15)):
            bl(g, f, (bxp, Y0 + eave - 0.34, fz + 0.12), 0.12, r, JUMPER_A if k % 2 == 0 else JUMPER_B, L['none'], (1.0, 1.1, 1.0), 1, 0.03, 0.6)
            cb(g, f, (bxp, Y0 + eave - 0.13, fz + 0.06), (0.025, 0.22, 0.025), S['Rock'], L['none'], 0.5)
        # a net rack against the store: four neatly folded nets on a shelf, a coiled rope, labelled crates stacked square
        for k in range(4):
            bx(g, f, (W / 2 + 0.65, 0.36 + 0.09 * k, 1.5 + 0.15), (0.95, 0.08, 0.4), S['Sand'] if k % 2 == 0 else S['Reed'], L['Cloth'], 0.55, r=0.02)
        bx(g, f, (W / 2 + 0.65, 0.3, 1.65), (1.0, 0.06, 0.5), S['Wood'], L['Wood'], 0.5, r=0.02)
        for k, (cx_, cy_, s_) in enumerate(((-2.7, 0.0, 0.5), (-2.15, 0.0, 0.5), (-2.42, 0.5, 0.46))):
            bx(g, f, (cx_, cy_ + s_ / 2 + 0.03, fz + 0.65), (s_, s_, s_), S['Wood'], L['Wood'], 0.5, r=0.03)
            cb(g, f, (cx_, cy_ + s_ / 2 + 0.03, fz + 0.65 + s_ / 2 + 0.005), (s_ * 0.5, 0.1, 0.01), JUMPER_A, L['none'], 0.6)
        H.rope_coil(g, 'x', f.P(2.4, PAD_TOP, fz + 0.75))
        g.items.pop()
        bx(g, f, (0.0, 0.06, fz + 1.0), (0.8, 0.03, 0.5), S['Reed'], L['Cloth'], 0.55, r=0.01)         # a doormat, square to the door
        # mailbox straight on its post, a plaque, two clipped shrubs, a neat washing line with two jumpers
        lt(g, f, (-0.9, 0.0, fz + 1.05), [(0.0, 0.04), (0.95, 0.04), (0.97, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
        bx(g, f, (-0.9, 1.07, fz + 1.05), (0.22, 0.22, 0.34), JUMPER_B, L['none'], 0.55, r=0.05)
        for sx in (-1.0, 1.0):
            bx(g, f, (sx * 0.9 + 0.0, 0.3, fz + 1.1), (0.5, 0.5, 0.5), S['Foliage'], L['Foliage'], 0.6, r=0.1)
        for px in (-3.2, -1.4):
            lt(g, f, (px, 0.0, fz + 1.0), [(0.0, 0.035), (1.9, 0.03), (1.92, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
        tb(g, f, sag_pts((-3.2, 1.82, fz + 1.0), (-1.4, 1.82, fz + 1.0), 0.06), 0.01, S['Sand'], L['Cloth'], 0.5, 3)
        jumper(g, f, (-2.8, 1.5, fz + 1.0), JUMPER_A, JUMPER_B, r)
        jumper(g, f, (-1.8, 1.5, fz + 1.0), JUMPER_B, JUMPER_A, r)
        bx(g, f, (0.0, Y0 + 2.37, fz + 0.04), (1.5, 0.27, 0.05), S['Sand'], L['Wood'], 0.5, r=0.02)
        label(g, f, 'GARY. OLDER.', 0.1, (0.0, Y0 + 2.37, fz + 0.08), trim)
    else:
        # --- the chaotic Gary: nets everywhere, a boat on the roof
        for wx, ang in ((-1.6, 31.0), (1.6, -24.0)):                              # shutters hanging off at odd angles
            for s_, dang in ((-1, 0.0), (1, 38.0)):
                bx(g, f, (wx + s_ * 0.64, Y0 + 1.46 + 0.04 * s_, fz + 0.12), (0.36, 0.98, 0.04), sh, L['Wood'], 0.5, rot=H.ry(ang * s_ + dang) @ H.rz(r.uniform(-8, 8)), r=0.015)
        flowerbox(g, f, -1.6, Y0 + 0.82, fz + 0.16, 0.9, r, slot_box=trim, cols=(JUMPER_A, JUMPER_B))
        # the dinghy on the roof: hull parallel to the ridge on the front slope, tied down with three ropes
        zb = 0.95
        yb = roof_y(Y0, eave, ridge, D, zb) + 0.2
        dn = H.hull(2.3, 0.95, 0.4, hull_slot=JUMPER_A, stripe=JUMPER_B)
        H.add_part(g, dn, f.P(-0.2, yb, zb), f.R @ H.rx(slope) @ H.ry(-76) @ H.rz(7), 0.5)
        for k, bxp in enumerate((-1.1, -0.2, 0.7)):
            tb(g, f, [(bxp, yb + 0.12, zb), (bxp + r.uniform(-0.1, 0.1), yb + 0.05, zb + 0.5), (bxp + r.uniform(-0.2, 0.2), Y0 + eave + 0.2, fz + 0.15 + 0.2 * k)], 0.015, S['Sand'], L['Cloth'], 0.55, 4)
        lt(g, f, (-0.7, yb + 0.15, zb - 0.2), [(0.0, 0.025), (1.8, 0.025), (1.82, 0.0)], S['Wood'], L['Wood'], 0.5, 6, rot=H.ry(30) @ H.rz(70))     # a stray oar
        # nets: draped over the front slope's eave, over a window, tumbling off the store, heaped by the door, strung across the path
        net = H.cloth_panel(2.2, 1.5, 9, 5, 0.1, 0.5, sag=0.2)
        put(g, f, net, (-2.5, Y0 + eave + 0.05, fz + 0.22), S['Sand'], L['Cloth'], 0.55, rot=H.rx(14))
        net2 = H.cloth_panel(1.5, 1.6, 6, 5, 0.12, 1.5, sag=0.25)
        put(g, f, net2, (1.0, Y0 + eave - 0.05, fz + 0.2), S['Reed'], L['Cloth'], 0.55, rot=H.ry(12))
        net3 = H.cloth_panel(1.2, 1.5, 5, 5, 0.1, 0.2, sag=0.1)
        put(g, f, net3, (W / 2 + 0.1, Y0 + 1.9, 1.42), S['Sand'], L['Cloth'], 0.55, rot=H.ry(-8))
        for k in range(4):
            bl(g, f, (-2.7 + 0.5 * k + r.uniform(-0.1, 0.1), 0.18, fz + 0.75 + r.uniform(-0.1, 0.1)), 0.4, r, S['Sand'] if k % 2 else S['Reed'], L['Cloth'],
               (1.0, 0.45, 1.0), 1, 0.4, 0.55)
        for k in range(5):
            a = (r.uniform(-3.0, 2.2), r.uniform(0.2, 1.1), fz + r.uniform(0.7, 1.1))
            b = (a[0] + r.uniform(-1.6, 1.6), r.uniform(0.1, 1.4), min(fz + 1.2, a[2] + r.uniform(-0.6, 0.4)))
            tb(g, f, sag_pts(a, b, r.uniform(0.05, 0.25), 8), 0.015, S['Sand'], L['Cloth'], 0.55, 4)
        tb(g, f, sag_pts((-0.1, Y0 + 1.9, fz + 0.15), (1.9, 0.5, fz + 1.35), 0.3, 10), 0.016, S['Reed'], L['Cloth'], 0.55, 4)      # across the doorway
        # buoys scattered like dropped sweets, and two hung crooked
        for k in range(8):
            bl(g, f, (r.uniform(-3.0, 3.0), 0.14, fz + r.uniform(0.5, 1.05)), 0.13, r, JUMPER_A if k % 2 == 0 else JUMPER_B, L['none'], (1.0, 1.1, 1.0), 1, 0.03, 0.6)
        for bxp in (-0.8, 2.2):
            tb(g, f, [(bxp, Y0 + eave - 0.1, fz + 0.08), (bxp + 0.15, Y0 + eave - 0.45, fz + 0.2)], 0.012, S['Sand'], L['Cloth'], 0.55, 3)
            bl(g, f, (bxp + 0.15, Y0 + eave - 0.6, fz + 0.2), 0.14, r, JUMPER_B, L['none'], (1.0, 1.1, 1.0), 1, 0.03, 0.6)
        # crates: toppled, one on its side with a fish spilling out
        for k, (cx_, cz_, yaw_, tilt) in enumerate(((-2.8, 0.5, 25, 0), (-2.2, 0.85, -40, 70), (-2.9, 1.05, 70, 20))):
            bx(g, f, (cx_, 0.26 + (0.1 if tilt else 0.0), fz + cz_), (0.5, 0.5, 0.5), S['Wood'], L['Wood'], 0.5, rot=H.ry(yaw_) @ H.rx(tilt), r=0.03)
        put(g, f, H.blob(0.22, r, (1.8, 0.4, 0.6), subdiv=1, rough=0.05, flat=-2), (-1.7, 0.15, fz + 1.0), S['Sand'], L['none'], 0.6, rot=H.ry(35))
        # mailbox leaning, a crooked plaque, a washing line of three jumpers sagging over the roof edge and one hung from the chimney
        lt(g, f, (-1.0, 0.0, fz + 1.0), [(0.0, 0.04), (0.95, 0.04), (0.97, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6, rot=H.rz(14) @ H.rx(-6))
        bx(g, f, (-1.17, 0.98, fz + 0.98), (0.22, 0.22, 0.34), JUMPER_A, L['none'], 0.55, rot=H.rz(14), r=0.05)
        tb(g, f, sag_pts((2.4, Y0 + eave + 0.3, fz + 0.35), (3.4, 1.0, fz + 1.0), 0.15), 0.01, S['Sand'], L['Cloth'], 0.5, 3)
        jumper(g, f, (2.6, Y0 + eave - 0.2, fz + 0.45), JUMPER_B, JUMPER_A, r, rot=H.ry(8))
        jumper(g, f, (3.1, 0.95, fz + 1.0), JUMPER_A, JUMPER_B, r, rot=H.rz(20))
        jumper(g, f, (-1.5, Y0 + ridge - 0.3, -0.4), JUMPER_B, JUMPER_A, r, rot=H.rx(-20))      # snagged on the chimney side of the ridge
        bx(g, f, (0.1, Y0 + 2.37, fz + 0.04), (1.5, 0.27, 0.05), S['Sand'], L['Wood'], 0.5, rot=H.rz(-9), r=0.02)
        label(g, f, 'GARY. CORRECT.', 0.095, (0.1, Y0 + 2.37, fz + 0.08), trim, rot=H.rz(-9))
    body_collider(g)
    smoke(g, ck, 'Chimney_Gary' + ('Tidy' if tidy else 'Chaos'))
    return g, dict(role='cottage', lot_class='S', door_offset_x=-0.6, variant='Gary' + ('Tidy' if tidy else 'Chaos'), rundown=False,
                   jumper_slots=dict(orange=S['Accent'], red=S['Roof']),
                   trim_slot=trim, door_slot=door_c)


# ---------------------------------------------------------------------------------------------------------------------------------------
# Lorraine's cafe: striped awning, outdoor tables, a chalkboard with a dry joke. Footprint 9.2 x 6.0 (the body 6.4 x 3.6 at the back, a 2.4 m
# terrace in front under and beside the awning).
# ---------------------------------------------------------------------------------------------------------------------------------------
def table_set(g, f, x, z, r, yaw=0.0, parasol=False, run=False, toppled=False):
    tf = Fr((x, 0, z), yaw, par=f)
    if toppled:
        tf = Fr((x, 0.0, z), yaw, par=f)
    top_r = 0.46
    tilt = H.rx(86) if toppled else None
    ty = 0.76 if not toppled else 0.46
    lt(g, tf, (0, ty, 0), [(0.0, top_r), (0.03, top_r + 0.02), (0.06, top_r), (0.06, 0.0)], S['Wood'], L['Wood'], 0.5, 10, cap1=True, rot=tilt)
    lt(g, tf, (0, 0.0 if not toppled else 0.46, 0), [(0.0, 0.2), (0.03, 0.2), (0.05, 0.04), (0.74, 0.035), (0.76, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6, rot=tilt)
    if toppled:
        return
    for s_ in (-1, 1):
        cf = Fr((s_ * 0.66, 0, 0), 90 * s_, par=tf)                 # chair faces the table
        cb(g, cf, (0, 0.44, 0), (0.4, 0.05, 0.4), S['Wood'], L['Wood'], 0.5)
        for lx in (-0.17, 0.17):
            for lz in (-0.17, 0.17):
                cb(g, cf, (lx, 0.22, lz), (0.04, 0.44, 0.04), S['WoodDark'], L['Wood'], 0.45)
        cb(g, cf, (0, 0.72, -0.18), (0.4, 0.04, 0.04), S['Wood'], L['Wood'], 0.5)
        cb(g, cf, (0, 0.62, -0.18), (0.4, 0.04, 0.04), S['Wood'], L['Wood'], 0.5)
        for lx in (-0.18, 0.18):
            cb(g, cf, (lx, 0.58, -0.18), (0.04, 0.3, 0.04), S['WoodDark'], L['Wood'], 0.45)
    # a cup and saucer, a sugar pot
    lt(g, tf, (0.12, 0.82, 0.05), [(0.0, 0.0), (0.0, 0.07), (0.06, 0.065), (0.07, 0.0)], S2['Snow'], L['none'], 0.6, 8)
    lt(g, tf, (-0.15, 0.82, -0.08), [(0.0, 0.05), (0.1, 0.06), (0.13, 0.0)], S['Roof'], L['none'], 0.6, 8)
    if parasol:
        lt(g, tf, (0, 0.0, 0), [(0.0, 0.03), (2.2, 0.025), (2.22, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
        umbrella(g, tf, (0, 2.12, 0), 1.1, 0.46, (S['Roof'], S2['Snow']))
        put(g, tf, H.lathe([(0.0, 0.0), (0.03, 0.04), (0.1, 0.0)], 8)[0], (0, 2.34, 0), S2['Snow'], L['none'], 0.6)


def umbrella(g, f, c, r_out, h, slots, n=8):
    # a parasol canopy of n striped wedges (alternating slots): rings at (height, radius)
    prof = [(h, 0.0), (h * 0.93, r_out * 0.35), (h * 0.55, r_out * 0.8), (0.0, r_out)]
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        V_, N_, F_, UV_ = [], [], [], []
        for (y, rr) in prof:
            for a in (a0, a1):
                V_.append(Vector((rr * math.cos(a), y, rr * math.sin(a))))
                N_.append(Vector((0.55 * math.cos(a), 0.85, 0.55 * math.sin(a))).normalized())
        for i in range(len(prof) - 1):
            b = 2 * i
            F_.append([b, b + 1, b + 3, b + 2])
            UV_.append([(V_[j].x, V_[j].z) for j in F_[-1]])
        put(g, f, (V_, N_, F_, UV_), c, slots[k % len(slots)], L['Cloth'], 0.6)


def awning_valance(g, f, x, y, z, w, slot, thick=0.03):
    # one pointed flap of the awning's front edge (a prism in the x-y plane, thin in z)
    poly = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, -0.2), (0.0, -0.34), (-w / 2, -0.2)]
    put(g, f, H.prism(poly, thick, 0.0, segs=1), (x, y, z), slot, L['Cloth'], 0.6, rot=H.ry(90))


def cafe(run=False):
    g = make_group('Village_Cafe' + ('_Rundown' if run else ''), 'Lorraine\'s cafe (pistachio walls, striped awning, three tables, a chalkboard).', lmap={})
    r = vrng('cafe' + ('r' if run else ''))
    f0 = Fr()
    W, D, eave, ridge = 6.4, 3.6, 3.8, 5.1
    fb = Fr((0, 0, -1.0))
    trim = S2['Snow'] if not run else S['WoodDark']
    pad(g, f0, 9.4, 6.2)
    g.mark()
    plinth(g, fb, W, D)
    shell(g, fb, W, D, eave, ridge, S['Reed'], L['Plaster'], S['Rock'], L['RoofTile'], trim)
    ck = chimney(g, fb, -W / 2 + 0.8, Y0 + ridge - 0.9, Y0 + ridge + 0.9, z=-0.4)
    fz = D / 2
    slope = math.degrees(math.atan2(ridge - eave, D / 2))
    # shopfront window and door
    win(g, fb, -1.2, Y0 + 1.35, fz, 2.6, 1.3, trim, None)
    door(g, fb, 2.2, fz, w=1.0, slot=S['Roof'] if not run else S['Wood'], trim=trim, step=True)
    if not run:
        label(g, fb, 'OPEN-ISH', 0.09, (2.2, Y0 + 1.55, fz + 0.14), S2['Snow'])
        label(g, fb, 'NO REFUNDS ON GRAVY', 0.075, (-1.2, Y0 + 1.0, fz + 0.075), S2['Snow'])
    # the sign board over the awning
    bx(g, fb, (0.0, Y0 + 3.25, fz + 0.09), (4.6, 0.55, 0.12), S2['PineDark'], L['Wood'], 0.5, r=0.04)
    label(g, fb, "LORRAINE'S", 0.3 if not run else 0.28, (0.0, Y0 + 3.25, fz + 0.16), S2['Snow'] if not run else S['Sand'])
    # side and back windows
    sb = wsub(fb, 'back')
    win(g, sb, 1.2, Y0 + 1.5, D / 2, 0.8, 0.8, trim)
    win(g, sb, -1.2, Y0 + 1.5, D / 2, 0.8, 0.8, trim)
    for side in ('E', 'W'):
        sw = wsub(fb, side)
        win(g, sw, 0.0, Y0 + 1.5, W / 2, 0.8, 0.9, trim)
    # a hanging cafe sign on a bracket at the west corner: a big cup
    bx(g, fb, (-W / 2 - 0.55, Y0 + 2.5, fz - 0.4), (0.06, 0.06, 0.9), S['WoodDark'], L['Wood'], 0.5, r=0.02)
    cup = Fr((-W / 2 - 0.55, Y0 + 2.2, fz - 0.15), 0, par=fb)
    lt(g, cup, (0, 0, 0), [(0.0, 0.0), (0.02, 0.12), (0.28, 0.2), (0.3, 0.19)], S2['Snow'], L['none'], 0.6, 10)
    bx(g, cup, (0.24, 0.17, 0), (0.1, 0.18, 0.04), S2['Snow'], L['none'], 0.6, rot=H.rz(0), r=0.02)
    # terrace: awning (stripes + valance flaps) over the front, two posts, string lights
    aw_y, aw_run, aw_drop = Y0 + 2.55, 1.55, 0.6
    aang = math.degrees(math.atan2(aw_drop, aw_run))
    alen = math.hypot(aw_run, aw_drop)
    n = 9
    sw_ = 6.8 / n
    stripe_cols = (S['Roof'], S2['Snow']) if not run else (S['Dock'], S['Sand'])
    for k in range(n):
        x = -3.4 + sw_ * (k + 0.5)
        if run and k >= 5:
            continue                                                      # the right half has collapsed
        sag = 0.0
        slotc = stripe_cols[k % 2]
        bx(g, fb, (x, aw_y - aw_drop / 2, fz + aw_run / 2 + 0.02), (sw_ - 0.01, 0.035, alen), slotc, L['Cloth'], 0.6, rot=H.rx(aang + (14 if run else 0)), r=0.01, segs=1)
        awning_valance(g, fb, x, aw_y - aw_drop + (-0.2 if run else 0.0), fz + aw_run + (-0.1 if run else 0.0), sw_ - 0.02, slotc)
    pz = fz + aw_run - 0.05
    for sx in ((-3.15, 3.15) if not run else (-3.15,)):
        lt(g, fb, (sx, 0.0, pz), [(0.0, 0.05), (aw_y - aw_drop, 0.045), (aw_y - aw_drop + 0.04, 0.0)], S['WoodDark'], L['Wood'], 0.5, 8)
    if run:
        # the collapsed right half: stripes lie on the terrace, one post fallen
        for k in range(5, n):
            x = -3.4 + sw_ * (k + 0.5)
            bx(g, fb, (x, 0.25 + 0.05 * (k % 2), fz + 1.2), (sw_ - 0.01, 0.04, 1.5), stripe_cols[k % 2], L['Cloth'], 0.6, rot=H.ry(r.uniform(-12, 12)) @ H.rx(r.uniform(-6, 6)), r=0.01)
        lt(g, fb, (1.3, 0.1, pz - 0.5), [(0.0, 0.05), (1.8, 0.045), (1.84, 0.0)], S['WoodDark'], L['Wood'], 0.5, 8, rot=H.rz(-88))
    else:
        for (a, b) in (((-3.15, 2.2, pz), (3.15, 2.2, pz)), ((-3.15, 2.2, pz), (-3.15, 2.5, fz)), ((3.15, 2.2, pz), (3.15, 2.5, fz))):
            pts = sag_pts(fb.P(*a), fb.P(*b), 0.12, 8)
            g.add(H.tube(pts, 0.01, 3, caps=False), S['WoodDark'], L['none'], 0.5)
        for k in range(14):
            t = (k + 0.5) / 14
            p = sag_pts(fb.P(-3.15, 2.2, pz), fb.P(3.15, 2.2, pz), 0.12, 14)
            pp = Vector(fb.P(-3.15, 2.2, pz)).lerp(Vector(fb.P(3.15, 2.2, pz)), t) - Vector((0, 0.12 * 4 * t * (1 - t) + 0.05, 0))
            put(g, Fr(), H.blob(0.04, r, (1, 1.2, 1), subdiv=1, rough=0.02, flat=-2), (pp.x, pp.y, pp.z), S['Accent'], L['none'], 0.8)
    body_collider(g)
    # tables on the terrace (world frame f0): two under the awning, one in the sun with a parasol
    spots = [(-2.6, 1.55, 6), (-0.5, 1.6, -4)]
    if run:
        for k, (x, z, yw) in enumerate(spots):
            g.mark()
            table_set(g, f0, x, z, r, yw, toppled=(k == 0))
            if k == 1:
                g.solid('Table_%d' % k)
        table_set(g, f0, 3.5, 1.9, r, 0)
    else:
        for k, (x, z, yw) in enumerate(spots):
            g.mark()
            table_set(g, f0, x, z, r, yw)
            g.solid('Table_%d' % k)
        g.mark()
        table_set(g, f0, 3.5, 1.9, r, 20, parasol=True)
        g.solid('Table_2')
    # the chalkboard A-frame at the east end of the terrace
    g.mark()
    cf = Fr((-4.05, 0.0, 2.35), 22, par=f0)
    for s_ in (-1, 1):
        cb(g, cf, (s_ * 0.3, 0.45, 0.0), (0.05, 0.95, 0.05), S['WoodDark'], L['Wood'], 0.5, rot=H.rx(-8))
    bx(g, cf, (0, 0.62, 0.08), (0.7, 0.9, 0.05), S['WoodDark'], L['Wood'], 0.5, rot=H.rx(-10), r=0.02)
    bx(g, cf, (0, 0.62, 0.105), (0.6, 0.8, 0.02), S2['PineDark'], L['none'], 0.5, rot=H.rx(-10), r=0.01)
    if not run:
        label(g, cf, 'SOUP OF THE DAY:', 0.075, (0.0, 0.86, 0.13), S2['Snow'], depth=0.006, rot=H.rx(-10))
        label(g, cf, "YESTERDAY'S", 0.085, (0.0, 0.64, 0.13), S2['Snow'], depth=0.006, rot=H.rx(-10))
        label(g, cf, '(SMALL OR NONE)', 0.055, (0.0, 0.43, 0.13), S['Sand'], depth=0.006, rot=H.rx(-10))
    else:
        label(g, cf, 'BACK IN 5 MIN.', 0.07, (0.0, 0.7, 0.13), S['Sand'], depth=0.006, rot=H.rx(-10))
    g.solid('Chalkboard')
    if not run:
        # planters flank the door: barrels with shrubs
        for sx in (1.3, 3.2):
            g.mark()
            lt(g, fb, (sx, 0.0, fz + 0.5), [(0.0, 0.2), (0.02, 0.24), (0.35, 0.26), (0.45, 0.25), (0.46, 0.2)], S['WoodDark'], L['Wood'], 0.5, 10, cap0=True)
            bl(g, fb, (sx, 0.62, fz + 0.5), 0.3, r, S['Foliage'], L['Foliage'], (1.0, 0.9, 1.0), 2, 0.25)
            g.solid('Planter_%d' % int(sx > 2))
        smoke(g, ck, 'Chimney_Cafe')
    else:
        board_up(g, fb, 2.2, Y0 + 1.15, fz, 1.0, 1.95, r, 0.2)
        board_up(g, fb, -1.2, Y0 + 1.35, fz, 2.6, 1.3, r, 0.5)
        tarp(g, fb, (-1.2, roof_y(Y0, eave, ridge, D, 0.9) + 0.04, 0.9), (2.0, 0.03, 1.5), r, H.rx(slope))
        weeds(g, f0, r, [(-4.0, 0.8), (-1.5, 2.8), (1.2, 2.8), (4.3, 0.5), (-4.3, -1.0), (4.3, -1.5)], 5, 0.6)
        tilt_text = H.rz(0)
    return g, dict(role='cafe', lot_class='M', door_offset_x=2.2, variant='Cafe', rundown=run)


# ---------------------------------------------------------------------------------------------------------------------------------------
# Aquarium tank set: separate props. Each tank is THREE objects sharing one origin (base centre on the floor, front +z): the body (cabinet, frame,
# gravel, decor), `_Glass` (panes only: put a glass material on it) and `_Water` (the water volume: a water material). Fish mount points are in the
# manifest in tank-local Unity metres.
# ---------------------------------------------------------------------------------------------------------------------------------------
def plane_box(g, f, c, size, slot=None):
    cb(g, f, c, size, S['Foliage'] if slot is None else slot, L['none'], 0.9)


def kelp(g, f, x, z, y0, r, n=3, hgt=0.7, slot=None):
    for k in range(n):
        a = r.uniform(0, 2 * math.pi)
        base = f.P(x + r.uniform(-0.06, 0.06), y0, z + r.uniform(-0.06, 0.06))
        d = f.R @ Vector((math.cos(a), 0.0, math.sin(a)))
        g.add(H.blade_geo(base, d, hgt * r.uniform(0.7, 1.15), 0.07, r.uniform(0.3, 0.7), segs=3), slot or (S['FoliageDark'] if k % 2 else S['Foliage']), L['Foliage'], 0.6)


def display_tank():
    g = make_group('Village_DisplayTank', 'Aquarium display tank body (cabinet, frame, hood, gravel, rocks, kelp, a toy lighthouse). Glass and water are separate objects.',
                   rays=14, ao_dist=0.5, lmap={})
    gg = make_group('Village_DisplayTank_Glass', 'Glass panes of the display tank: put the glass material on this object.', rays=0, lmap={})
    gw = make_group('Village_DisplayTank_Water', 'Water volume of the display tank: put the water material on this object.', rays=0, lmap={})
    r = vrng('displaytank')
    f = Fr()
    Wc, Dc = 2.7, 1.0
    g.mark()
    bx(g, f, (0, 0.4, 0), (Wc, 0.8, Dc), S['WoodDark'], L['Wood'], 0.5, r=0.04)
    for sx in (-0.68, 0.68):
        bx(g, f, (sx, 0.4, Dc / 2 + 0.005), (1.2, 0.62, 0.03), S['Wood'], L['Wood'], 0.5, r=0.01)
        bx(g, f, (-0.08 * (1 if sx > 0 else -1), 0.45, Dc / 2 + 0.035), (0.04, 0.12, 0.03), S['Accent'], L['none'], 0.5, r=0.01)
    bx(g, f, (0, 0.84, 0), (Wc, 0.08, Dc), S['Rock'], L['none'], 0.55, r=0.03)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cb(g, f, (sx * (Wc / 2 - 0.05), 1.38, sz * (Dc / 2 - 0.05)), (0.1, 1.0, 0.1), S['Rock'], L['none'], 0.55)
    bx(g, f, (0, 1.98, 0), (Wc, 0.2, Dc), S['Rock'], L['none'], 0.55, r=0.04)
    cb(g, f, (0, 1.875, 0.0), (2.2, 0.02, 0.14), S2['Snow'], L['none'], 0.9)
    bx(g, f, (0, 0.93, 0), (2.4, 0.1, 0.82), S['Sand'], L['Sand'], 0.6, r=0.04)
    for (x, z, rad) in ((-0.75, 0.08, 0.26), (0.45, -0.12, 0.3), (1.0, 0.16, 0.2)):
        put(g, f, H.blob(rad, r, (1.0, 0.8, 1.0), subdiv=1, rough=0.25), (x, 0.97 + rad * 0.35, z), S['Rock'], L['Rock'], 0.6)
    for (x, z) in ((-1.0, -0.2), (-0.55, 0.2), (0.0, -0.22), (0.9, -0.2), (1.1, 0.0)):
        kelp(g, f, x, z, 0.97, r, 3, 0.75)
    lt(g, f, (-0.2, 0.98, 0.15), [(0.0, 0.1), (0.1, 0.085), (0.3, 0.06), (0.36, 0.0)], S2['Snow'], L['none'], 0.6, 8, ring_slots=[S2['Snow'], S['Roof'], S2['Snow']])
    label(g, f, 'PLEASE DO NOT TAP', 0.06, (0.0, 0.18, Dc / 2 + 0.035), S['Sand'], depth=0.005)
    g.solid('Body')
    # glass: four panes (a hollow box, no top)
    ga = Fr()
    Gx, Gz, Gy0, Gy1, t = 1.26, 0.45, 0.88, 1.88, 0.025
    for sz in (-1, 1):
        cb(gg, ga, (0, (Gy0 + Gy1) / 2, sz * Gz), (2 * Gx, Gy1 - Gy0, t), S2['Snow'], L['none'], 0.9)
    for sx in (-1, 1):
        cb(gg, ga, (sx * Gx, (Gy0 + Gy1) / 2, 0), (t, Gy1 - Gy0, 2 * Gz), S2['Snow'], L['none'], 0.9)
    plane_box(gw, ga, (0, 1.37, 0), (2.4, 0.84, 0.82))
    mounts = []
    pts = [(-0.95, 1.55, -0.1), (-0.5, 1.25, 0.15), (0.0, 1.5, -0.15), (0.45, 1.2, 0.1), (0.9, 1.45, 0.0), (-0.7, 1.1, 0.2), (0.2, 1.65, 0.05), (0.75, 1.7, -0.2)]
    for i, (x, y, z) in enumerate(pts):
        mounts.append(dict(name='FishMount_%d' % i, position=[x, y, z], yaw_deg=90.0 if i % 2 == 0 else 270.0, max_fish_length_m=0.45))
    g.extra['fish_mounts'] = mounts
    g.extra['swim_volume'] = dict(min=[-1.18, 1.0, -0.38], max=[1.18, 1.76, 0.38], note='inside the water volume; keep fish centres 0.05 m inside')
    g.extra['companions'] = ['Village_DisplayTank_Glass', 'Village_DisplayTank_Water']
    g.extra['placement_note'] = 'origin = base centre on the floor, front +z; footprint 2.7 x 1.0 m, height 2.08 m'
    return [g, gg, gw]


def breeding_tank():
    g = make_group('Village_BreedingTank', 'Breeding tank body: two compartments, a pump, air lines, a heater, a breeding trap.', rays=14, ao_dist=0.5, lmap={})
    gg = make_group('Village_BreedingTank_Glass', 'Glass of the breeding tank (outer panes and the centre divider).', rays=0, lmap={})
    gw = make_group('Village_BreedingTank_Water', 'Water volumes of the breeding tank (one box per compartment).', rays=0, lmap={})
    r = vrng('breedingtank')
    f = Fr()
    Wc, Dc = 1.9, 0.9
    g.mark()
    bx(g, f, (0, 0.35, 0), (Wc, 0.7, Dc), S['Wood'], L['Wood'], 0.5, r=0.04)
    for sx in (-0.45, 0.45):
        bx(g, f, (sx, 0.35, Dc / 2 + 0.005), (0.8, 0.56, 0.03), S['Dock'], L['Wood'], 0.5, r=0.01)
    bx(g, f, (0, 0.74, 0), (Wc, 0.08, Dc), S['Rock'], L['none'], 0.55, r=0.03)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cb(g, f, (sx * (Wc / 2 - 0.04), 1.16, sz * (Dc / 2 - 0.04)), (0.08, 0.8, 0.08), S['Rock'], L['none'], 0.55)
    cb(g, f, (0, 1.16, 0), (0.05, 0.8, Dc - 0.1), S['Rock'], L['none'], 0.55)
    bx(g, f, (0, 1.64, 0), (Wc, 0.16, Dc), S['Rock'], L['none'], 0.55, r=0.04)
    cb(g, f, (0, 1.55, 0.0), (1.5, 0.02, 0.12), S2['Snow'], L['none'], 0.9)
    bx(g, f, (0, 0.81, 0), (1.7, 0.08, 0.72), S['Sand'], L['Sand'], 0.6, r=0.03)
    # pump and air lines, a heater, a mesh breeding trap
    bx(g, f, (1.15, 0.16, 0.1), (0.3, 0.3, 0.26), S['Rock'], L['none'], 0.5, r=0.05)
    tb(g, f, [(1.15, 0.32, 0.1), (1.15, 1.3, 0.1), (0.9, 1.74, 0.1), (0.4, 1.74, 0.1), (0.4, 1.4, 0.1)], 0.015, S['WoodDark'], L['none'], 0.5, 4)
    tb(g, f, [(1.22, 0.32, 0.0), (1.22, 1.2, 0.0), (0.95, 1.7, 0.0), (-0.4, 1.74, 0.0), (-0.4, 1.4, 0.0)], 0.015, S['WoodDark'], L['none'], 0.5, 4)
    lt(g, f, (-0.7, 0.86, -0.25), [(0.0, 0.03), (0.6, 0.03), (0.63, 0.0)], S['Roof'], L['none'], 0.6, 6)
    bx(g, f, (0.5, 1.02, 0.1), (0.3, 0.3, 0.26), S['Reed'], L['Cloth'], 0.5, r=0.04)
    kelp(g, f, -0.2, 0.0, 0.86, r, 3, 0.5)
    kelp(g, f, 0.25, 0.0, 0.86, r, 3, 0.5)
    label(g, f, 'DO NOT DISTURB (THEY ARE TRYING)', 0.05, (0.0, 0.18, Dc / 2 + 0.035), S['Sand'], depth=0.005)
    g.solid('Body')
    Gx, Gz, Gy0, Gy1, t = 0.86, 0.4, 0.78, 1.58, 0.022
    for sz in (-1, 1):
        cb(gg, f, (0, (Gy0 + Gy1) / 2, sz * Gz), (2 * Gx, Gy1 - Gy0, t), S2['Snow'], L['none'], 0.9)
    for sx in (-1, 0, 1):
        cb(gg, f, (sx * Gx, (Gy0 + Gy1) / 2, 0), (t, Gy1 - Gy0, 2 * Gz), S2['Snow'], L['none'], 0.9)
    for sx in (-0.42, 0.42):
        plane_box(gw, f, (sx, 1.2, 0), (0.78, 0.64, 0.7))
    mounts = []
    for i, (x, y, z) in enumerate(((-0.55, 1.1, 0.0), (-0.3, 1.3, 0.1), (0.3, 1.2, -0.1), (0.55, 1.35, 0.05))):
        mounts.append(dict(name='FishMount_%d' % i, position=[x, y, z], yaw_deg=90.0 if i % 2 == 0 else 270.0, max_fish_length_m=0.35,
                           compartment='A' if x < 0 else 'B'))
    g.extra['fish_mounts'] = mounts
    g.extra['swim_volume'] = dict(min=[-0.78, 0.92, -0.32], max=[0.78, 1.5, 0.32], note='two compartments split at x = 0 (A west, B east): keep a pair per side')
    g.extra['companions'] = ['Village_BreedingTank_Glass', 'Village_BreedingTank_Water']
    g.extra['placement_note'] = 'origin = base centre on the floor, front +z; footprint 1.9 x 0.9 m (+0.4 m pump on the east side), height 1.72 m'
    return [g, gg, gw]


def starfish(g, f, c, rad, slot, yaw, r):
    for k in range(5):
        a = math.radians(72 * k + yaw)
        d = Vector((math.cos(a), 0, math.sin(a)))
        cb(g, f, (c[0] + d.x * rad * 0.5, c[1], c[2] + d.z * rad * 0.5), (rad, 0.035, rad * 0.32), slot, L['none'], 0.6, rot=H.ry(-math.degrees(a)))
    bl(g, f, c, rad * 0.32, r, slot, L['none'], (1, 0.35, 1), 1, 0.05, 0.6)


def touch_pool():
    g = make_group('Village_TouchPool', 'Touch pool: a low stone basin with a rock rim, seaweed, starfish and a sign. The water is a separate object.', rays=14, ao_dist=0.6,
                   lmap={})
    gw = make_group('Village_TouchPool_Water', 'Water surface of the touch pool: put the water material on this object.', rays=0, lmap={})
    r = vrng('touchpool')
    f = Fr()
    Wp, Dp, Hp, th = 3.0, 1.8, 0.62, 0.2
    g.mark()
    for sz in (-1, 1):
        bx(g, f, (0, Hp / 2, sz * (Dp / 2 - th / 2)), (Wp, Hp, th), S['Rock'], L['StoneWall'], 0.55, r=0.05)
    for sx in (-1, 1):
        bx(g, f, (sx * (Wp / 2 - th / 2), Hp / 2, 0), (th, Hp, Dp - 2 * th), S['Rock'], L['StoneWall'], 0.55, r=0.05)
    bx(g, f, (0, 0.08, 0), (Wp - 2 * th + 0.04, 0.16, Dp - 2 * th + 0.04), S['Sand'], L['Sand'], 0.6, r=0.04)
    for k in range(9):
        t = (k + 0.5) / 9
        side = 1 if k % 2 else -1
        bl(g, f, (-Wp / 2 + 0.35 + (Wp - 0.7) * t, Hp + 0.04, side * (Dp / 2 - th / 2)), r.uniform(0.15, 0.22), r, S['Rock'], L['Rock'], (1.1, 0.7, 1.0), 1, 0.25, 0.6)
    for sx in (-1, 1):
        bl(g, f, (sx * (Wp / 2 - th / 2), Hp + 0.02, r.uniform(-0.3, 0.3)), 0.2, r, S['Rock'], L['Rock'], (0.9, 0.7, 1.2), 1, 0.25, 0.6)
    for (x, z, rad) in ((-0.55, -0.1, 0.32), (0.45, 0.15, 0.26), (-0.9, 0.25, 0.2), (0.95, -0.2, 0.22)):
        put(g, f, H.blob(rad, r, (1.0, 0.7, 1.0), subdiv=1, rough=0.25), (x, 0.18 + rad * 0.2, z), S['Rock'], L['Rock'], 0.6)
    starfish(g, f, (0.1, 0.19, -0.35), 0.17, S['Accent'], 20, r)
    starfish(g, f, (-0.3, 0.19, 0.4), 0.14, S['Roof'], 60, r)
    kelp(g, f, 0.7, -0.45, 0.17, r, 3, 0.45)
    kelp(g, f, -1.0, -0.35, 0.17, r, 3, 0.4)
    # the sign on a post at the east end
    lt(g, f, (Wp / 2 + 0.35, 0.0, -0.55), [(0.0, 0.045), (1.1, 0.04), (1.12, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
    bx(g, f, (Wp / 2 + 0.35, 1.0, -0.5), (0.6, 0.42, 0.05), S['Sand'], L['Wood'], 0.5, r=0.02)
    label(g, f, 'PLEASE TOUCH.', 0.065, (Wp / 2 + 0.35, 1.1, -0.47), S['Roof'])
    label(g, f, "THEY WON'T.", 0.065, (Wp / 2 + 0.35, 0.9, -0.47), S['Roof'])
    g.solid('Body')
    plane_box(gw, f, (0, 0.33, 0), (Wp - 2 * th - 0.02, 0.34, Dp - 2 * th - 0.02))
    g.extra['fish_mounts'] = [
        dict(name='Starfish_0', position=[0.1, 0.2, -0.35], yaw_deg=20.0, kind='starfish', max_length_m=0.35),
        dict(name='Starfish_1', position=[-0.3, 0.2, 0.4], yaw_deg=60.0, kind='starfish', max_length_m=0.3),
        dict(name='Crab_0', position=[0.45, 0.4, 0.15], yaw_deg=140.0, kind='crab', max_length_m=0.25),
        dict(name='Crab_1', position=[-0.55, 0.45, -0.1], yaw_deg=210.0, kind='crab', max_length_m=0.25),
        dict(name='Fish_0', position=[-0.8, 0.32, 0.1], yaw_deg=90.0, kind='fish', max_length_m=0.3),
        dict(name='Fish_1', position=[0.85, 0.34, -0.1], yaw_deg=270.0, kind='fish', max_length_m=0.3)]
    g.extra['swim_volume'] = dict(min=[-1.2, 0.2, -0.65], max=[1.2, 0.5, 0.65], note='shallow: water surface y 0.5')
    g.extra['companions'] = ['Village_TouchPool_Water']
    g.extra['placement_note'] = 'origin = base centre on the ground, front +z; basin 3.0 x 1.8 m, rim 0.62 m, water surface at y 0.5; the sign post stands 0.5 m east of the basin'
    return [g, gw]


# ---------------------------------------------------------------------------------------------------------------------------------------
# Dr. Hollis's aquarium: a glass-fronted hall with a painted false front. Footprint 10.8 x 7.8 (the hall 10.2 x 5.0 at the back, a 2.6 m
# forecourt with the touch-pool spot). Enterable: a 1.3 m doorway in the middle of the frontage, the door leaf standing open. Three display-tank
# platforms along the back wall and a breeding-tank spot on the east side (manifest `tank_mounts`).
# ---------------------------------------------------------------------------------------------------------------------------------------
def painted_line(g, f, text, size, c, slot, r, jitter=0.03, depth=0.01):
    # hand-painted lettering: each word its own little offset and roll, baseline wandering. Glyph width ~ 0.63 x size per character.
    # On a +z face a viewer has +x on their LEFT (see sign_text), so the line is laid out from +x towards -x to read left to right.
    words = text.split(' ')
    cw = 0.63 * size
    total = cw * len(text)
    x = c[0] + total / 2
    for w_ in words:
        wcx = x - cw * len(w_) / 2
        rollv = r.uniform(-3.0, 3.0)
        label(g, f, w_, size, (wcx, c[1] + r.uniform(-jitter, jitter) * 2, c[2]), slot, depth=depth, rot=H.rz(rollv))
        x -= cw * (len(w_) + 1)


def aquarium(run=False):
    g = make_group('Village_Aquarium' + ('_Rundown' if run else ''), 'Dr. Hollis\'s aquarium (glass frontage, painted false front, interior with three tank platforms).' if not run else
                   'Dr. Hollis\'s aquarium, run-down: frontage boarded, sign faded, the mascot fish on the ground.', rays=12, ao_dist=0.9)
    gg = None if run else make_group('Village_Aquarium_Glass', 'Glass of the aquarium frontage (bay panes and the door pane): put the glass material on this object.', rays=0, lmap={})
    r = vrng('aquarium' + ('r' if run else ''))
    f0 = Fr()
    fb = Fr((0, 0, -1.2))
    W, D = 10.2, 5.0
    trim = S2['PineDark'] if not run else S['WoodDark']
    fz = D / 2
    pad(g, f0, 10.8, 7.8)
    g.mark()
    bx(g, fb, (0, 0.1, 0), (W + 0.3, 0.42, D + 0.3), S['Rock'], L['StoneWall'], 0.55, r=0.05)
    # walls: back, west, east; the front is a sill, mullions, a lintel and the false front
    wall_y, wall_h = 0.31 + 1.55, 3.1
    bx(g, fb, (0, wall_y, -D / 2 + 0.125), (W, wall_h, 0.25), S['Sand'], L['Sand'], H.AO_WOOD, r=0.03)
    for sx in (-1, 1):
        bx(g, fb, (sx * (W / 2 - 0.125), wall_y, 0), (0.25, wall_h, D), S['Sand'], L['Sand'], H.AO_WOOD, r=0.03)
        put(g, fb, H.prism([(-D / 2, 0.0), (D / 2, 0.0), (0.0, 0.8)], 0.25, 0.03, segs=1), (sx * (W / 2 - 0.125), 3.41, 0), S['Sand'], L['Sand'], H.AO_WOOD)
        cb(g, fb, (sx * (W / 2 - 0.0), 0.31 + 1.55, -D / 2 + 0.05), (0.18, wall_h, 0.18), trim, L['Wood'], 0.45)
    for sx in (-1, 1):
        for xx in (sx * (W / 4 + 0.325),):
            bx(g, fb, (xx, 0.31 + 0.275, fz - 0.14), (W / 2 - 0.65, 0.55, 0.28), S['Rock'], L['StoneWall'], 0.55, r=0.03)
    bx(g, fb, (0, 0.33, fz - 0.14), (1.3, 0.04, 0.4), S['Rock'], L['StoneWall'], 0.55, r=0.02)                 # the threshold
    xs = [-5.0, -3.55, -2.1, -0.65, 0.65, 2.1, 3.55, 5.0]
    for xp in xs:
        bx(g, fb, (xp, 0.86 + 1.1, fz - 0.1), (0.14, 2.3, 0.2), trim, L['Wood'], 0.45, r=0.02)
    bx(g, fb, (0, 3.25, fz - 0.12), (W, 0.3, 0.3), trim, L['Wood'], 0.5, r=0.03)
    # roof: low-pitched, ridge along x; the front slab stops behind the false front
    eave, ridge = 3.4, 4.2
    ang = math.degrees(math.atan2(ridge - eave, D / 2))
    for sz, run_ in ((-1, D / 2 + 0.2), (1, D / 2 - 0.1)):
        slab = run_ / math.cos(math.radians(ang)) + 0.05
        mid = (0, ridge - (ridge - eave) * (run_ / 2) / (D / 2) + 0.08, sz * run_ / 2)
        bx(g, fb, mid, (W + 0.5, 0.13, slab), S['Rock'], L['RoofTile'], 0.5, rot=H.rx(ang * sz), r=0.05)
    bx(g, fb, (0, ridge + 0.1, 0), (W + 0.5, 0.14, 0.26), S['Rock'], L['Wood'], 0.5, r=0.05)
    # the painted false front: an arched board, a frame, the lettering
    board = [(-4.7, 0.0), (4.7, 0.0), (4.7, 1.15), (3.4, 1.65), (0.0, 1.95), (-3.4, 1.65), (-4.7, 1.15)]
    put(g, fb, H.prism(board, 0.16, 0.02, segs=1), (0, 3.4, fz), S2['PineDark'] if not run else S['Dock'], L['Wood'], 0.5, rot=H.ry(90))
    for k in range(len(board) - 1):
        (xa, ya), (xb, yb) = board[k], board[k + 1]
        if k in (0,):
            continue
        L_ = math.hypot(xb - xa, yb - ya)
        cb(g, fb, ((xa + xb) / 2, 3.4 + (ya + yb) / 2, fz + 0.09), (L_, 0.1, 0.03), S['Sand'] if not run else S['Wood'], L['Wood'], 0.5,
           rot=H.rz(math.degrees(math.atan2(yb - ya, xb - xa))))
    txt = S2['Snow'] if not run else S['Sand']
    painted_line(g, fb, "DR. HOLLIS'S", 0.62, (0.0, 3.4 + 1.18, fz + 0.1), txt, r)
    painted_line(g, fb, 'AQUARIUM', 0.7, (0.0, 3.4 + 0.58, fz + 0.1), txt, r)
    if not run:
        painted_line(g, fb, '(INSTITUTE PENDING)', 0.17, (0.0, 3.4 + 0.2, fz + 0.1), S['Accent'], r, 0.01, 0.008)
    # the mascot: a big wooden fish on the roof, a tail and an eye
    if not run:
        mf = Fr((3.7, 5.55, fz - 0.1), -10, par=fb)
        put(g, mf, H.blob(0.7, r, (1.5, 0.75, 0.22), subdiv=2, rough=0.05, flat=-2), (0, 0, 0), S['Accent'], L['none'], 0.6)
        put(g, mf, H.blob(0.32, r, (0.55, 1.0, 0.18), subdiv=1, rough=0.05, flat=-2), (-0.95, 0.0, 0), S['Accent'], L['none'], 0.6)
        put(g, mf, H.blob(0.08, r, (1, 1, 0.7), subdiv=1, rough=0.0, flat=-2), (0.55, 0.12, 0.18), S2['Snow'], L['none'], 0.7)
        cb(g, mf, (0.0, -0.55, 0.0), (0.1, 0.7, 0.1), S['WoodDark'], L['Wood'], 0.5)
    # a cupola with a weathervane on the ridge
    lt(g, fb, (-3.4, ridge + 0.1, -0.4), [(0.0, 0.5), (0.9, 0.5), (0.95, 0.62), (1.05, 0.62)], S['Sand'], L['Plaster'], 0.55, 8)
    lt(g, fb, (-3.4, ridge + 1.15, -0.4), [(0.0, 0.7), (0.5, 0.0)], S['Rock'], L['RoofTile'], 0.55, 8)
    lt(g, fb, (-3.4, ridge + 1.6, -0.4), [(0.0, 0.015), (0.4, 0.0)], S['WoodDark'], L['none'], 0.5, 4)
    if not run:
        # ---- the door leaf, standing open (hinged at x -0.6, swung 85 deg inward), and the glass
        leaf = Fr((-0.6, 0.0, fz - 0.1), 85, par=fb)
        bx(g, leaf, (0.6, 1.4, 0), (1.18, 2.1, 0.07), trim, L['Wood'], 0.5, r=0.02)
        cb(gg, leaf, (0.6, 1.5, 0.0), (0.92, 1.7, 0.03), S2['Snow'], L['none'], 0.9)
        # panes in every bay
        for k in range(3):
            for base in (-5.0, 0.65):
                x0 = base + 1.45 * k
                cb(gg, fb, (x0 + 0.725, 0.86 + 1.075, fz - 0.1), (1.31, 2.15, 0.03), S2['Snow'], L['none'], 0.9)
        # ---- interior: back panel, tank platforms, ceiling, a counter, shelves with jars, a hanging fish, lights, a runner rug
        cb(g, fb, (0, 0.31 + 1.35, -D / 2 + 0.27), (W - 0.5, 2.7, 0.04), S2['PineDark'], L['Plaster'], 0.55)
        for xx in (-3.6, 0.0, 3.6):
            bx(g, fb, (xx, 0.31 + 0.06, -1.9), (2.9, 0.12, 1.3), S['Rock'], L['StoneWall'], 0.55, r=0.03)
        bx(g, fb, (4.4, 0.31 + 0.06, 0.2), (1.0, 0.12, 2.1), S['Rock'], L['StoneWall'], 0.55, r=0.03)
        cb(g, fb, (0, 0.33, 0.6), (1.1, 0.015, 3.6), S['Roof'], L['Cloth'], 0.6)
        bx(g, fb, (0, 3.2, 0), (W - 0.4, 0.12, D - 0.4), S['WoodDark'], L['Wood'], 0.5, r=0.03)
        for zz in (-1.5, 0.0, 1.5):
            cb(g, fb, (0, 3.1, zz), (W - 0.4, 0.16, 0.2), S['Wood'], L['Wood'], 0.5)
        bx(g, fb, (-2.6, 0.31 + 0.5, 1.2), (2.0, 1.0, 0.65), S['Wood'], L['Wood'], 0.5, r=0.03)
        bx(g, fb, (-2.6, 0.31 + 1.03, 1.2), (2.1, 0.06, 0.75), S['Dock'], L['Wood'], 0.5, r=0.02)
        bx(g, fb, (-3.1, 0.31 + 1.2, 1.2), (0.35, 0.3, 0.3), S['Rock'], L['none'], 0.5, r=0.04)
        lt(g, fb, (-2.2, 0.31 + 1.06, 1.15), [(0.0, 0.07), (0.05, 0.05), (0.09, 0.0)], S['Accent'], L['none'], 0.6, 8)
        for k, yy in enumerate((0.9, 1.5, 2.1)):
            bx(g, fb, (-4.8, yy, 0.6), (0.3, 0.05, 3.0), S['Wood'], L['Wood'], 0.5, r=0.015)
            for j in range(7):
                lt(g, fb, (-4.8, yy + 0.025, -0.65 + 0.4 * j + 0.05 * (k % 2)), [(0.0, 0.07), (0.01, 0.08), (0.24, 0.08), (0.26, 0.0)],
                   [S['Reed'], S['Sand'], S2['PineLight']][(j + k) % 3], L['none'], 0.6, 7)
        # the hanging fish skeleton: a spine and eleven ribs
        spine = [(-1.4 + 0.28 * i, 2.55 + 0.03 * math.sin(i * 0.7), 0.2) for i in range(11)]
        tb(g, fb, spine, 0.025, S2['Snow'], L['none'], 0.7, 4)
        for i, (x, y, z) in enumerate(spine[1:-1]):
            hh = 0.3 * math.sin(math.pi * (i + 1) / 10) + 0.08
            tb(g, fb, [(x, y - hh, z), (x, y, z), (x, y + hh, z)], 0.012, S2['Snow'], L['none'], 0.7, 3)
        put(g, fb, H.blob(0.16, r, (1.2, 1.0, 0.9), subdiv=1, rough=0.05, flat=-2), (1.55, 2.58, 0.2), S2['Snow'], L['none'], 0.7)
        tb(g, fb, [(-1.4, 2.6, 0.2), (-1.4, 3.1, 0.2)], 0.01, S['Sand'], L['Cloth'], 0.5, 3)
        tb(g, fb, [(1.0, 2.6, 0.2), (1.0, 3.1, 0.2)], 0.01, S['Sand'], L['Cloth'], 0.5, 3)
        pa, pb = fb.P(-4.6, 3.0, 2.2), fb.P(4.6, 3.0, 2.2)
        g.add(H.tube(sag_pts(pa, pb, 0.25, 16), 0.012, 3, caps=False), S['WoodDark'], L['none'], 0.5)
        for k in range(14):
            t = (k + 0.5) / 14
            pp = Vector(pa).lerp(Vector(pb), t) - Vector((0, 0.25 * 4 * t * (1 - t) + 0.05, 0))
            put(g, Fr(), H.blob(0.045, r, (1, 1.2, 1), subdiv=1, rough=0.02, flat=-2), (pp.x, pp.y, pp.z), S['Accent'], L['none'], 0.8)
        # ---- forecourt: planters at the door, an A-board, a bench; the touch-pool spot is reserved on the east side
        for sx in (-1.15, 1.15):
            g.mark()
            lt(g, f0, (sx, 0.0, 1.7), [(0.0, 0.2), (0.02, 0.24), (0.35, 0.26), (0.45, 0.25), (0.46, 0.2)], S['WoodDark'], L['Wood'], 0.5, 10, cap0=True)
            bl(g, f0, (sx, 0.62, 1.7), 0.3, r, S['Foliage'], L['Foliage'], (1.0, 0.9, 1.0), 2, 0.25)
            g.solid('Planter_%d' % int(sx > 0))
        g.mark()
        cf = Fr((-2.3, 0.0, 2.6), 15, par=f0)
        for s_ in (-1, 1):
            cb(g, cf, (s_ * 0.3, 0.45, 0.0), (0.05, 0.95, 0.05), S['WoodDark'], L['Wood'], 0.5, rot=H.rx(-8))
        bx(g, cf, (0, 0.62, 0.08), (0.7, 0.9, 0.05), S['WoodDark'], L['Wood'], 0.5, rot=H.rx(-10), r=0.02)
        bx(g, cf, (0, 0.62, 0.105), (0.6, 0.8, 0.02), S2['PineDark'], L['none'], 0.5, rot=H.rx(-10), r=0.01)
        for yy, tx, sz_ in ((0.87, 'PLEASE DO NOT', 0.075), (0.66, 'TAP THE GLASS', 0.075), (0.45, '(THEY KNOW)', 0.06)):
            label(g, cf, tx, sz_, (0.0, yy, 0.13), S2['Snow'], depth=0.006, rot=H.rx(-10))
        g.solid('ABoard')
        H.bench(g, 'ForecourtBench', -4.2, 3.0, 180.0, r, y=PAD_TOP)
        g.extra['smoke_points'] = []
        # ---- colliders: the hall's walls as boxes (a 1.3 m doorway stays open), the roof, the false front
        zb = fb.P(0, 0, 0).z
        for nm, lo, hi in (('WallBack', (-5.1, 0.3, zb - D / 2), (5.1, 3.4, zb - D / 2 + 0.25)),
                           ('WallWest', (-5.1, 0.3, zb - D / 2), (-4.85, 3.4, zb + D / 2)),
                           ('WallEast', (4.85, 0.3, zb - D / 2), (5.1, 3.4, zb + D / 2)),
                           ('FrontWest', (-5.1, 0.3, zb + D / 2 - 0.3), (-0.65, 3.4, zb + D / 2)),
                           ('FrontEast', (0.65, 0.3, zb + D / 2 - 0.3), (5.1, 3.4, zb + D / 2)),
                           ('RoofAndFalseFront', (-5.3, 3.4, zb - D / 2 - 0.4), (5.3, 5.35, zb + D / 2 + 0.1))):
            H.box_item(g, nm, lo, hi, role='wall' if nm.startswith('Wall') or nm.startswith('Front') else 'roof (not walkable)')
    else:
        # run-down: plywood over every bay and the doorway, a tarp on the roof, a fallen mascot and weeds
        for k in range(3):
            for base in (-5.0, 0.65):
                x0 = base + 1.45 * k + 0.725
                for j in range(2):
                    cb(g, fb, (x0, 0.86 + 0.55 + 1.1 * j, fz - 0.02), (1.38, 1.05, 0.05), S['Wood'] if (j + k) % 2 else S['Dock'], L['Wood'], 0.5,
                       rot=H.rz(r.uniform(-2, 2)))
        for j in range(3):
            cb(g, fb, (0.0, 0.5 + 0.7 * j, fz - 0.02), (1.4, 0.55, 0.06), S['WoodDark'], L['Wood'], 0.5, rot=H.rz(r.uniform(-4, 4)))
        cb(g, fb, (0.0, 1.4, fz + 0.02), (0.14, 2.3, 0.04), S['Wood'], L['Wood'], 0.5, rot=H.rz(30))
        tarp(g, fb, (2.4, ridge - (ridge - eave) * 0.5 + 0.08, -1.2), (3.0, 0.03, 1.8), r, H.rx(-ang))
        for k in range(5):
            bl(g, fb, (r.uniform(-4.6, 4.6), 0.4, fz + 0.05), r.uniform(0.2, 0.35), r, S['FoliageDark'], L['Foliage'], (1.4, 0.4, 0.5), 1, 0.4, 0.5)
        mf = Fr((3.2, 0.3, 2.9), 160, par=f0)
        put(g, mf, H.blob(0.7, r, (1.5, 0.75, 0.22), subdiv=2, rough=0.05, flat=-2), (0, 0.2, 0), S['Accent'], L['none'], 0.6, rot=H.rz(8))
        put(g, mf, H.blob(0.32, r, (0.55, 1.0, 0.18), subdiv=1, rough=0.05, flat=-2), (-0.95, 0.2, 0), S['Accent'], L['none'], 0.6)
        label(g, fb, 'CLOSED (FISH ESCAPED)', 0.13, (0.0, 3.4 + 0.2, fz + 0.1), S['Roof'], depth=0.01, rot=H.rz(-3))
        weeds(g, f0, r, [(-4.6, 2.8), (-2.0, 3.2), (1.5, 3.4), (4.4, 2.6), (-4.8, -1.0), (4.8, -2.0)], 5, 0.7)
        body_collider(g, 'Hall')
    return ([g, gg] if gg else [g]), dict(role='aquarium', lot_class='L', door_offset_x=0.0, variant='Aquarium', rundown=run)


# ---------------------------------------------------------------------------------------------------------------------------------------
# Lot marker: survey stakes at the corners (and the midpoints of long sides), a sagging rope, a hi-vis ribbon on every stake and the sign
# "LOT AVAILABLE. ENQUIRE WITHIN. DON'T." One model per lot class; origin = the lot centre on the ground, front +z (the side the sign faces).
# ---------------------------------------------------------------------------------------------------------------------------------------
LOT_CLASSES = {'S': (9.0, 6.0), 'M': (9.6, 6.4), 'L': (14.0, 8.0)}      # (width x, depth z) in the lot's own frame, metres


def lot_marker(cls):
    w, d = LOT_CLASSES[cls]
    g = make_group('Village_LotMarker_' + cls, 'Lot marker for a %.1f x %.1f m lot: stakes, rope, ribbons and the sign. Origin = lot centre at ground level.' % (w, d),
                   rays=8, ao_dist=0.5, lmap={})
    r = vrng('lotmarker.' + cls)
    f = Fr()
    ix, iz = w / 2 - 0.15, d / 2 - 0.15
    corners = [(-ix, -iz), (ix, -iz), (ix, iz), (-ix, iz)]
    stakes = list(corners)
    pts_h = 0.8
    for a, b in zip(corners, corners[1:] + corners[:1]):
        L_ = math.hypot(b[0] - a[0], b[1] - a[1])
        if L_ > 7.0:
            stakes.append(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
    for k, (x, z) in enumerate(stakes):
        tall = (x, z) in corners
        hh = 1.15 if tall else 0.85
        g.mark()
        lt(g, f, (x, -0.3, z), [(0.0, 0.0), (0.12, 0.035), (0.3 + hh, 0.03), (0.3 + hh + 0.02, 0.0)], S['Wood'], L['Wood'], 0.5, 5, rot=H.rz(r.uniform(-2, 2)))
        lt(g, f, (x, hh - 0.2, z), [(0.0, 0.034), (0.0, 0.05), (0.12, 0.05), (0.12, 0.034)], S['Accent'], L['Cloth'], 0.6, 6)
        if tall:
            cl = H.cloth_panel(0.3, 0.16, 3, 2, 0.03, r.uniform(0, 3))
            put(g, f, cl, (x + 0.03, hh - 0.02, z), S['Accent'], L['Cloth'], 0.6, rot=H.ry(r.uniform(0, 360)))
        g.solid('Stake_%d' % k, collider='none (a trigger-free visual; lots are not blocked)')
    ring = corners + [corners[0]]
    for a, b in zip(ring[:-1], ring[1:]):
        mids = [s_ for s_ in stakes if s_ not in corners and abs((s_[0] - a[0]) * (b[1] - a[1]) - (s_[1] - a[1]) * (b[0] - a[0])) < 1e-6
                and min(a[0], b[0]) - 1e-6 <= s_[0] <= max(a[0], b[0]) + 1e-6 and min(a[1], b[1]) - 1e-6 <= s_[1] <= max(a[1], b[1]) + 1e-6]
        seq = [a] + mids + [b]
        for p, q in zip(seq[:-1], seq[1:]):
            g.add(H.tube(sag_pts((p[0], pts_h, p[1]), (q[0], pts_h, q[1]), 0.1, 10), 0.012, 4, caps=False), S['Sand'], L['Cloth'], 0.55)
    # the sign: two posts and a board on the front side, left of centre, facing +z
    sx, sz = -w / 2 + 1.3, d / 2 - 0.4
    sf = Fr((sx, 0.0, sz), 0, par=f)
    for dx in (-0.55, 0.55):
        lt(g, sf, (dx, -0.15, 0), [(0.0, 0.04), (1.55, 0.038), (1.57, 0.0)], S['WoodDark'], L['Wood'], 0.5, 6)
    bx(g, sf, (0, 1.2, 0.0), (1.2, 0.74, 0.05), S2['Snow'], L['Wood'], 0.55, r=0.02)
    bx(g, sf, (0, 1.2, -0.005), (1.26, 0.8, 0.04), S['WoodDark'], L['Wood'], 0.5, r=0.02)
    label(g, sf, 'LOT AVAILABLE.', 0.105, (0.0, 1.42, 0.03), S['Roof'], depth=0.006)
    label(g, sf, 'ENQUIRE WITHIN.', 0.105, (0.0, 1.22, 0.03), S['Roof'], depth=0.006)
    label(g, sf, "DON'T.", 0.17, (0.0, 0.98, 0.03), S['Roof'], depth=0.006)
    g.solid('Sign')
    return g


# ---------------------------------------------------------------------------------------------------------------------------------------
# The village lots (design 3.2, 5.3). Unity metres, y up; each lot is an axis-aligned rectangle (yaw 0 or 180) of the class size below.
# `yaw` is the default facing (the building's front +z after the yaw); the lot allows yaw and yaw + 180. A building's origin sits at the
# lot centre, at ground height.
# ---------------------------------------------------------------------------------------------------------------------------------------
LOTS = [
    dict(id='Lot1', name='Upper lane west', zone='level 1, the upper lane, north row', centre=(-54.0, -82.0), yaw=180.0, cls='S', replaces=0,
         notes='West end of the lane, beside stair B. Far end from Lot4: the Garys\' "as far apart as the lots allow" pair.'),
    dict(id='Lot2', name='Upper lane west-centre', zone='level 1, the upper lane, north row', centre=(-17.5, -79.7), yaw=180.0, cls='S', replaces=2,
         notes='West of the market stalls and stair A.'),
    dict(id='Lot3', name='Upper lane east-centre', zone='level 1, the upper lane, north row', centre=(23.8, -79.5), yaw=180.0, cls='S', replaces=7,
         notes='Between the square-side lot and stair D.'),
    dict(id='Lot4', name='Upper lane east', zone='level 1, the upper lane, north row', centre=(47.4, -81.8), yaw=180.0, cls='S', replaces=9,
         notes='East end of the lane, past stair D (the cottage stood at x 46.6; the lot is 0.8 m east to clear cottage 8\'s garden fence).'),
    dict(id='Lot5', name='Overlook west', zone='level 2, the overlook terrace, west of the chapel', centre=(-4.2, -101.2), yaw=0.0, cls='M', removes_tree=14,
         notes='Faces the harbour over the terrace edge. Next to the chapel (its nave starts at x 1.15): the cafe\'s "next to the chapel" wish.'),
    dict(id='Lot6', name='Overlook east', zone='level 2, the overlook terrace, east of the chapel', centre=(15.5, -101.2), yaw=0.0, cls='M', removes_tree=15,
         notes='Faces the harbour; 11 m east of the chapel; the telescope and the east bench are on the terrace edge in front of it.'),
    dict(id='Lot7', name='Square lot', zone='level 1, east of stair A, 11 m from the village square', centre=(12.8, -79.3), yaw=180.0, cls='S',
         notes='The nearest free ground to the square (the square, its tree and the market fill the west side). Stair A lands beside it.'),
    dict(id='Lot8', name='Beach lot', zone='level 0, the plateau (y 1.2), behind the east beach', centre=(47.0, -59.0), yaw=0.0, cls='L',
         notes='The aquarium\'s lot (Dr. Hollis: "somewhere children can\'t tap the glass"): off every path and cast lane, behind the crew berth.'),
]
REMOVED_COTTAGES = [lot['replaces'] for lot in LOTS if 'replaces' in lot]
REMOVED_TREES = ['Tree2_%d' % lot['removes_tree'] for lot in LOTS if 'removes_tree' in lot]     # TREES2[14], [15]: the two shade trees on the overlook


def lot_rect(lot):
    w, d = LOT_CLASSES[lot['cls']]
    cx, cz = lot['centre']
    return (cx - w / 2, cx + w / 2, cz - d / 2, cz + d / 2)


def rect_gap(a, b):
    dx = max(a[0] - b[1], b[0] - a[1], 0.0)
    dz = max(a[2] - b[3], b[2] - a[3], 0.0)
    return math.hypot(dx, dz)


def frange(a, b, step):
    n = int(round((b - a) / step))
    return [a + (b - a) * i / max(n, 1) for i in range(n + 1)]


def removed_item(name):
    if name in REMOVED_TREES:
        return True
    for i in REMOVED_COTTAGES:
        if name == 'Cottage_%d' % i or name.startswith('GardenFence_%d' % i) or name.startswith('GardenFenceSide_%d' % i):
            return True
    return False


def analyse_lots(groups_all):
    # Per lot: ground heights over the rectangle, clearances to every solid item of the harbour (minus the replaced cottages and their fences),
    # to stand points / cast lanes, the paths, the lane (road), the stairs, the code trees and the other lots.
    solids = []
    for g in groups_all:
        for it in g.items:
            if 'bounds_min' in it and not removed_item(it['name']):
                solids.append((g.name, it['name'], it['bounds_min'], it['bounds_max']))
    out = {}
    for lot in LOTS:
        rc = lot_rect(lot)
        xs, zs = frange(rc[0], rc[1], 0.5), frange(rc[2], rc[3], 0.5)
        ys = [H.land_y(x, z) for x in xs for z in zs]
        cy = H.land_y(*lot['centre'])
        info = dict(ground_y_centre=round(cy, 3), ground_y_min=round(min(ys), 3), ground_y_max=round(max(ys), 3), flat_within_m=round(max(ys) - min(ys), 3))
        gap = 99.0
        near = None
        for gn, nm, lo, hi in solids:
            if hi[1] < cy - 0.5 or lo[1] > cy + 4.5:
                continue
            gp = rect_gap(rc, (lo[0], hi[0], lo[2], hi[2]))
            if gp < gap:
                gap, near = gp, '%s/%s' % (gn, nm)
        info['min_gap_to_solid_m'] = round(gap, 2)
        info['nearest_solid'] = near
        info['overlapping_solids'] = sorted(set('%s/%s' % (gn, nm) for gn, nm, lo, hi in solids if rect_gap(rc, (lo[0], hi[0], lo[2], hi[2])) <= 0.0
                                                 and not (hi[1] < cy - 0.5 or lo[1] > cy + 4.5)))
        pts = [(x, z) for x in xs for z in zs]
        info['clear_of_stand_circles_and_cast_lanes_1m'] = not any(H.lane_hit(x, z, 1.0) for x, z in pts)
        info['stand_radius_STAND_R_m'] = H.STAND_R
        info['min_dist_to_any_stand_point_m'] = round(min(math.hypot(x - sp[0], z - sp[2]) for x, z in pts for sp in H.STAND_POINTS + H.STAND_POINTS_R1), 1)
        info['min_dist_to_path_edge_m'] = round(min(H.poly_dist(x, z, pp) - hw for x, z in pts for pp, hw in H.PATHS), 2)
        info['min_dist_to_lane_edge_m'] = round(min(H.road_dist(x, z) - 2.35 for x, z in pts), 2) if cy > 3.0 and abs(lot['centre'][1] + 88) < 15 else None
        info['stairs_clear_0.5m'] = not any(H.in_ramp(x, z, 0.5) for x, z in pts)
        info['code_trees_min_dist_m'] = round(min(math.hypot(x - tx, z - tz) for x, z in pts for tx, tz, _, _ in H.CODE_TREES), 1)
        info['pier_hit'] = any(H.pier_hit(x, z, 1.0) for x, z in pts)
        info['other_lots_min_gap_m'] = round(min(rect_gap(rc, lot_rect(o)) for o in LOTS if o is not lot), 2)
        rem = []
        for g in groups_all:
            for it in g.items:
                nm = it['name']
                if 'replaces' in lot and (nm == 'Cottage_%d' % lot['replaces'] or nm.startswith('GardenFence_%d' % lot['replaces']) or nm.startswith('GardenFenceSide_%d' % lot['replaces'])):
                    rem.append('%s/%s' % (g.name, nm))
                if 'removes_tree' in lot and nm == 'Tree2_%d' % lot['removes_tree']:
                    rem.append('%s/%s' % (g.name, nm))
        info['harbour_items_to_remove'] = sorted(rem)
        out[lot['id']] = info
    return out


def lots_manifest(analysis):
    out = []
    for lot in LOTS:
        w, d = LOT_CLASSES[lot['cls']]
        cx, cz = lot['centre']
        a = analysis[lot['id']]
        e = dict(id=lot['id'], name=lot['name'], zone=lot['zone'], lot_class=lot['cls'], centre=[cx, a['ground_y_centre'], cz], yaw_deg=lot['yaw'],
                 size=dict(width_x=w, depth_z=d, note='metres, in the lot frame: x across, z = front (the door side at yaw)'),
                 rect_world=dict(x0=round(cx - w / 2, 3), x1=round(cx + w / 2, 3), z0=round(cz - d / 2, 3), z1=round(cz + d / 2, 3)),
                 ground=dict(y_centre=a['ground_y_centre'], y_min=a['ground_y_min'], y_max=a['ground_y_max'], flat_within_m=a['flat_within_m']),
                 allowed_yaws_deg=[lot['yaw'], (lot['yaw'] + 180.0) % 360.0], accepts_building_classes=[c for c in 'SML' if 'SML'.index(c) <= 'SML'.index(lot['cls'])],
                 marker_model='Village_LotMarker_' + lot['cls'], marker_origin='lot centre at ground height, yaw = the lot yaw', notes=lot['notes'], clearances=a)
        if 'replaces' in lot:
            e['replaces_upper_house_index'] = lot['replaces']
            e['replaces_upper_house'] = list(H.UPPER_HOUSES[lot['replaces']][:4]) + [H.UPPER_HOUSES[lot['replaces']][10]]
        out.append(e)
    return out


# ---------------------------------------------------------------------------------------------------------------------------------------
# Build everything, export, manifest
# ---------------------------------------------------------------------------------------------------------------------------------------
def build_all():
    models = []          # dicts: kind, groups (main first), info
    pairs = {'A': cottage_A, 'B': cottage_B, 'C': cottage_C, 'D': cottage_D}
    for key, fn in pairs.items():
        for run in (False, True):
            g, info = fn(run)
            models.append(dict(kind='building', groups=[g], info=info))
    for kind, run in (('tidy', False), ('chaos', False), ('x', True)):
        g, info = gary(kind, run)
        models.append(dict(kind='building', groups=[g], info=info))
    for run in (False, True):
        g, info = cafe(run)
        models.append(dict(kind='building', groups=[g], info=info))
    for run in (False, True):
        gs, info = aquarium(run)
        models.append(dict(kind='building', groups=gs, info=info))
    for fn in (display_tank, breeding_tank, touch_pool):
        gs = fn()
        models.append(dict(kind='prop', groups=gs, info=dict(role='aquarium prop')))
    for cls in LOT_CLASSES:
        models.append(dict(kind='lot_marker', groups=[lot_marker(cls)], info=dict(role='lot marker', lot_class=cls)))
    return models


def extent_xz(g):
    lo, hi = H.u_bounds(g)
    return lo, hi


def fits(lo, hi, cls):
    w, d = LOT_CLASSES[cls]
    # building origin at the lot centre, yaw 0 or 180 (a 180 flip mirrors the extents through the origin)
    ok0 = lo[0] >= -w / 2 and hi[0] <= w / 2 and lo[2] >= -d / 2 and hi[2] <= d / 2
    ok180 = -hi[0] >= -w / 2 and -lo[0] <= w / 2 and -hi[2] >= -d / 2 and -lo[2] <= d / 2
    return bool(ok0 and ok180)


def building_extra(m):
    g = m['groups'][0]
    lo, hi = extent_xz(g)
    info = m['info']
    d = dict(role=info['role'], variant=info['variant'], state='rundown' if info['rundown'] else 'built', lot_class_min=info['lot_class'],
             footprint=dict(extent_x=[lo[0], hi[0]], extent_z=[lo[2], hi[2]], width_x=round(hi[0] - lo[0], 2), depth_z=round(hi[2] - lo[2], 2),
                            origin='centre of the pad at ground level (y 0); the extents are the whole mesh at yaw 0 (roof overhangs, awnings, props included)',
                            door_side='+z (front)', door_offset_x=info['door_offset_x'], pad=g.extra.get('pad')),
             fits_lot_classes=[c for c in 'SML' if fits(lo, hi, c)],
             placement='origin on the lot centre, yaw = lot yaw (or lot yaw + 180); y = the lot ground height')
    for k in ('jumper_slots', 'trim_slot', 'door_slot'):
        if k in info:
            d[k] = info[k]
    return d


def write_manifest(models, analysis, report, export=True):
    man = dict(
        generator=SCRIPT_REL + ' (imports Art/Blender/fg_harbour.py)', area='VillageBuildings', branch='harbour-village-buildings (cut from harbour-art3)',
        axes='Unity metres. Authored in Unity axes; Blender = (-x, -z, y); FBX export as the harbour (-Z forward, Y up, bake_space_transform). Each FBX object is at '
             'the origin with an identity transform. Every model is a LOCAL model: building origin = centre of its footprint at ground level, front/door = +z.',
        encoding=dict(uv0='UVMap: metres', uv1='Layer: (palette slot, texture layer) on every corner (no uv1.z in the FBX: the material gives the mapping)',
                      colour='Col: 0.5 x baked AO (per-vertex ray-cast bake per model, 1 m)', normals='smooth / analytic rounded-box', materials='none exported'),
        slots=S, slots2=S2, layers=L, material_default='M_Toon_Base (every model; no triplanar objects here)',
        glass_and_water='*_Glass objects want a transparent glass material, *_Water objects a water material (the toon shader is meant for everything else). '
                        'A tank is three FBX files with the same origin: Village_X (body), Village_X_Glass, Village_X_Water; place all three at the same transform.',
        total_triangles=sum(g.tris() for m in models for g in m['groups']),
        buildings=[], props=[], lot_markers=[], lots=lots_manifest(analysis), fbx=[])
    for m in models:
        for i, g in enumerate(m['groups']):
            ex = {}
            if m['kind'] == 'building' and i == 0:
                ex = building_extra(m)
                if len(m['groups']) > 1:
                    ex['companions'] = [x.name for x in m['groups'][1:]]
                    ex['companion_note'] = 'same origin; place with the building. Glass: transparent glass material.'
            elif m['kind'] == 'building':
                ex = dict(role='glass companion of ' + m['groups'][0].name, material='glass (not M_Toon_Base)')
            elif m['kind'] == 'prop':
                ex = dict(role='aquarium prop' if i == 0 else ('glass companion' if g.name.endswith('_Glass') else 'water companion'),
                          material='M_Toon_Base' if i == 0 else ('glass (not M_Toon_Base)' if g.name.endswith('_Glass') else 'water (not M_Toon_Base)'))
            else:
                ex = dict(placements=[dict(lot=lot['id'], position=[lot['centre'][0], analysis[lot['id']]['ground_y_centre'], lot['centre'][1]], yaw_deg=lot['yaw'])
                                      for lot in LOTS if lot['cls'] == m['info']['lot_class']], role='lot marker', lot_class=m['info']['lot_class'], lot_size=dict(width_x=LOT_CLASSES[m['info']['lot_class']][0], depth_z=LOT_CLASSES[m['info']['lot_class']][1]),
                          placement='origin on the lot centre at ground height, yaw = the lot yaw; stakes reach 0.3 m below ground, so the lot must be flat within ~0.1 m')
            e = entry(g, ex)
            if g.name.endswith(('_Glass', '_Water')):
                e['material'] = 'glass' if g.name.endswith('_Glass') else 'water'
            man['fbx'].append(e)
            (man['buildings'] if m['kind'] == 'building' else man['props'] if m['kind'] == 'prop' else man['lot_markers']).append(e)
    man['report'] = report
    return man


def aquarium_layout_extra(models):
    # tank mounts in the aquarium's frame (building origin = pad centre): the display tanks stand on the three back platforms, the breeding tank on the
    # east platform, the touch pool in the forecourt
    for m in models:
        if m['groups'][0].name == 'Village_Aquarium':
            g = m['groups'][0]
            g.extra['tank_mounts'] = [
                dict(name='DisplayTank_0', prefab='Village_DisplayTank (+_Glass, +_Water)', position=[-3.6, 0.43, -3.1], yaw_deg=0.0),
                dict(name='DisplayTank_1', prefab='Village_DisplayTank (+_Glass, +_Water)', position=[0.0, 0.43, -3.1], yaw_deg=0.0),
                dict(name='DisplayTank_2', prefab='Village_DisplayTank (+_Glass, +_Water)', position=[3.6, 0.43, -3.1], yaw_deg=0.0),
                dict(name='BreedingTank', prefab='Village_BreedingTank (+_Glass, +_Water)', position=[4.4, 0.43, -1.0], yaw_deg=270.0,
                     note='yaw 270: the tank front faces west into the hall; footprint 1.9 x 0.9 turned onto the 1.0 x 2.1 east platform'),
                dict(name='TouchPool', prefab='Village_TouchPool (+_Water)', position=[3.2, 0.03, 2.55], yaw_deg=0.0,
                     note='outdoors in the forecourt, east of the door; 3.0 x 1.8 m, the sign post stands 0.5 m east of it')]
            g.extra['interior_floor_y'] = 0.31
            g.extra['doorway'] = dict(x0=-0.65, x1=0.65, y1=3.1, note='open 1.3 m gap in the frontage; the door leaf stands open inside (mesh), glass in Village_Aquarium_Glass')


def main(export=True, previews=True, verify=True):
    bpy.ops.wm.read_homefile(use_empty=True)
    models = build_all()
    aquarium_layout_extra(models)
    groups = [g for m in models for g in m['groups']]
    for g in groups:
        if g.tris() > H.MAX_TRIS or len(g.V) >= 65535:
            raise RuntimeError('%s: %d tris, %d verts' % (g.name, g.tris(), len(g.V)))
    objs = [to_object(g) for g in groups]
    bake(groups, objs)
    os.makedirs(EXPORT_DIR, exist_ok=True)
    report = []
    for ob, g in zip(objs, groups):
        status = H.export_if_changed(ob, os.path.join(EXPORT_DIR, g.name + '.fbx')) if export else 'skipped'
        report.append('%s tris=%d verts=%d %s' % (g.name, g.tris(), len(g.V), status))
    if verify:
        groups_all = H.build_pass2() + H.build_groups()
        analysis = analyse_lots(groups_all)
    else:
        analysis = {lot['id']: dict(ground_y_centre=H.land_y(*lot['centre']), ground_y_min=0, ground_y_max=0, flat_within_m=0) for lot in LOTS}
    man = write_manifest(models, analysis, report)
    with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
        json.dump(man, fh, indent=1)
    print('\n'.join(report))
    print('TOTAL %d' % man['total_triangles'])
    for lot in (man['lots'] if verify else []):
        c = lot['clearances']
        print(lot['id'], lot['centre'], 'flat', lot['ground']['flat_within_m'], 'gap', c['min_gap_to_solid_m'], c['nearest_solid'], 'overlap', c['overlapping_solids'],
              'lanes_ok', c['clear_of_stand_circles_and_cast_lanes_1m'], 'stairs_ok', c['stairs_clear_0.5m'], 'trees', c['code_trees_min_dist_m'])
    if previews:
        make_previews(models, objs, groups)
    return models, man


def show_only(objs, names, hide_others=True):
    for ob in objs:
        ob.hide_render = ob.name not in names
        ob.hide_viewport = ob.hide_render


def row_positions(widths, gap, centre_x=0.0):
    # x positions (decreasing: Unity +x runs to the LEFT of a camera looking down -z) for a left-to-right row of the given widths
    total = sum(widths) + gap * (len(widths) - 1)
    x = centre_x + total / 2
    out = []
    for w in widths:
        out.append(x - w / 2)
        x -= w + gap
    return out


def make_previews(models, objs, groups):
    by = {g.name: ob for ob, g in zip(objs, groups)}
    gby = {g.name: g for g in groups}
    preview_materials(list(zip(objs, groups)))
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    # ---- 1: the four cottages, built (front) and run-down (back)
    preview_scene((40.0, 30.0), (0.0, -6.0))
    show_only(objs, [])
    xs = [15.0 - 10.0 * i for i in range(4)]
    for x, v in zip(xs, 'ABCD'):
        for zz, suffix in ((0.0, ''), (-11.5, '_Rundown')):
            ob = by['Village_Cottage_%s%s' % (v, suffix)]
            ob.hide_render = False
            place(ob, (x, 0.0, zz), 0.0)
    render(os.path.join(PREVIEW_DIR, 'lineup_cottages.png'), (0.0, 15.0, 32.0), (0.0, 1.8, -5.0), lens=30)
    # ---- 2: the Garys, the cafe and the aquarium (front: built; back: run-down)
    preview_scene((60.0, 40.0), (0.0, -6.0))
    show_only(objs, [])
    row = ['Village_Gary_Tidy', 'Village_Gary_Chaos', 'Village_Cafe', 'Village_Aquarium']
    wid = [round(gby[n].extra['pad']['width_x'], 1) + 0.6 for n in row]
    xs = row_positions(wid, 1.2)
    for n, x in zip(row, xs):
        by[n].hide_render = False
        place(by[n], (x, 0.0, 0.0), 0.0)
        if n == 'Village_Aquarium':
            by['Village_Aquarium_Glass'].hide_render = False
            place(by['Village_Aquarium_Glass'], (x, 0.0, 0.0), 0.0)
    for n, x in (('Village_Gary_Rundown', xs[0] - 4.0), ('Village_Cafe_Rundown', xs[2]), ('Village_Aquarium_Rundown', xs[3])):
        by[n].hide_render = False
        place(by[n], (x, 0.0, -12.5), 0.0)
    render(os.path.join(PREVIEW_DIR, 'lineup_garys_cafe_aquarium.png'), (0.0, 22.0, 40.0), (0.0, 1.6, -4.0), lens=29)
    # ---- 3: the aquarium with its tanks on the mounts and the touch pool in the forecourt (glass and water previewed)
    preview_scene((24.0, 16.0), (0.0, 0.0))
    show_only(objs, [])
    mounts = gby['Village_Aquarium'].extra['tank_mounts']
    for n in ('Village_Aquarium', 'Village_Aquarium_Glass'):
        by[n].hide_render = False
        place(by[n], (0.0, 0.0, 0.0), 0.0)
    for mt in mounts:
        base = 'Village_DisplayTank' if mt['name'].startswith('Display') else 'Village_BreedingTank' if mt['name'].startswith('Breeding') else 'Village_TouchPool'
        for suffix in ('', '_Glass', '_Water'):
            nm = base + suffix
            if nm not in by:
                continue
            inst = bpy.data.objects.new('FGI_%s_%s' % (mt['name'], suffix), by[nm].data)
            H.collection('VillagePreview').objects.link(inst)
            place(inst, tuple(mt['position']), mt['yaw_deg'])
    render(os.path.join(PREVIEW_DIR, 'aquarium_with_tanks.png'), (-2.5, 3.4, 13.5), (0.2, 1.5, -1.4), lens=26)
    # ---- 4: the lots from above the square
    render_lots(objs, by, os.path.join(PREVIEW_DIR, 'lots_above_square.png'))


def terrain_object(x0, x1, z0, z1, step=1.0):
    xs, zs = frange(x0, x1, step), frange(z0, z1, step)
    verts = [to_b((x, H.land_y(x, z) - 0.04, z)) for z in zs for x in xs]
    nx = len(xs)
    faces = []
    for j in range(len(zs) - 1):
        for i in range(nx - 1):
            a = j * nx + i
            faces.append((a, a + 1, a + nx + 1, a + nx))
    me = bpy.data.meshes.new('FG_Village_Terrain')
    me.from_pydata(verts, [], faces)
    me.update()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(ground_material())
    ob = bpy.data.objects.new('FG_Village_Terrain', me)
    H.collection('VillagePreview').objects.link(ob)
    return ob


def render_lots(objs, by, path):
    preview_scene(ground=False)
    show_only(objs, [])
    terrain_object(-66.0, 66.0, -118.0, -46.0, 1.0)
    saved = list(H.UPPER_HOUSES)
    H.UPPER_HOUSES[:] = [h for i, h in enumerate(saved) if i not in REMOVED_COTTAGES]
    try:
        ug = H.build_upper_village()
    finally:
        H.UPPER_HOUSES[:] = saved
    extra = [H.build_road(), H.build_paths()] + H.build_stone_wall() + [H.build_buildings()]
    tm = H.toon_material()
    for g in ug + extra:
        ob = H.make_object(g)
        ob.data.materials.append(tm)
    mk = {c: by['Village_LotMarker_' + c] for c in LOT_CLASSES}
    tint = bpy.data.materials.new('FG_Village_LotTint')
    tint.use_nodes = True
    nt = tint.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL':
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = H.srgb_to_lin((1.0, 0.55, 0.15)) + (1,)
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    mix.inputs['Fac'].default_value = 0.3
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    try:
        tint.surface_render_method = 'BLENDED'
    except Exception:
        pass
    lbl = bpy.data.materials.new('FG_Village_LotLabel')
    lbl.use_nodes = True
    lnt = lbl.node_tree
    for n in list(lnt.nodes):
        if n.type != 'OUTPUT_MATERIAL':
            lnt.nodes.remove(n)
    lo_ = next(n for n in lnt.nodes if n.type == 'OUTPUT_MATERIAL')
    le = lnt.nodes.new('ShaderNodeEmission')
    le.inputs['Color'].default_value = H.srgb_to_lin((1.0, 0.95, 0.5)) + (1,)
    le.inputs['Strength'].default_value = 1.5
    lnt.links.new(le.outputs[0], lo_.inputs['Surface'])
    col = H.collection('VillagePreview')
    eye_u = (-3.0, 64.0, -58.0)
    for i, lot in enumerate(LOTS):
        cx, cz = lot['centre']
        cy = H.land_y(cx, cz)
        inst = bpy.data.objects.new('FGI_marker_%s' % lot['id'], mk[lot['cls']].data)
        col.objects.link(inst)
        place(inst, (cx, cy, cz), lot['yaw'])
        w, d = LOT_CLASSES[lot['cls']]
        me = bpy.data.meshes.new('FG_LotTint_%s' % lot['id'])
        me.from_pydata([to_b((cx - w / 2, cy + 0.05, cz - d / 2)), to_b((cx + w / 2, cy + 0.05, cz - d / 2)), to_b((cx + w / 2, cy + 0.05, cz + d / 2)),
                        to_b((cx - w / 2, cy + 0.05, cz + d / 2))], [], [(0, 1, 2, 3)])
        me.materials.append(tint)
        col.objects.link(bpy.data.objects.new('FG_LotTint_%s' % lot['id'], me))
        cu = bpy.data.curves.new('FG_LotNum_%s' % lot['id'], 'FONT')
        cu.body = str(i + 1)
        cu.size = 4.0
        cu.extrude = 0.15
        cu.align_x = 'CENTER'
        cu.align_y = 'CENTER'
        cu.materials.append(lbl)
        to = bpy.data.objects.new('FG_LotNum_%s' % lot['id'], cu)
        col.objects.link(to)
        to.location = to_b((cx, cy + 4.2, cz))
        to.rotation_euler = (H.to_blender(eye_u) - to.location).to_track_quat('Z', 'Y').to_euler()
    render(path, eye_u, (-2.0, 2.0, -86.0), lens=18)


if __name__ == '__main__':
    main(export=os.environ.get('FG_VB_EXPORT', '1') == '1', previews=os.environ.get('FG_VB_PREVIEW', '1') == '1', verify=os.environ.get('FG_VB_VERIFY', '1') == '1')
