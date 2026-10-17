# fg_berthkit.py (2026-10-17, branch harbour-berth-kit): the free-build kit for the crew berth and fishing camps (design 3.8, 5.3.1), the reusable
# stand kit (CURRENT.md section 3, item 2.1), the tacky grown-up decor (design 2.1) and the fixed berth site. Run it in an EMPTY Blender file:
#   FG_ROOT=$PWD blender -b --factory-startup --python Art/Blender/fg_berthkit.py
# main() rebuilds every model, bakes AO into the vertex colours, exports one FBX per model to Art/Export/BerthKit, writes berthkit_manifest.json
# and renders the previews to Art/Previews/BerthKit. Deterministic: every random draw is random.Random(SEED ^ crc32(stream)).
#
# Encoding is the harbour's (Art/Blender/fg_harbour.py header): authored in UNITY axes (x east, y up, z north/out to sea), mapped once to
# Blender by to_blender, FBX settings and export_if_changed from fg_harbour.py; UV0 "UVMap" in metres, UV1 "Layer" = (palette slot, texture
# layer), vertex colour "Col" = 0.5 x baked AO, smooth or analytic normals, no materials. fg_harbour.py is imported (it has no import side
# effects: no bpy calls at module level) for its Group/geometry helpers, palette slots S and layers L; this script does not edit it.
#
# KIT CONVENTIONS (also in the manifest "grid" block; the Unity builder must follow them):
#   * 1 m grid. Cell corners sit on integer Unity x/z. A piece's PIVOT is the centre of its footprint (a 1 x 2 piece pivots on the middle of
#     its two cells), so the pivot of a 1 x 1 piece is at x.5, z.5. Yaw is a multiple of 90 degrees. A piece faces +z (stairs rise, rails and
#     trim sit on the +z edge, benches face +z); to use another edge, yaw it.
#   * Y DATUM: y = 0 in a berth model is the WALKING SURFACE (where feet stand). Tiles hang below it (0 .. -0.16), pilings run from -L up to
#     the tile underside. Placing a piece = (cell centre x, datum_y, cell centre z). Stand and decor props are plain props: pivot at the base.
#   * DATUM LEVELS: datum_y(n) = 1.25 + n x 1.0 m above the water (y 0). n = 0 is the TUG DECK height (TUG_DECK 1.25), so the gangway always
#     lands; n = -1 is 0.25 (the beach at the berth is 0.24); stairs rise exactly 1.0 m (2.0 m run), ramps 1.0 m over 4.0 m. Pieces on the
#     same level share one datum_y; camps use the same 1 m steps from their own base level.
#   * Rails: top 1.0 m above the datum, posts 1.12; roof posts 2.5 m; flat roof panels sit at +2.5 (built into the model, so cell coordinates
#     stay the same); pitched panels rise 0.5 m over 1 m (+2.5 .. +3.0), two facing panels meet at the ridge at +3.0.
#   * Rails, posts, trim and roof posts are inboard of the +z cell edge (0.045 m) so a run of cells never overhangs its footprint.
import bpy, bmesh, math, json, os, sys, zlib, random
from mathutils import Vector, Matrix

def _here():
    if '__file__' in globals():
        return os.path.dirname(os.path.abspath(__file__))
    return os.path.join(os.environ.get('FG_ROOT', os.getcwd()), 'Art', 'Blender')

sys.path.insert(0, _here())
import fg_harbour as H

ROOT = os.environ.get('FG_ROOT') or os.path.abspath(os.path.join(_here(), '..', '..'))
EXPORT_DIR = os.path.join(ROOT, 'Art', 'Export', 'BerthKit')
PREVIEW_DIR = os.path.join(ROOT, 'Art', 'Previews', 'BerthKit')
MANIFEST_PATH = os.path.join(EXPORT_DIR, 'berthkit_manifest.json')
SCRIPT_PATH = os.path.join(ROOT, 'Art', 'Blender', 'fg_berthkit.py')

S, L = H.S, H.L
ry, rx, rz = H.ry, H.rx, H.rz
V3 = Vector
SLOT_PINK = 48           # NEW palette slot (berth-kit range 48-55): FlamingoPink, sRGB (0.95, 0.42, 0.58), the same all year
PINK_RGB = (0.95, 0.42, 0.58)
WHITE = 11               # fg_harbour palette slot 11 (0.95, 0.96, 0.98): plastic, enamel, paint
DATUM_TUG = H.TUG_DECK   # 1.25: the berth deck level that matches the tug's gangway
TILE_T = 0.16            # deck tile thickness below the walking surface

def rng(stream):
    return random.Random(H.SEED ^ zlib.crc32(('berthkit.' + stream).encode('utf-8')))

# ---------------------------------------------------------------------------------------------------------------------------------------
# Geometry helpers (all Unity axes; a model is built around its own pivot at the origin).
# ---------------------------------------------------------------------------------------------------------------------------------------
def new(name):
    g = H.Group(name)
    g.extra = {}
    return g

def A(g, geo, c=(0, 0, 0), rot=None, slot=S['Wood'], layer=L['Wood'], ao=0.5, fsl=None):
    g.add(H.xform(geo, c, rot), slot, layer, ao, fsl)

def BX(g, c, size, r, slot, layer=L['Wood'], ao=0.5, rot=None, segs=1):
    # rounded box centred on c (one bevel segment: 44 triangles)
    g.add(H.xform(H.rbox(size, r, segs), c, rot), slot, layer, ao)

def flat_box_geo(lo, hi):
    lo, hi = V3(lo), V3(hi)
    size = hi - lo
    V, N, F, UV = [], [], [], []
    for ax in range(3):
        for sg in (-1, 1):
            n = V3((0, 0, 0)); n[ax] = sg
            u, v = [i for i in range(3) if i != ax]
            quad = []
            for (a, b) in ((0, 0), (1, 0), (1, 1), (0, 1)):
                p = V3((0, 0, 0))
                p[ax] = hi[ax] if sg > 0 else lo[ax]
                p[u] = hi[u] if a else lo[u]
                p[v] = hi[v] if b else lo[v]
                quad.append(p)
            if (quad[1] - quad[0]).cross(quad[2] - quad[0]).dot(n) < 0:
                quad.reverse()
            base = len(V)
            V += quad; N += [n.copy()] * 4
            F.append([base, base + 1, base + 2, base + 3])
            UV.append([H.box_uv(p, n, size) for p in quad])
    return V, N, F, UV

def FB(g, lo, hi, slot, layer=L['Wood'], ao=0.5, rot=None, c=(0, 0, 0)):
    # sharp box from lo to hi (12 triangles, flat normals): for parts nobody sees up close
    g.add(H.xform(flat_box_geo(lo, hi), c, rot), slot, layer, ao)

def FBC(g, c, size, slot, layer=L['Wood'], ao=0.5, rot=None):
    h = V3(size) / 2
    g.add(H.xform(flat_box_geo(-h, h), c, rot), slot, layer, ao)

def scale_geo(geo, s):
    V, N, F, UV = geo
    return ([V3((v[0] * s[0], v[1] * s[1], v[2] * s[2])) for v in V],
            [V3((n[0] / s[0], n[1] / s[1], n[2] / s[2])).normalized() for n in N], F, UV)

def LA(g, c, prof, slot, layer=L['none'], ao=0.5, sides=10, rot=None, scale=None, cap0=False, cap1=False, ring_slots=None):
    geo, fs = H.lathe(prof, sides, cap0, cap1, ring_slots=ring_slots)
    if scale:
        geo = scale_geo(geo, scale)
    fsl = [(x if x is not None else slot, layer) for x in fs] if ring_slots else None
    g.add(H.xform(geo, c, rot), slot, layer, ao, fsl)

def TB(g, pts, rad, slot, layer=L['none'], ao=0.5, sides=5, caps=True, closed=False):
    g.add(H.tube(pts, rad, sides, caps=caps, closed=closed), slot, layer, ao)

def TT(g, pts, radii, slot, layer=L['none'], ao=0.5, sides=6):
    g.add(H.taper_tube(pts, radii, sides), slot, layer, ao)

def prism_geo(poly, depth, r=0.0):
    return H.prism(poly, depth, r)

def PR(g, poly, depth, c, rot, slot, layer=L['Wood'], ao=0.5):
    # polygon in the (z, y) plane extruded along x (centred), placed at c with rot
    g.add(H.xform(H.prism(poly, depth, 0.0), c, rot), slot, layer, ao)

def PL(g, c, length, width, thick, slot, layer=L['Wood'], ao=0.45, yaw=0.0, ch=0.012, rot=None):
    # a plank: long axis x, width z, top chamfered (20 triangles). c is the centre of its thickness.
    w2, t2 = width / 2, thick / 2
    poly = [(-w2, -t2), (w2, -t2), (w2, t2 - ch), (w2 - ch, t2), (-w2 + ch, t2), (-w2, t2 - ch)]
    R = rot if rot is not None else ry(yaw)
    PR(g, poly, length, c, R, slot, layer, ao)

def basis(f, u, r):
    return Matrix(((f[0], u[0], r[0]), (f[1], u[1], r[1]), (f[2], u[2], r[2])))

def gx(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]

def place(dst, src, c=(0, 0, 0), yaw=0.0, name=None, **extra):
    # copy a built Group into another (composition for the site models and the preview scenes)
    R = ry(yaw)
    c = V3(c)
    base = len(dst.V)
    dst.V += [c + R @ v for v in src.V]
    dst.N += [(R @ n).normalized() for n in src.N]
    for i, f in enumerate(src.F):
        dst.F.append(tuple(base + k for k in f))
        dst.UV.append(list(src.UV[i]))
        dst.SL.append(src.SL[i])
        dst.AO.append(src.AO[i])
    d = dict(name=name or src.name, position=[round(x, 3) for x in c], yaw=yaw)
    d.update(extra)
    dst.items.append(d)

# ---------------------------------------------------------------------------------------------------------------------------------------
# STAND KIT (CURRENT.md section 3, item 2.1). Plain props: pivot = base centre, facing +z, REPO density (a crate ~100 triangles).
# ---------------------------------------------------------------------------------------------------------------------------------------
def b_stand_rodholder():
    # A bank-stick style holder: a steel spike, a post, two angled PVC sleeves (a rod rests in each) and a strap.
    g = new('Stand_RodHolder')
    LA(g, (0, 0, 0), [(-0.25, 0.0), (-0.24, 0.018), (0.0, 0.03), (0.1, 0.035), (0.14, 0.0)], S['Rock'], L['none'], 0.4, sides=6)
    BX(g, (0, 0.3, 0), (0.09, 0.62, 0.09), 0.02, S['WoodDark'], L['Wood'], 0.45)
    BX(g, (0, 0.62, 0), (0.11, 0.05, 0.3), 0.015, S['Wood'], L['Wood'], 0.45)              # cross-piece the sleeves bolt to
    for sx in (-1, 1):
        LA(g, (sx * 0.1, 0.62, 0.0), [(-0.19, 0.055), (-0.18, 0.072), (0.19, 0.072), (0.2, 0.055)], WHITE, L['none'], 0.55, sides=8,
           rot=rx(-28) @ rz(sx * 5))
        LA(g, (sx * 0.1, 0.62, 0.0), [(-0.1, 0.0), (-0.098, 0.062), (0.098, 0.062), (0.1, 0.0)], S['Accent'], L['none'], 0.5, sides=8,
           rot=rx(-28) @ rz(sx * 5))
    return g

def b_stand_baitbucket():
    g = new('Stand_BaitBucket')
    LA(g, (0, 0, 0), [(0.0, 0.0), (0.0, 0.15), (0.02, 0.165), (0.3, 0.2), (0.315, 0.2), (0.32, 0.185)], S['Accent'], L['none'], 0.5, sides=10, cap0=True)
    LA(g, (0, 0.3, 0), [(0.0, 0.205), (0.025, 0.21), (0.04, 0.0)], S['Accent'], L['none'], 0.55, sides=10)                  # lid rim
    LA(g, (0, 0.34, 0), [(0.0, 0.0), (0.0, 0.17), (0.03, 0.15), (0.05, 0.0)], S['Accent'], L['none'], 0.6, sides=10)        # lid
    LA(g, (0, 0.389, 0), [(0.0, 0.0), (0.0, 0.05), (0.05, 0.05), (0.055, 0.0)], S['Rock'], L['none'], 0.5, sides=6)         # knob
    TB(g, [V3((0.2 * math.cos(a), 0.3 + 0.2 * math.sin(a), 0.0)) for a in [math.pi * i / 6 for i in range(7)]], 0.012, S['Rock'], L['none'], 0.5, 4)   # bail
    FB(g, (-0.09, 0.1, 0.19), (0.09, 0.2, 0.205), WHITE, L['none'], 0.6)                                                     # BAIT label
    FB(g, (-0.075, 0.125, 0.205), (0.075, 0.135, 0.209), S['Roof'], L['none'], 0.6)
    return g

def b_stand_crateseat():
    # a slatted crate with a folded cushion: sit, kneel or put a rod across it (~190 triangles)
    g = new('Stand_CrateSeat')
    for sy, sl in ((0.07, S['Wood']), (0.19, S['Dock'])):
        for sz in (-1, 1):
            FB(g, (-0.25, sy - 0.04, sz * 0.205 - 0.02), (0.25, sy + 0.04, sz * 0.205 + 0.02), sl, L['Wood'], 0.45)
        for sx in (-1, 1):
            FB(g, (sx * 0.23 - 0.02, sy - 0.04, -0.2), (sx * 0.23 + 0.02, sy + 0.04, 0.2), sl, L['Wood'], 0.45)
    for sx in (-1, 1):
        for sz in (-1, 1):
            FBC(g, (sx * 0.235, 0.14, sz * 0.205), (0.05, 0.28, 0.05), S['WoodDark'], L['Wood'], 0.4)
    FBC(g, (0, 0.3, 0), (0.5, 0.04, 0.46), S['WoodDark'], L['Wood'], 0.45)
    BX(g, (0, 0.345, 0.0), (0.42, 0.06, 0.4), 0.025, S['Accent'], L['Cloth'], 0.6)
    return g

def b_stand_tacklebox():
    g = new('Stand_TackleBox')
    BX(g, (0, 0.11, 0), (0.46, 0.2, 0.24), 0.03, S['Roof'], L['none'], 0.5)
    BX(g, (0, 0.235, 0), (0.44, 0.07, 0.23), 0.025, S['Roof'], L['none'], 0.55)                                              # lid
    BX(g, (0, 0.275, 0), (0.26, 0.03, 0.05), 0.012, S['Rock'], L['none'], 0.5)                                                # handle bar
    for sx in (-1, 1):
        FBC(g, (sx * 0.11, 0.255, 0.0), (0.03, 0.05, 0.05), S['Rock'], L['none'], 0.5)
        FBC(g, (sx * 0.13, 0.17, 0.123), (0.05, 0.11, 0.015), S['Rock'], L['none'], 0.5)                                        # latches
    FB(g, (-0.2, 0.14, 0.119), (0.2, 0.18, 0.123), S['Sand'], L['none'], 0.6)                                                   # stripe
    return g

def b_stand_lantern():
    # Storm lantern: base, cage, cap and bail. The glass is its own object (Stand_Lantern_Glow) so Unity can make it emissive.
    g = new('Stand_Lantern')
    LA(g, (0, 0, 0), [(0.0, 0.0), (0.0, 0.11), (0.02, 0.125), (0.07, 0.12), (0.1, 0.095)], S['Rock'], L['none'], 0.4, sides=10, cap1=True)
    LA(g, (0, 0.34, 0), [(0.0, 0.1), (0.03, 0.105), (0.07, 0.07), (0.1, 0.03), (0.115, 0.0)], S['Rock'], L['none'], 0.5, sides=10)
    LA(g, (0, 0.34, 0), [(0.0, 0.0), (0.0, 0.1), (0.02, 0.105)], S['Rock'], L['none'], 0.5, sides=10)
    for k in range(4):
        a = math.pi / 2 * k + math.pi / 4
        c0, s0 = math.cos(a), math.sin(a)
        TB(g, [V3((0.1 * c0, 0.1, 0.1 * s0)), V3((0.1 * c0, 0.2, 0.1 * s0)), V3((0.1 * c0, 0.34, 0.1 * s0))], 0.007, S['Rock'], L['none'], 0.5, 4)
    TB(g, [V3((0.1 * math.cos(a), 0.46 + 0.08 * math.sin(a), 0.0)) for a in [math.pi * i / 8 for i in range(9)]], 0.009, S['Rock'], L['none'], 0.5, 4)
    LA(g, (0, 0.455, 0), [(0.0, 0.03), (0.02, 0.025), (0.035, 0.0)], S['Rock'], L['none'], 0.5, sides=8)
    g.extra['companion'] = 'Stand_Lantern_Glow'
    g.extra['light_points'] = [dict(name='LanternLight', position=[0.0, 0.2, 0.0], color=[1.0, 0.72, 0.38], range=14.0, intensity=1.3,
                                    note='warm point light; flicker 3-5 percent at 8 Hz; at night the stand must read from 10 m')]
    return g

def b_stand_lantern_glow():
    g = new('Stand_Lantern_Glow')
    LA(g, (0, 0.012, 0), [(0.0, 0.0), (0.0, 0.082), (0.2, 0.092), (0.32, 0.066), (0.324, 0.0)], S['Accent'], L['none'], 0.9, sides=10)
    g.extra['emissive'] = True
    g.extra['companion_of'] = 'Stand_Lantern'
    return g

def b_stand_ropecoil():
    # a heavier coil than the harbour's: three flat turns, a second layer and a loose tail
    g = new('Stand_RopeCoil')
    pts = []
    for i in range(3 * 12 + 1):
        a = 2 * math.pi * i / 12
        rr = 0.1 + 0.15 * i / 36
        pts.append(V3((rr * math.cos(a), 0.027, rr * math.sin(a))))
    TB(g, pts, 0.027, S['Sand'], L['Cloth'], 0.5, 5)
    pts2 = []
    for i in range(int(1.5 * 12) + 1):
        a = 2 * math.pi * i / 12 + 0.6
        rr = 0.19 - 0.06 * i / 18
        pts2.append(V3((rr * math.cos(a), 0.078, rr * math.sin(a))))
    TB(g, pts2, 0.027, S['Sand'], L['Cloth'], 0.5, 5)
    TB(g, [V3((0.25, 0.027, 0.0)), V3((0.35, 0.026, 0.08)), V3((0.46, 0.022, 0.12))], 0.022, S['Sand'], L['Cloth'], 0.5, 5)
    return g

STAND_BUILDERS = [b_stand_rodholder, b_stand_baitbucket, b_stand_crateseat, b_stand_tacklebox, b_stand_lantern, b_stand_lantern_glow, b_stand_ropecoil]

# ---------------------------------------------------------------------------------------------------------------------------------------
# BERTH KIT (1 m grid, datum = walking surface; see the header). Footprints are in cells (x, z) before yaw.
# ---------------------------------------------------------------------------------------------------------------------------------------
PILING_LENGTHS = (2, 3, 4, 5, 6, 7)

def b_piling(n):
    # Round timber pile, pivot at the datum: the body runs from y = -n up to the tile underside (-0.17) with a rounded cap. Taller than
    # the water: the lower 40 percent is the dark wet band. Pick the shortest length with n >= datum_y - seabed_y + 0.3 (embed 0.3 m).
    g = new('Berth_Piling_%dm' % n)
    top = n - 0.17
    sl = []
    prof = [(0.0, 0.0), (0.03, 0.11), (0.15, 0.135)]
    k = max(2, int(n / 1.6))
    for i in range(1, k + 1):
        prof.append((0.15 + (top - 0.4 - 0.15) * i / k, 0.15 - 0.012 * (i / k)))
    prof += [(top - 0.08, 0.14), (top - 0.02, 0.115), (top, 0.0)]
    wet = 0.4 * n
    for i in range(len(prof) - 1):
        sl.append(S['WoodDark'] if prof[i + 1][0] <= wet else S['Wood'])
    LA(g, (0, -n, 0), prof, S['Wood'], L['Bark'], 0.45, sides=8, ring_slots=sl)
    for h in (top - 0.3, top - 0.55):                                                      # steel straps under the cap
        LA(g, (0, -n + h, 0), [(0.0, 0.147), (0.012, 0.158), (0.034, 0.158), (0.046, 0.147)], S['Rock'], L['none'], 0.4, sides=8)
    return g

def deck_tile(g, w, d, planks_along_z=True):
    # w (x) by d (z) cells; boards run along z, stringers across. Top at y = 0, underside at -TILE_T. 20 triangles a board.
    r = rng(g.name)
    nb = int(round(w / 0.25))
    bw = w / nb
    for i in range(nb):
        x = -w / 2 + bw * (i + 0.5)
        PL(g, (x, -0.025, 0), d - 0.006, bw - 0.012, 0.05, S['Wood'] if r.random() < 0.55 else S['Dock'], L['Wood'], 0.45, yaw=90.0, ch=0.01)
    ns = int(round(d / 0.8)) + 1 if d > 1.0 else 2
    zs = gx(-d / 2 + 0.12, d / 2 - 0.12, ns)
    for z in zs:
        FB(g, (-w / 2 + 0.005, -TILE_T, z - 0.045), (w / 2 - 0.005, -0.05, z + 0.045), S['WoodDark'], L['Wood'], 0.4)
    for sx in (-1, 1):                                                                      # edge beam so tiles read as framed
        FB(g, (sx * (w / 2 - 0.045) - 0.04, -TILE_T, -d / 2 + 0.005), (sx * (w / 2 - 0.045) + 0.04, -0.05, d / 2 - 0.005), S['WoodDark'], L['Wood'], 0.4)

def b_tile(w, d):
    g = new('Berth_DeckTile_%dx%d' % (w, d))
    deck_tile(g, w, d)
    return g

def b_trim():
    # fascia for the +z edge: hangs 0.30 m below the walking surface, proud of the cell by 0.045 m, with a cap lip
    g = new('Berth_DeckTrim_1m')
    BX(g, (0, -0.14, 0.527), (1.0, 0.32, 0.045), 0.012, S['Dock'], L['Wood'], 0.45)
    BX(g, (0, 0.012, 0.505), (1.0, 0.03, 0.085), 0.01, S['Wood'], L['Wood'], 0.45)
    return g

def b_stair():
    # 1 wide x 2 long, rises 1.0 toward +z: the top tread is flush with the datum, the foot stands on a surface 1.0 m lower (z = -1)
    g = new('Berth_Stair_1m')
    for i in range(4):
        top = -0.75 + 0.25 * i
        z0 = -1.0 + 0.5 * i
        PL(g, (0, top - 0.025, z0 + 0.25 - 0.015), 0.94, 0.54, 0.05, S['Wood'] if i % 2 == 0 else S['Dock'], L['Wood'], 0.45, ch=0.01)
        FB(g, (-0.46, top - 0.25, z0 - 0.005), (0.46, top - 0.045, z0 + 0.025), S['WoodDark'], L['Wood'], 0.4)         # riser
    for sx in (-1, 1):
        poly = [(-1.0, -1.0), (1.0, -1.0), (1.0, -0.05), (0.5, -0.05), (-1.0, -0.8)]
        PR(g, poly, 0.06, (sx * 0.47, 0, 0), Matrix.Identity(3), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.5, -1.0, -1.0), (0.5, -0.94, -0.88), S['WoodDark'], L['Wood'], 0.4)                                         # foot sill
    return g

def b_ramp():
    # 1 x 4, rises 1.0 toward +z over 4.0 m (14 degrees): slip cleats every 0.8 m. Datum at the high end (z = +2), the low foot at -1.0.
    g = new('Berth_Ramp_1m')
    ln = math.hypot(4.0, 1.0)
    f = V3((0, 1.0, 4.0)).normalized()
    u = V3((0, 4.0, -1.0)).normalized()
    R = basis(f, u, V3((-1, 0, 0)))
    mid = V3((0, -0.5, 0))
    r = rng(g.name)
    for i in range(4):
        x = -0.375 + 0.25 * i
        c = mid + R @ V3((0, -0.025, 0)) + V3((x, 0, 0))
        PL(g, c, ln, 0.235, 0.05, S['Wood'] if r.random() < 0.5 else S['Dock'], L['Wood'], 0.45, rot=R, ch=0.01)
    for k in range(5):
        t = (k + 0.5) / 5.0 - 0.5
        c = mid + f * (t * ln) + u * 0.016
        BX(g, c, (0.06, 0.032, 0.94), 0.008, S['WoodDark'], L['Wood'], 0.45, rot=R)
    for sx in (-1, 1):
        BX(g, mid + V3((sx * 0.45, 0, 0)) + u * -0.1, (ln, 0.1, 0.06), 0.01, S['WoodDark'], L['Wood'], 0.4, rot=R)
    return g

def b_rail():
    # one metre of rail on the +z edge with its end post at +x; the next cell's rail starts where this post ends
    g = new('Berth_Rail_1m')
    z = 0.455
    BX(g, (0, 0.995, z), (1.0, 0.045, 0.085), 0.015, S['Wood'], L['Wood'], 0.45)
    BX(g, (0, 0.55, z), (0.99, 0.04, 0.05), 0.012, S['Dock'], L['Wood'], 0.45)
    FB(g, (-0.5, 0.03, z - 0.012), (0.5, 0.15, z + 0.012), S['WoodDark'], L['Wood'], 0.4)                                   # kick board
    for x in (-0.26, 0.0, 0.26):
        FB(g, (x - 0.012, 0.15, z - 0.012), (x + 0.012, 0.99, z + 0.012), S['Dock'], L['Wood'], 0.45)
    g.add(H.xform(H.rbox((0.09, 1.2, 0.09), 0.02, 1), (0.455, 0.47, z)), S['WoodDark'], L['Wood'], 0.45)
    return g

def b_railpost():
    g = new('Berth_RailPost')
    g.add(H.xform(H.rbox((0.09, 1.2, 0.09), 0.02, 1), (-0.455, 0.47, 0.455)), S['WoodDark'], L['Wood'], 0.45)
    return g

def b_roofpost():
    g = new('Berth_RoofPost')
    x = z = 0.43
    BX(g, (x, 1.17, z), (0.12, 2.7, 0.12), 0.02, S['WoodDark'], L['Wood'], 0.45)
    FB(g, (x - 0.07, -0.16, z - 0.07), (x + 0.07, -0.13, z + 0.07), S['Rock'], L['none'], 0.4)                                  # foot plate
    for sx, sz in ((-1, 0), (0, -1)):                                                                                       # two braces to the beam line
        BX(g, (x + sx * 0.17, 2.3, z + sz * 0.17), (0.05, 0.4, 0.05), 0.012, S['Wood'], L['Wood'], 0.45,
           rot=(rz(-45 * sx) if sx else rx(45 * sz * -1)))
    return g

def corrugated_geo(width, depth, thick=0.014, waves=4):
    # sheet 'width' (x) with 'waves' corrugations, running 'depth' along z; top near y = +0.02, bottom near y = -thick
    pts_top = []
    for i in range(waves * 2 + 1):
        x = -width / 2 + width * i / (waves * 2)
        pts_top.append((x, 0.016 if i % 2 else -0.002))
    poly = [(p[0], p[1]) for p in pts_top] + [(p[0], p[1] - thick) for p in reversed(pts_top)]
    # prism extrudes along x with the polygon in (z, y): swap so the section's x becomes the prism's z and rotate the length onto z
    poly_zy = [(p[0], p[1]) for p in poly]
    return H.prism(poly_zy, depth, 0.0)

def b_roof_flat():
    g = new('Berth_RoofPanel_Flat')
    A(g, corrugated_geo(1.0, 1.0), (0, 2.5, 0), ry(90), S['Roof'], L['none'], 0.55)
    FB(g, (-0.5, 2.47, -0.5), (0.5, 2.498, -0.47), S['Rock'], L['none'], 0.5)                                              # purlins show at the eaves
    FB(g, (-0.5, 2.47, 0.47), (0.5, 2.498, 0.5), S['Rock'], L['none'], 0.5)
    return g

def b_roof_pitched():
    # rises toward +z: (z -0.5, y 2.5) to (z +0.5, y 3.0). Two panels, the second yawed 180 in the next cell, meet at the ridge at +3.0
    g = new('Berth_RoofPanel_Pitched')
    ang = math.degrees(math.atan2(0.5, 1.0))
    A(g, corrugated_geo(1.0, math.hypot(1.0, 0.5)), (0, 2.75, 0), rx(-ang) @ ry(90), S['Roof'], L['none'], 0.55)
    return g

def b_roof_ridge():
    g = new('Berth_RoofRidge_1m')
    poly = [(-0.2, -0.1), (0.0, 0.0), (0.2, -0.1), (0.17, -0.1), (0.0, -0.03), (-0.17, -0.1)]
    PR(g, poly, 1.0, (0, 3.0, 0), Matrix.Identity(3), S['Rock'], L['none'], 0.55)
    return g

def b_bench():
    # 2 x 1: a slatted bench facing +z, seat 0.45 m, back to 0.95 m; sits 0.12 m in from the -z edge so the back can lean on a rail
    g = new('Berth_Bench_2m')
    r = rng(g.name)
    zc = -0.12
    for k in range(3):
        PL(g, (0, 0.44, zc + 0.17 - 0.17 * k + 0.0), 1.8, 0.13, 0.04, S['Wood'] if k % 2 == 0 else S['Dock'], L['Wood'], 0.45, ch=0.008)
    for k, y in enumerate((0.7, 0.86)):
        PL(g, (0, y, zc - 0.27 - 0.03 * k), 1.8, 0.13, 0.04, S['Dock'] if k else S['Wood'], L['Wood'], 0.45, rot=rx(-8) @ ry(0), ch=0.008)
    for sx in (-1, 1):
        x = sx * 0.82
        FB(g, (x - 0.03, 0.0, zc + 0.19), (x + 0.03, 0.42, zc + 0.25), S['WoodDark'], L['Wood'], 0.4)                          # front leg
        FB(g, (x - 0.03, 0.0, zc - 0.31), (x + 0.03, 0.95, zc - 0.25), S['WoodDark'], L['Wood'], 0.4)                          # rear leg + back post
        FB(g, (x - 0.03, 0.4, zc - 0.28), (x + 0.03, 0.46, zc + 0.24), S['WoodDark'], L['Wood'], 0.4)                          # seat rail
        BX(g, (x, 0.64, zc + 0.04), (0.06, 0.05, 0.55), 0.012, S['Wood'], L['Wood'], 0.45)                                    # arm
        FB(g, (x - 0.03, 0.4, zc + 0.19), (x + 0.03, 0.64, zc + 0.22), S['WoodDark'], L['Wood'], 0.4)
    return g

def b_cleaningtable():
    # 2 x 1: 1.7 m work table, 0.92 m high, raised lip on three sides, stainless sink dip, knife block, hooks and a hanging catch
    g = new('Berth_CleaningTable')
    BX(g, (0, 0.89, 0.0), (1.7, 0.06, 0.75), 0.015, S['Wood'], L['Wood'], 0.45)
    FB(g, (-0.85, 0.92, -0.375), (0.85, 0.99, -0.34), S['WoodDark'], L['Wood'], 0.4)                                        # back lip
    FB(g, (-0.85, 0.92, -0.34), (-0.82, 0.96, 0.375), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (0.82, 0.92, -0.34), (0.85, 0.96, 0.375), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.78, 0.92, -0.22), (-0.28, 0.925, 0.22), S['Rock'], L['none'], 0.5)                                            # sink plate
    LA(g, (-0.53, 0.915, 0.0), [(0.0, 0.0), (0.0, 0.045), (0.012, 0.05)], S['WoodDark'], L['none'], 0.3, sides=8)           # drain
    for sx in (-1, 1):
        for sz in (-1, 1):
            FBC(g, (sx * 0.77, 0.43, sz * 0.31), (0.07, 0.86, 0.07), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.8, 0.22, -0.34), (0.8, 0.26, 0.34), S['Dock'], L['Wood'], 0.4)                                                # lower shelf
    FB(g, (-0.8, 0.8, -0.04), (0.8, 0.86, 0.0), S['WoodDark'], L['Wood'], 0.4)
    BX(g, (0.45, 0.99, 0.05), (0.14, 0.12, 0.1), 0.012, S['Dock'], L['Wood'], 0.45)                                         # knife block
    for dx in (-0.025, 0.025):
        FB(g, (0.45 + dx - 0.006, 1.05, 0.04), (0.45 + dx + 0.006, 1.2, 0.06), S['Rock'], L['none'], 0.5)
    for sx in (-1, 1):                                                                                                      # back posts with a hook bar
        FBC(g, (sx * 0.77, 1.3, -0.34), (0.06, 0.8, 0.06), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.8, 1.6, -0.36), (0.8, 1.66, -0.32), S['Wood'], L['Wood'], 0.4)
    for hx in (-0.4, 0.2):
        TB(g, [V3((hx, 1.6, -0.3)), V3((hx, 1.48, -0.27)), V3((hx + 0.03, 1.42, -0.22)), V3((hx + 0.04, 1.47, -0.24))], 0.009, S['Rock'], L['none'], 0.5, 4)
    fish = H.lathe([(-0.28, 0.0), (-0.22, 0.05), (-0.05, 0.075), (0.1, 0.06), (0.2, 0.02), (0.24, 0.0)], 8)[0]               # a hung catch
    A(g, scale_geo(fish, (1.0, 1.0, 0.45)), (-0.4 + 0.0, 1.2, -0.24), None, S['Rock'], L['none'], 0.55)
    return g

def b_smoker():
    # 1 x 1 oil-drum smoker: 0.55 m drum on a stand, door with a latch, thermometer, stack with a rain cap, a coal tray
    g = new('Berth_Smoker')
    for sx in (-1, 1):
        for sz in (-1, 1):
            FBC(g, (sx * 0.27, 0.15, sz * 0.27), (0.05, 0.3, 0.05), S['Rock'], L['none'], 0.35)
    FB(g, (-0.3, 0.3, -0.3), (0.3, 0.34, 0.3), S['Rock'], L['none'], 0.35)
    prof = [(0.0, 0.0), (0.0, 0.31), (0.02, 0.32), (0.07, 0.325), (0.5, 0.325), (0.55, 0.32), (0.66, 0.3), (0.7, 0.27), (0.74, 0.0)]
    LA(g, (0, 0.34, 0), prof, S['Rock'], L['none'], 0.4, sides=12, cap0=True,
       ring_slots=[S['Rock'], S['WoodDark'], S['Rock'], S['Rock'], S['Rock'], S['Rock'], S['WoodDark'], S['Rock']])
    for h in (0.33, 0.64):
        LA(g, (0, 0.34 + h, 0), [(0.0, 0.325), (0.005, 0.335), (0.03, 0.335), (0.035, 0.325)], S['WoodDark'], L['none'], 0.35, sides=12)
    BX(g, (0, 0.6, 0.322), (0.3, 0.38, 0.03), 0.01, S['WoodDark'], L['none'], 0.4)                                          # door
    FBC(g, (0.1, 0.6, 0.345), (0.03, 0.12, 0.02), S['Sand'], L['none'], 0.55)                                               # latch
    LA(g, (-0.1, 0.9, 0.318), [(0.0, 0.0), (0.0, 0.04), (0.02, 0.045), (0.03, 0.0)], WHITE, L['none'], 0.6, sides=10, rot=rx(90))    # thermometer
    LA(g, (0.1, 1.04, -0.12), [(0.0, 0.07), (0.38, 0.06), (0.4, 0.075), (0.43, 0.08), (0.45, 0.0)], S['Rock'], L['none'], 0.4, sides=8)  # stack
    LA(g, (0.1, 1.49, -0.12), [(0.0, 0.0), (0.0, 0.1), (0.04, 0.0)], S['Rock'], L['none'], 0.4, sides=8)                    # rain cap
    return g

def b_crabpotrack():
    # 1 x 1: a rack holding three round crab pots (mesh drums with a funnel mouth) and a net float on the top rail
    g = new('Berth_CrabPotRack')
    for sx in (-1, 1):
        for sz in (-1, 1):
            FBC(g, (sx * 0.47, 0.7, sz * 0.4), (0.06, 1.4, 0.06), S['WoodDark'], L['Wood'], 0.4)
        FB(g, (sx * 0.47 - 0.03, 0.06, -0.4), (sx * 0.47 + 0.03, 0.1, 0.4), S['Wood'], L['Wood'], 0.4)
    for y in (0.5, 0.95, 1.4):
        FB(g, (-0.5, y - 0.025, -0.43), (0.5, y + 0.025, -0.37), S['Wood'], L['Wood'], 0.4)
        FB(g, (-0.5, y - 0.025, 0.37), (0.5, y + 0.025, 0.43), S['Wood'], L['Wood'], 0.4)
    for k, y in enumerate((0.1, 0.55)):
        LA(g, (0.0, y + 0.17, 0.0), [(-0.38, 0.0), (-0.36, 0.17), (-0.2, 0.19), (0.2, 0.19), (0.36, 0.17), (0.38, 0.0)], S['Rock'], L['Cloth'], 0.5,
           sides=9, rot=rz(90), cap0=True, cap1=True, ring_slots=[S['Rock'], S['WoodDark'], S['Rock'], S['WoodDark'], S['Rock']])
        LA(g, (0.0, y + 0.17, 0.17), [(0.0, 0.07), (0.07, 0.06), (0.1, 0.0)], S['WoodDark'], L['none'], 0.3, sides=7, rot=rx(90))                # funnel mouth
    LA(g, (0.0, 0.95 + 0.17, 0.0), [(-0.38, 0.0), (-0.36, 0.17), (-0.2, 0.19), (0.2, 0.19), (0.36, 0.17), (0.38, 0.0)], S['Rock'], L['Cloth'], 0.5,
       sides=9, rot=rz(90), cap0=True, cap1=True, ring_slots=[S['Rock'], S['WoodDark'], S['Rock'], S['WoodDark'], S['Rock']])
    LA(g, (0.2, 1.43, 0.0), [(-0.12, 0.0), (-0.1, 0.1), (0.0, 0.14), (0.1, 0.1), (0.12, 0.0)], S['Accent'], L['none'], 0.6, sides=8)               # float
    return g

def b_rodrack():
    # 1 x 0.5 (use a full cell): a freestanding rack for five rods leaning at 9 degrees. Base plank, two rails with notches, rods with reels.
    g = new('Berth_RodRack')
    FB(g, (-0.46, 0.0, -0.12), (0.46, 0.05, 0.16), S['WoodDark'], L['Wood'], 0.4)
    for sx in (-1, 1):
        FB(g, (sx * 0.43 - 0.03, 0.05, 0.04), (sx * 0.43 + 0.03, 0.95, 0.1), S['Wood'], L['Wood'], 0.4)
    FB(g, (-0.46, 0.5, 0.0), (0.46, 0.57, 0.1), S['Dock'], L['Wood'], 0.4)
    FB(g, (-0.46, 0.88, 0.0), (0.46, 0.95, 0.1), S['Dock'], L['Wood'], 0.4)
    r = rng(g.name)
    for i in range(5):
        x = -0.33 + 0.165 * i
        lean = rx(-9)
        top = lean @ V3((0, 1.9, 0))
        TT(g, [V3((x, 0.08, 0.02)), V3((x, 0.08, 0.02)) + lean @ V3((0, 0.9, 0)), V3((x, 0.08, 0.02)) + top], [0.016, 0.011, 0.005], S['Rock'], L['none'], 0.5, 5)
        LA(g, (x, 0.08, 0.02), [(0.0, 0.0), (0.0, 0.026), (0.075, 0.03), (0.08, 0.0)], S['WoodDark'], L['Wood'], 0.45, sides=5)     # cork grip
        LA(g, V3((x, 0.0, 0.0)) + V3((0, 0.62, 0.08)), [(-0.022, 0.0), (-0.02, 0.04), (0.02, 0.04), (0.022, 0.0)], S['Sand'] if i % 2 else S['Accent'], L['none'], 0.5, sides=7, rot=rz(90))
    return g

def b_namesign():
    # 2 x 1: a free-standing name board. The blank face is a flat plane for UI text (see text_surface in the manifest).
    g = new('Berth_NameSign')
    for sx in (-1, 1):
        BX(g, (sx * 0.86, 0.95, 0.0), (0.14, 2.0, 0.14), 0.025, S['WoodDark'], L['Wood'], 0.45)
        LA(g, (sx * 0.86, 1.95, 0.0), [(0.0, 0.0), (0.01, 0.09), (0.06, 0.1), (0.1, 0.0)], S['Rock'], L['none'], 0.45, sides=8)    # iron post cap
    BX(g, (0, 1.4, 0.0), (1.78, 0.62, 0.07), 0.025, S['Dock'], L['Wood'], 0.45)                                              # frame
    FB(g, (-0.78, 1.14, 0.034), (0.78, 1.66, 0.04), S['Sand'], L['Plaster'], 0.62)                                           # painted face
    for sx in (-1, 1):
        for sy in (-1, 1):
            LA(g, (sx * 0.8, 1.4 + sy * 0.27, 0.04), [(0.0, 0.0), (0.0, 0.016), (0.01, 0.017), (0.015, 0.0)], S['Rock'], L['none'], 0.45, sides=6, rot=rx(90))
    g.extra['text_surface'] = dict(center=[0.0, 1.4, 0.0405], size=[1.56, 0.52], normal=[0, 0, 1],
                                   note='blank painted board; put the crew name (UI text, TMP) on this rectangle')
    return g

def b_stringlights():
    # 2 m span string lights hooked 2.3 m above the datum at x = -1 and +1, sagging 0.28 m. Bulbs are their own object (emissive).
    g = new('Berth_StringLights_2m')
    for sx in (-1, 1):
        LA(g, (sx, 2.3, 0), [(0.0, 0.0), (0.0, 0.025), (0.03, 0.025), (0.035, 0.0)], S['Rock'], L['none'], 0.5, sides=6, rot=rx(90))
    pts = H.rope_line((-1, 2.3, 0), (1, 2.3, 0), 0.28, 10)
    TB(g, pts, 0.01, S['WoodDark'], L['none'], 0.5, 4)
    for i in range(5):
        p = pts[1 + 2 * i]
        FBC(g, (p.x, p.y - 0.035, p.z), (0.03, 0.05, 0.03), S['Rock'], L['none'], 0.5)
    g.extra['companion'] = 'Berth_StringLights_2m_Glow'
    g.extra['light_points'] = [dict(name='StringLightsA', position=[round(pts[3].x, 3), round(pts[3].y - 0.1, 3), 0.0], color=[1.0, 0.85, 0.55], range=7.0, intensity=0.9),
                               dict(name='StringLightsB', position=[round(pts[7].x, 3), round(pts[7].y - 0.1, 3), 0.0], color=[1.0, 0.85, 0.55], range=7.0, intensity=0.9)]
    g.extra['endpoints'] = [[-1.0, 2.3, 0.0], [1.0, 2.3, 0.0]]
    return g

def b_stringlights_glow():
    g = new('Berth_StringLights_2m_Glow')
    pts = H.rope_line((-1, 2.3, 0), (1, 2.3, 0), 0.28, 10)
    for i in range(5):
        p = pts[1 + 2 * i]
        LA(g, (p.x, p.y - 0.115, p.z), [(0.0, 0.0), (0.0, 0.02), (0.03, 0.046), (0.07, 0.04), (0.09, 0.0)], S['Accent'] if i % 2 else S['Sand'], L['none'], 0.9, sides=6)
    g.extra['emissive'] = True
    g.extra['companion_of'] = 'Berth_StringLights_2m'
    return g

def b_bollard():
    g = new('Berth_Bollard')
    prof = [(0.0, 0.0), (0.0, 0.2), (0.02, 0.22), (0.2, 0.15), (0.34, 0.13), (0.4, 0.17), (0.46, 0.2), (0.5, 0.17), (0.52, 0.0)]
    LA(g, (0, 0, 0), prof, S['Rock'], L['none'], 0.35, sides=10, cap0=True,
       ring_slots=[S['WoodDark'], S['Rock'], S['Rock'], S['Rock'], S['Rock'], S['Rock'], S['Rock'], S['Rock']])
    return g

def b_cleat():
    # horn cleat, 0.44 long, bolted to the deck
    g = new('Berth_Cleat')
    BX(g, (0, 0.085, 0), (0.44, 0.07, 0.07), 0.02, S['Rock'], L['none'], 0.4)
    FB(g, (-0.05, 0.0, -0.05), (0.05, 0.05, 0.05), S['Rock'], L['none'], 0.4)
    for sx in (-1, 1):
        FB(g, (sx * 0.19 - 0.035, 0.12, -0.035), (sx * 0.19 + 0.035, 0.2, 0.035), S['Rock'], L['none'], 0.4)
    return g

def b_tyrefender():
    # a tyre hung on the +z fascia by a short rope: ring torus (hollow centre), pivot at the hanging point on the deck edge (0, 0, 0.5)
    g = new('Berth_TyreFender')
    R, rr = 0.22, 0.1
    pts = [V3((R * math.cos(a), R * math.sin(a), 0.0)) + V3((0, -0.45, 0.62)) for a in [2 * math.pi * i / 14 for i in range(14)]]
    V, N, F, UV = H.tube(pts, rr, 7, caps=False, closed=True)
    A(g, (V, N, F, UV), (0, 0, 0), None, S['Rock'], L['none'], 0.22)
    # rope: from the deck lip over the edge to the tyre
    TB(g, [V3((0, 0.02, 0.5)), V3((0, 0.0, 0.58)), V3((0, -0.12, 0.62)), V3((0, -0.2, 0.62))], 0.014, S['Sand'], L['Cloth'], 0.5, 5)
    return g

def b_pontoon():
    # 2 x 2 floating pontoon section: the deck frame 0.25 m thick, 4 steel float drums and a guide hoop on the +z side. Static in the kit: the
    # waterline is at y = -1.25 (the datum is 1.25 above the water), so the floats are tall tanks. Fender rail on the +z edge.
    g = new('Berth_Pontoon_2x2')
    r = rng(g.name)
    for i in range(8):
        PL(g, (0, -0.025, -0.875 + 0.25 * i), 1.994, 0.238, 0.05, S['Wood'] if r.random() < 0.5 else S['Dock'], L['Wood'], 0.45, ch=0.01)
    for sz in (-0.85, 0.0, 0.85):
        FB(g, (-0.99, -0.22, sz - 0.07), (0.99, -0.05, sz + 0.07), S['WoodDark'], L['Wood'], 0.4)
    for sx in (-1, 1):
        FB(g, (sx * 0.91 - 0.06, -0.22, -0.99), (sx * 0.91 + 0.06, -0.05, 0.99), S['WoodDark'], L['Wood'], 0.4)
    for sx in (-0.5, 0.5):
        BX(g, (sx, -0.92, 0.0), (0.78, 1.35, 1.8), 0.1, S['Rock'], L['none'], 0.4)
    BX(g, (0, -0.2, 0.96), (1.9, 0.1, 0.1), 0.03, S['WoodDark'], L['Wood'], 0.4)
    BX(g, (0, -0.1, 1.04), (1.9, 0.14, 0.08), 0.03, S['Rock'], L['none'], 0.3)                                              # steel fender strip
    return g

BERTH_BUILDERS = ([lambda n=n: b_piling(n) for n in PILING_LENGTHS] +
                  [lambda: b_tile(1, 1), lambda: b_tile(1, 2), b_trim, b_stair, b_ramp, b_rail, b_railpost, b_roofpost, b_roof_flat,
                   b_roof_pitched, b_roof_ridge, b_bench, b_cleaningtable, b_smoker, b_crabpotrack, b_rodrack, b_namesign, b_stringlights,
                   b_stringlights_glow, b_bollard, b_cleat, b_tyrefender, b_pontoon])

def b_wall():
    # 1 m of board-and-batten shed wall, 2.4 m high, on the -z edge of its cell; the front (+z) face is at z = -0.42. Wall decor pivots there.
    g = new('Berth_Wall_1m')
    r = rng(g.name)
    for i in range(4):
        FB(g, (-0.5 + 0.25 * i + 0.004, -0.1, -0.5), (-0.5 + 0.25 * (i + 1) - 0.004, 2.4, -0.45), S['Wood'] if r.random() < 0.5 else S['Dock'], L['Wood'], 0.45)
    for i in (1, 2, 3):
        BX(g, (-0.5 + 0.25 * i, 1.15, -0.435), (0.045, 2.5, 0.03), 0.008, S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.5, 2.3, -0.45), (0.5, 2.4, -0.4), S['WoodDark'], L['Wood'], 0.4)
    FB(g, (-0.5, -0.1, -0.45), (0.5, 0.0, -0.4), S['WoodDark'], L['Wood'], 0.4)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# TACKY DECOR (design 2.1: grown-up, worn, a bit tacky; nothing cute or pastel). Props: pivot = base centre, facing +z. Wall pieces pivot at
# the back face, bottom centre, and stand out toward +z.
# ---------------------------------------------------------------------------------------------------------------------------------------
def align_y(d):
    return V3((0, 1, 0)).rotation_difference(V3(d).normalized()).to_matrix()

def XY(g, poly, depth, c, slot, layer=L['Wood'], ao=0.5, rot=None, r=0.0):
    # polygon in the (x, y) plane, extruded along z (centred), placed at c
    R = (rot if rot is not None else Matrix.Identity(3)) @ ry(90)
    g.add(H.xform(H.prism(poly, depth, r), c, R), slot, layer, ao)

def b_decor_flamingo():
    g = new('Decor_Flamingo')
    P = SLOT_PINK
    LA(g, (0, 0.82, 0), [(-0.4, 0.0), (-0.32, 0.03), (-0.16, 0.1), (0.04, 0.14), (0.2, 0.11), (0.29, 0.06), (0.31, 0.0)], P, L['none'], 0.5, sides=10,
       rot=rx(90) @ rx(8), scale=(1.0, 0.85, 1.0))
    TT(g, [V3((0, 0.9, 0.22)), V3((0, 1.02, 0.34)), V3((0, 1.16, 0.34)), V3((0, 1.3, 0.26)), V3((0, 1.43, 0.3)), V3((0, 1.52, 0.36))],
       [0.05, 0.04, 0.032, 0.03, 0.03, 0.034], P, L['none'], 0.5, 6)
    LA(g, (0, 1.53, 0.38), [(-0.045, 0.0), (-0.035, 0.032), (0.0, 0.048), (0.035, 0.035), (0.05, 0.0)], P, L['none'], 0.55, sides=8, rot=rx(90))
    LA(g, (0, 1.51, 0.43), [(0.0, 0.04), (0.06, 0.036), (0.11, 0.02), (0.15, 0.0)], S['Rock'], L['none'], 0.25, sides=6, rot=align_y((0, -0.45, 0.9)))   # black-tipped beak
    LA(g, (0, 1.505, 0.425), [(0.0, 0.045), (0.05, 0.04), (0.07, 0.03)], S['Sand'], L['none'], 0.6, sides=6, rot=align_y((0, -0.45, 0.9)))
    for sx in (-1, 1):                                                                                          # mirrored shades
        BX(g, (sx * 0.038, 1.545, 0.425), (0.036, 0.03, 0.014), 0.006, S['Rock'], L['none'], 0.25)
    FB(g, (-0.045, 1.55, 0.419), (0.045, 1.558, 0.427), S['Rock'], L['none'], 0.3)
    TB(g, [V3((-0.052, 1.552, 0.42)), V3((-0.06, 1.55, 0.38))], 0.004, S['Rock'], L['none'], 0.3, 4)
    TB(g, [V3((0.052, 1.552, 0.42)), V3((0.06, 1.55, 0.38))], 0.004, S['Rock'], L['none'], 0.3, 4)
    TB(g, [V3((-0.04, 0.0, 0.0)), V3((-0.04, 0.4, 0.01)), V3((-0.04, 0.83, 0.0))], 0.011, P, L['none'], 0.5, 5)               # standing leg
    TB(g, [V3((0.06, 0.8, 0.0)), V3((0.09, 0.62, -0.1)), V3((0.07, 0.56, -0.24)), V3((0.07, 0.62, -0.3))], 0.011, P, L['none'], 0.5, 5)   # folded leg
    FB(g, (-0.09, 0.0, -0.03), (0.01, 0.012, 0.13), P, L['none'], 0.45)                                           # webbed foot
    LA(g, (-0.04, 0.0, 0.0), [(-0.3, 0.0), (-0.28, 0.008), (0.0, 0.016)], S['Rock'], L['none'], 0.3, sides=5)       # lawn stake
    return g

def b_decor_bassplaque():
    g = new('Decor_BassPlaque')
    poly = [(-0.34, 0.06), (-0.3, 0.0), (0.3, 0.0), (0.34, 0.06), (0.34, 0.74), (0.3, 0.8), (-0.3, 0.8), (-0.34, 0.74)]
    XY(g, poly, 0.045, (0, 0, 0.0225), S['WoodDark'], L['Wood'], 0.45, r=0.008)
    poly2 = [(-0.28, 0.08), (-0.26, 0.05), (0.26, 0.05), (0.28, 0.08), (0.28, 0.72), (0.26, 0.75), (-0.26, 0.75), (-0.28, 0.72)]
    XY(g, poly2, 0.02, (0, 0, 0.05), S['Wood'], L['Wood'], 0.5)
    # the bass, built nose-along-+x in its own frame (centre at the origin) and tilted nose-up 24 degrees onto the board
    C = V3((0.0, 0.43, 0.08))
    Rf = rz(24)
    def part(geo, p, rot, slot, layer, ao):
        g.add(H.xform(geo, C + Rf @ V3(p), Rf @ rot), slot, layer, ao)
    body = scale_geo(H.lathe([(-0.3, 0.0), (-0.24, 0.045), (-0.1, 0.085), (0.06, 0.09), (0.2, 0.065), (0.3, 0.03), (0.34, 0.0)], 9)[0], (1.0, 1.0, 0.5))
    part(body, (-0.02, 0.0, 0.0), rz(-90), S['Foliage'], L['Foliage'], 0.55)
    belly = scale_geo(H.lathe([(-0.2, 0.0), (-0.12, 0.04), (0.06, 0.055), (0.2, 0.04), (0.28, 0.0)], 8)[0], (1.0, 1.0, 0.5))
    part(belly, (0.0, -0.035, 0.012), rz(-90), S['Sand'], L['Cloth'], 0.6)
    jaw = scale_geo(H.lathe([(0.0, 0.03), (0.08, 0.024), (0.14, 0.0)], 6)[0], (1.0, 1.0, 0.6))
    part(jaw, (0.3, -0.03, 0.0), rz(-90 - 28), S['Foliage'], L['Foliage'], 0.5)                                         # open mouth
    part(scale_geo(H.lathe([(0.0, 0.03), (0.04, 0.02), (0.12, 0.0)], 6)[0], (1, 1, 0.5)), (0.3, 0.0, 0.0), rz(-90), S['WoodDark'], L['none'], 0.3)
    part(H.lathe([(-0.01, 0.0), (0.0, 0.024), (0.012, 0.0)], 8)[0], (0.2, 0.05, 0.03), rx(90), WHITE, L['none'], 0.7)        # eye
    part(flat_box_geo((-0.008, -0.008, -0.004), (0.008, 0.008, 0.008)), (0.2, 0.05, 0.042), Matrix.Identity(3), S['Rock'], L['none'], 0.2)
    part(H.prism([(0.0, 0.0), (0.13, 0.12), (0.09, 0.0), (0.13, -0.12)], 0.012, 0.0), (-0.3, 0.0, 0.0), ry(90) @ Matrix.Identity(3), S['Foliage'], L['Foliage'], 0.5)   # tail
    part(H.prism([(-0.08, 0.0), (0.0, 0.11), (0.09, 0.0)], 0.01, 0.0), (0.0, 0.08, 0.0), ry(90), S['Roof'], L['none'], 0.5)                                        # dorsal fin
    BX(g, (0.2, 0.12, 0.07), (0.15, 0.1, 0.07), 0.015, S['Rock'], L['none'], 0.4)                                        # the sensor box
    FB(g, (0.14, 0.09, 0.105), (0.26, 0.15, 0.108), S['WoodDark'], L['none'], 0.3)                                       # speaker grille
    LA(g, (0.2, 0.17, 0.07), [(0.0, 0.0), (0.0, 0.016), (0.012, 0.018), (0.016, 0.0)], S['Roof'], L['none'], 0.5, sides=8, rot=rx(90))   # the BIG red button
    return g

def b_decor_shark():
    g = new('Decor_InflatableShark')
    rot = rx(90)
    body = [(-0.85, 0.0), (-0.78, 0.07), (-0.55, 0.19), (-0.15, 0.285), (0.3, 0.3), (0.65, 0.22), (0.82, 0.1), (0.86, 0.0)]
    LA(g, (0, 0.31, 0), body, S['Rock'], L['none'], 0.5, sides=12, rot=rot, scale=(1.0, 1.0, 1.0))
    LA(g, (0, 0.27, 0.0), [(-0.7, 0.0), (-0.5, 0.18), (0.0, 0.26), (0.5, 0.2), (0.74, 0.0)], WHITE, L['none'], 0.5, sides=12, rot=rot, scale=(1.0, 1.0, 0.8))
    g.add(H.xform(H.prism([(a, b) for a, b in [(-0.2, 0.0), (0.18, 0.0), (0.1, 0.18), (-0.06, 0.4)]], 0.07, 0.02), (0, 0.55, 0.05)), S['Rock'], L['none'], 0.5)     # dorsal
    g.add(H.xform(H.prism([(0.0, 0.0), (-0.3, 0.1), (-0.5, 0.34), (-0.2, 0.2), (-0.38, 0.0)], 0.06, 0.02), (0, 0.25, 0.0)), S['Rock'], L['none'], 0.5)             # tail
    for sx in (-1, 1):
        g.add(H.xform(H.prism([(0.0, 0.0), (-0.2, -0.08), (0.12, -0.16)], 0.05, 0.015), (sx * 0.27, 0.32, 0.12), rz(sx * 35)), S['Rock'], L['none'], 0.5)
        LA(g, (sx * 0.13, 0.43, 0.62), [(-0.02, 0.0), (-0.01, 0.05), (0.02, 0.05), (0.03, 0.0)], WHITE, L['none'], 0.7, sides=8, rot=rx(90))
        FBC(g, (sx * 0.13, 0.43, 0.665), (0.03, 0.03, 0.02), S['Rock'], L['none'], 0.2)
    BX(g, (0, 0.25, 0.8), (0.26, 0.05, 0.08), 0.02, S['Roof'], L['none'], 0.35)
    for i in range(6):                                                                                       # a row of teeth
        x = -0.115 + 0.046 * i
        LA(g, (x, 0.275, 0.84), [(0.0, 0.016), (0.03, 0.008), (0.045, 0.0)], WHITE, L['none'], 0.6, sides=4, rot=rx(180) @ rz(0))
    LA(g, (0, 0.6, -0.2), [(0.0, 0.03), (0.02, 0.03), (0.035, 0.015)], S['Sand'], L['none'], 0.6, sides=8)                 # the valve
    return g

def b_decor_tikitorch():
    g = new('Decor_TikiTorch')
    prof = [(-0.3, 0.0), (-0.28, 0.014), (0.0, 0.026)]
    h = 0.0
    for k in range(4):
        prof += [(h + 0.38, 0.028), (h + 0.39, 0.034), (h + 0.41, 0.034), (h + 0.42, 0.028)]
        h += 0.4
    prof += [(1.62, 0.028), (1.7, 0.07), (1.76, 0.085), (1.8, 0.07), (1.81, 0.0)]
    LA(g, (0, 0, 0), prof, S['Reed'], L['Bark'], 0.45, sides=7)
    LA(g, (0, 1.76, 0), [(0.0, 0.0), (0.0, 0.075), (0.02, 0.07), (0.03, 0.0)], S['WoodDark'], L['none'], 0.3, sides=7)       # oil in the cup
    LA(g, (0, 1.78, 0), [(0.0, 0.013), (0.07, 0.016), (0.1, 0.0)], S['Sand'], L['Cloth'], 0.6, sides=5)                       # wick
    g.extra['companion'] = 'Decor_TikiTorch_Flame'
    g.extra['light_points'] = [dict(name='TikiFlame', position=[0.0, 1.95, 0.0], color=[1.0, 0.55, 0.2], range=9.0, intensity=1.0, note='flicker; casts the mackerel-orange pool')]
    return g

def b_decor_tikitorch_flame():
    g = new('Decor_TikiTorch_Flame')
    LA(g, (0, 1.82, 0), [(0.0, 0.0), (0.0, 0.04), (0.06, 0.055), (0.14, 0.04), (0.22, 0.012), (0.27, 0.0)], S['Accent'], L['none'], 0.95, sides=7)
    g.extra['emissive'] = True
    g.extra['companion_of'] = 'Decor_TikiTorch'
    return g

def b_decor_cooler():
    g = new('Decor_BeerCooler')
    BX(g, (0, 0.17, 0), (0.62, 0.34, 0.38), 0.05, S['Roof'], L['none'], 0.5)
    BX(g, (0, 0.375, 0), (0.64, 0.07, 0.4), 0.03, WHITE, L['none'], 0.55)
    FB(g, (-0.2, 0.18, 0.19), (0.2, 0.25, 0.193), WHITE, L['none'], 0.6)                                                      # a faded stripe
    LA(g, (0.0, 0.1, 0.19), [(0.0, 0.0), (0.0, 0.03), (0.012, 0.034), (0.02, 0.0)], WHITE, L['none'], 0.6, sides=8, rot=rx(90))   # drain plug
    for sx in (-1, 1):
        TB(g, [V3((sx * 0.31, 0.3, -0.07)), V3((sx * 0.37, 0.28, -0.07)), V3((sx * 0.4, 0.2, 0.0)), V3((sx * 0.37, 0.28, 0.07)), V3((sx * 0.31, 0.3, 0.07))],
           0.014, S['Sand'], L['Cloth'], 0.6, 5)
    LA(g, (0.12, 0.445, 0.02), [(-0.075, 0.0), (-0.07, 0.034), (0.07, 0.034), (0.075, 0.03), (0.078, 0.0)], S['Rock'], L['none'], 0.5, sides=8, rot=rz(80))   # a dead can on top
    LA(g, (-0.14, 0.43, 0.08), [(-0.04, 0.0), (-0.03, 0.04), (0.03, 0.04), (0.04, 0.0)], S['Foliage'], L['none'], 0.6, sides=7)         # a lime
    return g

def b_decor_patiochair():
    g = new('Decor_PatioChair')
    W = WHITE
    BX(g, (0, 0.43, 0), (0.46, 0.045, 0.44), 0.02, W, L['none'], 0.55)
    FB(g, (-0.2, 0.34, -0.19), (0.2, 0.4, 0.2), W, L['none'], 0.5)
    for sx in (-1, 1):
        for sz in (-1, 1):
            LA(g, (sx * 0.2, 0.0, sz * 0.19), [(0.0, 0.02), (0.2, 0.026), (0.41, 0.03), (0.43, 0.0)], W, L['none'], 0.5, sides=6)
        BX(g, (sx * 0.235, 0.66, 0.02), (0.05, 0.03, 0.4), 0.012, W, L['none'], 0.55)
        FBC(g, (sx * 0.235, 0.545, 0.19), (0.035, 0.24, 0.035), W, L['none'], 0.5)
    for i in range(5):
        x = -0.17 + 0.085 * i
        zb = -0.2 - 0.02 * (1 - (x / 0.2) ** 2) * 0.0 - 0.03 * (abs(x) / 0.2) ** 2 * -1
        g.add(H.xform(flat_box_geo((-0.03, 0.0, -0.013), (0.03, 0.5, 0.013)), (x, 0.43, -0.2 - 0.02 * (abs(x) / 0.2) ** 2), rx(-10)), W, L['none'], 0.55)
    BX(g, (0, 0.945, -0.285), (0.4, 0.06, 0.035), 0.015, W, L['none'], 0.55, rot=rx(-10))
    return g

def b_decor_patiochairstack():
    g = new('Decor_PatioChairStack')
    c = b_decor_patiochair()
    for i in range(3):
        place(g, c, (0.0, 0.0 + 0.1 * i, -0.015 * i), 0.0, name='chair%d' % i)
    g.items = []
    return g

def b_decor_hammock():
    # 3 x 1: two 1.9 m posts 3.0 m apart and a striped hammock between them (a pillow at the -x end)
    g = new('Decor_Hammock')
    for sx in (-1, 1):
        BX(g, (sx * 1.5, 0.95, 0.0), (0.14, 1.9, 0.14), 0.025, S['WoodDark'], L['Wood'], 0.45)
        LA(g, (sx * 1.5, 1.8, 0.07), [(0.0, 0.0), (0.0, 0.02), (0.05, 0.022), (0.065, 0.0)], S['Rock'], L['none'], 0.45, sides=6, rot=rx(90))
    nx, nz = 9, 5
    V, N, F, UV, fsl = [], [], [], [], []
    def surf(i, j):
        t = -1.35 + 2.7 * i / (nx - 1)
        wmax = 0.95 * math.sin(math.pi * (0.1 + 0.8 * i / (nx - 1))) ** 0.7 + 0.08
        zz = -wmax / 2 + wmax * j / (nz - 1)
        y = 1.58 - 0.8 * (1 - (t / 1.5) ** 2) + 0.2 * (zz / 0.5) ** 2 * (wmax / 0.9) * 0 + 0.28 * (2 * zz / max(wmax, 0.05)) ** 2 * wmax
        return V3((t, y, zz))
    P = [[surf(i, j) for j in range(nz)] for i in range(nx)]
    for side in (1, -1):
        base = len(V)
        for i in range(nx):
            for j in range(nz):
                p = P[i][j]
                di = (P[min(i + 1, nx - 1)][j] - P[max(i - 1, 0)][j])
                dj = (P[i][min(j + 1, nz - 1)] - P[i][max(j - 1, 0)])
                n = dj.cross(di).normalized()
                if n.y < 0:
                    n = -n
                V.append(p + n * (0.0 if side == 1 else -0.016)); N.append(n * side)
        for i in range(nx - 1):
            for j in range(nz - 1):
                a = base + i * nz + j
                f = [a, a + 1, a + nz + 1, a + nz]
                if side == -1:
                    f = list(reversed(f))
                F.append(f)
                UV.append([(V[k].x, V[k].z) for k in f])
                fsl.append((S['Accent'] if i % 2 == 0 else S['Sand'], L['Cloth']))
    g.add((V, N, F, UV), S['Accent'], L['Cloth'], 0.55, fsl)
    for sx, e in ((-1, P[0]), (1, P[-1])):
        for j in (0, nz - 1):
            TB(g, [e[j] + V3((0, 0.0, 0.0)), V3((sx * 1.5 - sx * 0.07, 1.78, 0.0))], 0.012, S['Sand'], L['Cloth'], 0.5, 5)
    BX(g, (-1.05, 0.86, 0.0), (0.4, 0.12, 0.28), 0.05, WHITE, L['Cloth'], 0.6, rot=rz(8))
    return g

def b_decor_nofishing():
    # a municipal sign on a post; "NO" has been painted over. The paint is a different cream, with drips.
    g = new('Decor_NoFishingSign')
    BX(g, (0, 0.95, 0.0), (0.1, 1.9, 0.1), 0.02, S['WoodDark'], L['Wood'], 0.45)
    BX(g, (0, 1.5, 0.07), (0.94, 0.62, 0.035), 0.03, WHITE, L['Plaster'], 0.55)
    for (lo, hi) in (((-0.43, 1.2, 0.083), (0.43, 1.215, 0.09)), ((-0.43, 1.785, 0.083), (0.43, 1.8, 0.09)),
                     ((-0.44, 1.2, 0.083), (-0.425, 1.8, 0.09)), ((0.425, 1.2, 0.083), (0.44, 1.8, 0.09))):
        FB(g, lo, hi, S['Roof'], L['none'], 0.55)
    H.sign_text(g, 'FISHING', 0.15, (0.0, 1.36, 0.093), Matrix.Identity(3), S['Roof'], depth=0.01)
    H.sign_text(g, 'NO', 0.24, (0.0, 1.63, 0.093), Matrix.Identity(3), S['Roof'], depth=0.01)
    BX(g, (0.0, 1.63, 0.1), (0.5, 0.26, 0.014), 0.006, S['Sand'], L['Plaster'], 0.62, rot=rz(1.2))                      # the paint-over
    FB(g, (-0.22, 1.51, 0.106), (0.22, 1.515, 0.109), S['Wood'], L['none'], 0.6)
    FB(g, (-0.2, 1.745, 0.106), (0.24, 1.75, 0.109), S['Wood'], L['none'], 0.6)
    for dx, ln in ((-0.15, 0.1), (0.06, 0.16), (0.19, 0.07)):
        TB(g, [V3((dx, 1.5, 0.107)), V3((dx, 1.5 - ln, 0.107))], 0.006, S['Sand'], L['none'], 0.62, 4)
    FB(g, (0.3, 1.24, 0.1), (0.4, 1.26, 0.102), S['Rock'], L['none'], 0.3)                                                # a sticker scrape
    return g

def b_decor_neonbait():
    # wall sign (pivot: back, bottom centre): a steel can with an angle bracket; the tubes are the _Glow object
    g = new('Decor_NeonBait')
    BX(g, (0, 0.27, 0.05), (1.06, 0.54, 0.1), 0.03, S['Rock'], L['none'], 0.4)
    FB(g, (-0.5, 0.02, 0.1), (0.5, 0.04, 0.115), S['WoodDark'], L['none'], 0.3)
    FB(g, (-0.5, 0.5, 0.1), (0.5, 0.52, 0.115), S['WoodDark'], L['none'], 0.3)
    for sx in (-1, 1):
        for sy in (0.12, 0.42):
            LA(g, (sx * 0.42, sy, 0.115), [(0.0, 0.0), (0.0, 0.024), (0.014, 0.026), (0.02, 0.0)], S['Sand'], L['none'], 0.5, sides=6, rot=rx(90))
    g.extra['companion'] = 'Decor_NeonBait_Glow'
    g.extra['light_points'] = [dict(name='NeonBait', position=[0.0, 0.3, 0.3], color=[1.0, 0.25, 0.3], range=6.0, intensity=0.8, note='flickers like a dying ballast; drop to 0 on ~1 percent of nights')]
    return g

def b_decor_neonbait_glow():
    g = new('Decor_NeonBait_Glow')
    H.sign_text(g, 'BAIT', 0.27, (0.0, 0.32, 0.135), Matrix.Identity(3), S['Accent'], depth=0.03)
    pts = [V3((-0.4 + 0.1 * i, 0.1 + 0.03 * (-1) ** i, 0.14)) for i in range(9)]
    TB(g, pts, 0.01, S['Roof'], L['none'], 0.95, 5)                                                                       # the worm
    g.extra['emissive'] = True
    g.extra['companion_of'] = 'Decor_NeonBait'
    return g

def b_decor_bathtub():
    # 1 x 2 garden planter: a claw-foot bath, soil, three shrubs and plastic flowers
    g = new('Decor_BathtubPlanter')
    for sx in (-1, 1):
        for sz in (-1, 1):
            LA(g, (sx * 0.62, 0.0, sz * 0.25), [(0.0, 0.0), (0.0, 0.05), (0.06, 0.07), (0.14, 0.05), (0.2, 0.06), (0.22, 0.0)], S['Rock'], L['none'], 0.4, sides=7)
    BX(g, (0, 0.38, -0.32), (1.5, 0.46, 0.09), 0.04, WHITE, L['none'], 0.5)
    BX(g, (0, 0.38, 0.32), (1.5, 0.46, 0.09), 0.04, WHITE, L['none'], 0.5)
    BX(g, (-0.7, 0.38, 0.0), (0.1, 0.46, 0.55), 0.04, WHITE, L['none'], 0.5)
    BX(g, (0.7, 0.38, 0.0), (0.1, 0.46, 0.55), 0.04, WHITE, L['none'], 0.5)
    FB(g, (-0.66, 0.15, -0.28), (0.66, 0.2, 0.28), S['WoodDark'], L['none'], 0.4)
    soil = H.lathe([(0.0, 0.0), (0.0, 0.5), (0.05, 0.6), (0.08, 0.0)], 10)[0]
    A(g, scale_geo(soil, (1.28, 1.0, 0.82)), (0, 0.46, 0), None, S['WoodDark'], L['Dirt'], 0.5)
    r = rng(g.name)
    for x, rad, sl in ((-0.38, 0.22, S['Foliage']), (0.1, 0.27, S['FoliageDark']), (0.46, 0.2, S['Foliage'])):
        A(g, H.blob(rad, r, (1.0, 0.85, 1.0), subdiv=1, rough=0.25), (x, 0.62 + rad * 0.4, r.uniform(-0.05, 0.05)), None, sl, L['Foliage'], 0.6)
    for x, z, c in ((-0.2, 0.12, SLOT_PINK), (0.28, -0.1, S['Accent']), (0.0, -0.17, WHITE), (-0.5, -0.1, S['Accent'])):
        TB(g, [V3((x, 0.64, z)), V3((x + 0.02, 0.8, z))], 0.006, S['Foliage'], L['none'], 0.5, 4)
        LA(g, (x + 0.02, 0.8, z), [(0.0, 0.0), (0.0, 0.05), (0.012, 0.055), (0.02, 0.0)], c, L['none'], 0.7, sides=6)
    BX(g, (0.78, 0.7, 0.0), (0.05, 0.05, 0.05), 0.012, S['Rock'], L['none'], 0.4)
    TB(g, [V3((0.78, 0.52, 0.0)), V3((0.78, 0.66, 0.0)), V3((0.7, 0.72, 0.0))], 0.016, S['Rock'], L['none'], 0.4, 5)       # the tap
    return g

DECOR_BUILDERS = [b_decor_flamingo, b_decor_bassplaque, b_decor_shark, b_decor_tikitorch, b_decor_tikitorch_flame, b_decor_cooler,
                  b_decor_patiochair, b_decor_patiochairstack, b_decor_hammock, b_decor_nofishing, b_decor_neonbait, b_decor_neonbait_glow, b_decor_bathtub]

# ---------------------------------------------------------------------------------------------------------------------------------------
# THE BERTH SITE (design 3.8, 5.3.1): the east basin's south shore. Unity metres. The lookout (LOOKOUT x 34.7-39.5) and the East Ledge jetty
# (JETTY x 54.4-57) are on land to the south-west and south-east; the coast is z -45 (BeachY above it, SeabedY = -0.25 x (z + 45) below it).
# ---------------------------------------------------------------------------------------------------------------------------------------
PLOT = dict(x0=41.0, x1=53.0, z0=-47.0, z1=-28.0)           # the crew's plot (rectangle)
MOOR_Z = -28.0                                              # the mooring line = the pontoon's north edge = the plot's north edge
PONTOON = dict(x0=41.0, x1=53.0, z0=-30.0, z1=-28.0)        # fixed, always there; its top is the tug's deck height
BUILD_Z1 = -30.0                                            # free building stops at the pontoon's south edge
DATUM_N0 = DATUM_TUG                                        # berth level 0 = 1.25
BOLLARDS = [(41.5, -28.45), (44.5, -28.45), (47.5, -28.45), (50.0, -28.45)]   # x, z on the pontoon top
GANGWAY_LANDING = dict(x=52.5, z=-28.0, width=2.0)          # where the tug's gangway lands (tug local z -7)
TUG_BERTH = dict(position=(45.5, 0.0, -25.2), yaw=270.0)    # bow west, port side to the pontoon; gangway at world x = 45.5 + 7 = 52.5
LANE = dict(x0=33.0, x1=57.0, z0=-28.0, z1=-15.0)           # the approach lane (keep clear: no piece may intersect it)
LANE_BUOYS = [(33.0, -16.0, 'W'), (33.0, -21.0, 'W'), (33.0, -26.0, 'W'), (57.0, -16.0, 'E'), (57.0, -21.0, 'E'), (57.0, -26.0, 'E')]

def land_y(x, z):
    return H.land_y(x, z)

def piling_for(datum_y, x, z):
    ground = land_y(x, z)
    need = datum_y - ground + 0.3
    for n in PILING_LENGTHS:
        if n >= need:
            return n
    return PILING_LENGTHS[-1]

def b_site_laneboy(side):
    g = new('Site_LaneBuoy_%s' % ('Port' if side == 'W' else 'Starboard'))
    col = S['Roof'] if side == 'W' else S['Foliage']
    LA(g, (0, 0, 0), [(-0.45, 0.0), (-0.4, 0.2), (-0.2, 0.33), (0.1, 0.35), (0.3, 0.3), (0.5, 0.18), (0.62, 0.1), (0.9, 0.05), (1.25, 0.0)], col, L['none'], 0.6,
       sides=10, ring_slots=[col, col, col, WHITE, WHITE, col, col, col])
    LA(g, (0, 1.2, 0), [(0.0, 0.0), (0.0, 0.1), (0.04, 0.12), (0.1, 0.0)], col, L['none'], 0.6, sides=8)                       # topmark
    return g

def b_site_mooringline():
    # The fixed mooring line, authored in WORLD coordinates (place at the Unity origin): six pontoon sections x 41..53, z -30..-28, top at 1.25,
    # four bollards, seven tyre fenders along the north edge, a steel gangway landing plate at x 51.5..53.5. Static (no bobbing needed).
    g = new('Site_MooringLine')
    pt = b_pontoon()
    for i in range(6):
        place(g, pt, (42.0 + 2.0 * i, DATUM_TUG, -29.0), 0.0, name='PontoonSection%d' % i, kind='pontoon', footprint=[2, 2])
    bol = b_bollard()
    for k, (x, z) in enumerate(BOLLARDS):
        place(g, bol, (x, DATUM_TUG, z), 0.0, name='Bollard%d' % k, kind='bollard')
    ty = b_tyrefender()
    for k in range(7):
        place(g, ty, (41.9 + 1.5 * k, DATUM_TUG, MOOR_Z - 0.5), 0.0, name='Fender%d' % k, kind='fender')
    FB(g, (51.0, DATUM_TUG, -29.6), (53.0, DATUM_TUG + 0.03, -27.8), S['Rock'], L['none'], 0.4)
    for k, x in enumerate((51.75, 52.25, 52.75, 53.25)):
        FB(g, (x - 0.02, DATUM_TUG + 0.03, -29.6), (x + 0.02, DATUM_TUG + 0.04, -27.8), S['WoodDark'], L['none'], 0.4)   # grip bars
    cl = b_cleat()
    place(g, cl, (51.0, DATUM_TUG, -28.6), 90.0, name='LandingCleat', kind='cleat')
    g.items.append(dict(name='MooringLine', kind='info', pontoon=PONTOON, bollards=[[x, DATUM_TUG, z] for x, z in BOLLARDS],
                        gangway_landing=[GANGWAY_LANDING['x'], DATUM_TUG, GANGWAY_LANDING['z']]))
    return g

def starter_landing():
    # Placements (kit model name, centre x, datum y, centre z, yaw) of the default landing: stair from the beach, a 2 m walkway to the pontoon,
    # a 3 x 4 side platform with a bench, rod rack and lanterns, a name board on the shore and a west rail. Pilings under every tile joint.
    P = []
    D = DATUM_TUG
    for x in (51.5, 52.5):
        P.append(('Berth_Stair_1m', x, D, -46.0, 0.0))
    for x in (51.5, 52.5):
        for z in (-44.0, -42.0, -40.0, -38.0, -36.0, -34.0, -32.0):
            P.append(('Berth_DeckTile_1x2', x, D, z, 0.0))
        P.append(('Berth_DeckTile_1x1', x, D, -30.5, 0.0))
    for x in (48.5, 49.5, 50.5):
        for z in (-34.0, -32.0):
            P.append(('Berth_DeckTile_1x2', x, D, z, 0.0))
        P.append(('Berth_DeckTile_1x1', x, D, -30.5, 0.0))
    # pilings (datum-relative bodies; the length is picked from the ground under them)
    for (x, z) in ((51.0, -44.6), (53.0, -44.6), (51.0, -41.0), (53.0, -41.0), (51.0, -37.0), (53.0, -37.0), (51.0, -33.0), (53.0, -33.0),
                   (48.1, -34.9), (48.1, -31.2), (50.0, -34.9), (50.0, -31.2), (51.0, -30.7), (53.0, -30.7)):
        P.append(('Berth_Piling_%dm' % piling_for(D, x, z), x, D, z, 0.0))
    for z in range(-44, -31):                                                   # west rail, cells z -44.5 .. -31.5 (13 rails) + a post at the foot
        P.append(('Berth_Rail_1m', 51.5, D, z - 0.5, 270.0))
    P.append(('Berth_RailPost', 51.5, D, -44.5, 270.0))
    P.append(('Berth_Bench_2m', 49.0, D, -34.5, 0.0))
    P.append(('Berth_RodRack', 48.5, D, -31.5, 0.0))
    P.append(('Berth_Cleat', 49.5, D, -31.2, 90.0))
    P.append(('Berth_NameSign', 49.5, land_y(49.5, -46.4), -46.4, 0.0))
    P.append(('Stand_Lantern', 51.2, D, -44.3, 0.0))
    P.append(('Stand_Lantern', 50.7, D, -31.35, 0.0))
    P.append(('Stand_Lantern', 52.4, D, -29.4, 0.0))
    P.append(('Stand_RopeCoil', 44.6, D, -29.0, 0.0))
    return P

FP = {}   # footprints in cells (x, z) for the manifest

BUILDERS_ALL = None

def build_all():
    out = []
    meta = {}
    def add(fn, kind, fp=None, ground=True, **kw):
        g = fn()
        g.extra.update(kind=kind, ground=ground, footprint=fp, **kw)
        out.append(g)
    for fn in STAND_BUILDERS:
        add(fn, 'stand')
    ground_off = ('Berth_Piling', 'Berth_DeckTile', 'Berth_DeckTrim', 'Berth_Stair', 'Berth_Ramp', 'Berth_Roof', 'Berth_Pontoon', 'Berth_TyreFender')
    fps = {'Berth_DeckTile_1x1': (1, 1), 'Berth_DeckTile_1x2': (1, 2), 'Berth_DeckTrim_1m': (1, 1), 'Berth_Stair_1m': (1, 2), 'Berth_Ramp_1m': (1, 4),
           'Berth_Rail_1m': (1, 1), 'Berth_RailPost': (1, 1), 'Berth_RoofPost': (1, 1), 'Berth_RoofPanel_Flat': (1, 1), 'Berth_RoofPanel_Pitched': (1, 1),
           'Berth_RoofRidge_1m': (1, 1), 'Berth_Bench_2m': (2, 1), 'Berth_CleaningTable': (2, 1), 'Berth_Smoker': (1, 1), 'Berth_CrabPotRack': (1, 1),
           'Berth_RodRack': (1, 1), 'Berth_NameSign': (2, 1), 'Berth_StringLights_2m': (2, 1), 'Berth_Bollard': (1, 1), 'Berth_Cleat': (1, 1),
           'Berth_TyreFender': (1, 1), 'Berth_Pontoon_2x2': (2, 2), 'Berth_Wall_1m': (1, 1), 'Decor_Hammock': (3, 1), 'Decor_BathtubPlanter': (1, 2),
           'Decor_PatioChair': (1, 1), 'Decor_PatioChairStack': (1, 1), 'Decor_InflatableShark': (1, 2)}
    for fn in BERTH_BUILDERS + [b_wall]:
        g = fn()
        g.extra.update(kind='berth', ground=not g.name.startswith(ground_off), footprint=fps.get(g.name) or (1, 1))
        out.append(g)
    for fn in DECOR_BUILDERS:
        g = fn()
        g.extra.update(kind='decor', ground=True, footprint=fps.get(g.name) or (1, 1))
        out.append(g)
    return out

# ---------------------------------------------------------------------------------------------------------------------------------------
# AO bake for the kit: the harbour's bake (fg_harbour.bake_ao, cosine rays, back faces pass, 1 m) with a ground plane at y = 0 instead of the
# terrain, run per model (a model's own geometry plus the plane). Deck-family models run without the plane.
# ---------------------------------------------------------------------------------------------------------------------------------------
def bake_kit(groups, rays=40, dist=0.8):
    from mathutils.bvhtree import BVHTree
    dirs = H.hemisphere_dirs(rays)
    out = []
    for g in groups:
        verts = [tuple(v) for v in g.V]
        polys = [tuple(f) for f in g.F]
        if g.extra.get('ground', True):
            b = len(verts)
            verts += [(-12, 0.0, -12), (-12, 0.0, 12), (12, 0.0, 12), (12, 0.0, -12)]
            polys.append((b, b + 1, b + 2, b + 3))
        bvh = BVHTree.FromPolygons(verts, polys, all_triangles=False)
        if g.extra.get('ground', True):
            # make sure the plane faces up
            pass
        ao = []
        for p, n in zip(g.V, g.N):
            t = n.orthogonal().normalized()
            bb = n.cross(t)
            org = p + n * 0.004
            occ = 0.0
            for d0 in dirs:
                d = t * d0.x + n * d0.y + bb * d0.z
                o, left = org, dist
                for _ in range(6):
                    loc, nrm, idx, dd = bvh.ray_cast(o, d, left)
                    if loc is None:
                        break
                    if nrm.dot(d) > 0.0:
                        o = loc + d * 0.002
                        left -= dd + 0.002
                        if left <= 0.0:
                            break
                        continue
                    occ += 1.0 - 0.5 * ((dist - left + dd) / dist)
                    break
            ao.append(max(0.0, 1.0 - occ / len(dirs)))
        out.append(ao)
    return out

# ---------------------------------------------------------------------------------------------------------------------------------------
# Layouts for the previews: the starter landing (above), a catalogue sheet per family, and "what a crew might build".
# ---------------------------------------------------------------------------------------------------------------------------------------
def example_dock():
    P = list(starter_landing())
    D = DATUM_TUG
    def tiles(xs, zs):
        for x in xs:
            for z in zs:
                P.append(('Berth_DeckTile_1x2', x, D, z, 0.0))
    tiles([41.5 + i for i in range(7)], [-39.0, -37.0, -35.0, -33.0, -31.0])
    for (x, z) in ((41.0, -39.8), (44.5, -39.8), (48.0, -39.8), (41.0, -35.5), (44.5, -35.5), (41.0, -31.3), (44.5, -31.3), (48.0, -37.0)):
        P.append(('Berth_Piling_%dm' % piling_for(D, x, z), x, D, z, 0.0))
    for k in range(10):
        P.append(('Berth_Rail_1m', 41.5, D, -39.5 + k, 270.0))
    P.append(('Berth_RailPost', 41.5, D, -39.5, 270.0))
    for k in range(7):
        P.append(('Berth_Rail_1m', 41.5 + k, D, -39.5, 180.0))
    P.append(('Berth_RailPost', 47.5, D, -39.5, 180.0))
    for (x, z, yaw) in ((45.5, -36.5, 0.0), (45.5, -37.5, 90.0), (42.5, -37.5, 180.0), (42.5, -36.5, 270.0), (44.5, -36.5, 0.0), (43.5, -37.5, 180.0)):
        P.append(('Berth_RoofPost', x, D, z, yaw))
    for x in (42.5, 43.5, 44.5, 45.5):
        P.append(('Berth_RoofPanel_Pitched', x, D, -37.5, 0.0))
        P.append(('Berth_RoofPanel_Pitched', x, D, -36.5, 180.0))
        P.append(('Berth_RoofRidge_1m', x, D, -37.0 + 0.0, 0.0))
        P.append(('Berth_Wall_1m', x, D, -37.5, 0.0))
    P += [('Berth_CleaningTable', 43.0, D, -36.9, 0.0), ('Berth_Smoker', 45.3, D, -37.0, 0.0), ('Berth_CrabPotRack', 41.9, D, -34.6, 270.0),
          ('Berth_RodRack', 47.3, D, -32.0, 0.0), ('Berth_Bench_2m', 46.5, D, -39.0, 180.0),
          ('Berth_StringLights_2m', 43.5, D + 0.0, -35.7, 0.0), ('Berth_StringLights_2m', 45.5, D + 0.0, -35.7, 0.0),
          ('Decor_BassPlaque', 43.0, D + 1.2, -37.4, 0.0), ('Decor_NeonBait', 44.9, D + 1.0, -37.4, 0.0),
          ('Decor_Hammock', 44.5, D, -32.3, 0.0), ('Decor_BeerCooler', 42.4, D, -35.5, 15.0), ('Decor_PatioChair', 47.0, D, -34.2, 200.0),
          ('Decor_PatioChair', 46.1, D, -34.6, 160.0), ('Decor_TikiTorch', 41.4, D, -31.2, 0.0), ('Decor_TikiTorch', 41.4, D, -39.2, 0.0),
          ('Decor_TikiTorch', 47.6, D, -39.2, 0.0), ('Decor_BathtubPlanter', 47.2, D, -36.2, 90.0),
          ('Decor_Flamingo', 50.2, land_y(50.2, -45.6), -45.6, 20.0), ('Decor_NoFishingSign', 46.8, land_y(46.8, -46.4), -46.4, 0.0),
          ('Decor_InflatableShark', 43.5, D, -30.7, 0.0), ('Stand_Lantern', 41.5, D, -37.9, 0.0), ('Stand_CrateSeat', 46.2, D, -31.4, 20.0),
          ('Stand_TackleBox', 46.9, D, -31.5, 40.0), ('Stand_BaitBucket', 42.2, D, -31.6, 0.0), ('Stand_RodHolder', 41.5, D, -33.6, 270.0)]
    return P

SPACING = 0.9

def sheet_layout(rows, lib, start=(0.0, 0.0), zstep=3.3):
    # rows: lists of model names. x runs NEGATIVE (the camera looks toward -z, where Unity's +x is on the left), so a row reads left to right.
    P, labs = [], []
    z = start[1]
    for row in rows:
        x = start[0]
        for ent in row:
            name, dy = (ent, 0.0) if isinstance(ent, str) else (ent[0], ent[1])
            g = lib[name]
            fp = g.extra.get('footprint') or (1, 1)
            w = max(fp[0], 1.0)
            cx = x - w / 2
            P.append((name, cx, dy, z, 0.0))
            labs.append((name.replace('Berth_', '').replace('Stand_', '').replace('Decor_', ''), cx, z - max(fp[1], 1.0) / 2 - 0.2))
            x -= w + SPACING
        z -= zstep
    return P, labs

def compose(lib, P, name='pv'):
    g = new(name)
    for ent in P:
        nm, x, y, z, yaw = ent
        place(g, lib[nm], (x, y, z), yaw, name=nm)
    return g

# ---------------------------------------------------------------------------------------------------------------------------------------
# Preview scenes (Eevee, 1280 x 720).
# ---------------------------------------------------------------------------------------------------------------------------------------
def srgb(c):
    return H.srgb_to_lin(c) + (1.0,)

def mat_flat(name, col, rough=0.9, emit=0.0, alpha=1.0):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = srgb(col)
    b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = srgb(col)
        b.inputs['Emission Strength'].default_value = emit
    if alpha < 1.0:
        b.inputs['Alpha'].default_value = alpha
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    return m

def kit_ground(y=0.0, size=60.0, col=(0.74, 0.72, 0.68), name='FG_Kit_Ground'):
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    me = bpy.data.meshes.new(name)
    s = size
    me.from_pydata([tuple(to_b(( -s, y, -s))), tuple(to_b((s, y, -s))), tuple(to_b((s, y, s))), tuple(to_b((-s, y, s)))], [], [(0, 3, 2, 1)])
    ob = bpy.data.objects.new(name, me)
    H.collection('Preview').objects.link(ob)
    me.materials.append(mat_flat(name, col, 0.95))
    return ob

def to_b(v):
    return tuple(H.to_blender(v))

def scene_hide_site(hide=True):
    for nm in ('FG_Preview_Terrain', 'FG_Preview_Water'):
        o = bpy.data.objects.get(nm)
        if o:
            o.hide_render = hide
            o.hide_viewport = hide

def preview_mat64():
    # preview only: fg_harbour's preview palette has 16 colours (the Blender ramp holds 32 stops), so the preview shows slot 48 (FlamingoPink)
    # through the unused slot 15; show_pv remaps the composed preview group's slot 48 to 15
    m = H.preview_material()
    ramp = next(n for n in m.node_tree.nodes if n.type == 'VALTORGB')
    ramp.color_ramp.elements[15].color = H.srgb_to_lin(PINK_RGB) + (1.0,)
    return m

def show_pv(g, bake='kit', emissive=False):
    g.SL = [(15 if sl == SLOT_PINK else sl, ly) for sl, ly in g.SL]
    ob = H.make_object(g)
    if bake == 'kit':
        raw = bake_kit([g], rays=24)[0]
    else:
        raw = H.bake_ao([g], rays=24)[0]
    H.write_ao(ob, g, raw)
    ob.data.materials.append(preview_mat64())
    return ob

def glow_object(g, pos=(0, 0, 0)):
    ob = H.make_object(g)
    ob.data.materials.append(mat_flat('FG_Glow', (1.0, 0.7, 0.3), 0.5, emit=6.0))
    return ob

def label(text, x, z, size=0.34, y=0.01):
    cu = bpy.data.curves.new('lab', 'FONT')
    cu.body = text; cu.size = size; cu.align_x = 'CENTER'; cu.align_y = 'TOP'
    ob = bpy.data.objects.new('lab', cu)
    H.collection('Preview').objects.link(ob)
    ob.location = to_b((x, y, z))
    ob.rotation_euler = (0.0, 0.0, math.pi)       # lies flat, reads from the +z side (Unity) looking toward -z
    ob.data.materials.append(mat_flat('FG_Label', (0.12, 0.12, 0.14), 1.0))
    return ob

def clear_pv():
    for o in list(bpy.data.objects):
        if o.name.startswith(('pv', 'lab', 'Stand_', 'Berth_', 'Decor_', 'Site_', 'FG_Tug', 'FG_Lane', 'FG_Grid', 'FG_Glow')):
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me and getattr(me, 'users', 1) == 0:
                try:
                    bpy.data.meshes.remove(me)
                except Exception:
                    pass

def checker_grid(x0, x1, z0, z1, y=-0.22):
    name = 'FG_Grid'
    verts, faces, cols = [], [], []
    k = 0
    for xi in range(int(x0), int(x1)):
        for zi in range(int(z0), int(z1)):
            a = len(verts)
            for dx, dz in ((0, 0), (1, 0), (1, 1), (0, 1)):
                verts.append(to_b((xi + dx, y, zi + dz)))
            faces.append((a, a + 3, a + 2, a + 1))
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    H.collection('Preview').objects.link(ob)
    mats = [mat_flat('FG_GridA', (0.80, 0.78, 0.72), 0.95), mat_flat('FG_GridB', (0.70, 0.68, 0.64), 0.95)]
    for m in mats:
        me.materials.append(m)
    i = 0
    for xi in range(int(x0), int(x1)):
        for zi in range(int(z0), int(z1)):
            me.polygons[i].material_index = (xi + zi) % 2
            i += 1
    return ob

def tug_ghost():
    # preview only: the tug's footprint at its berth (deck outline from fg_harbour.tug_outline, the deckhouse and wheelhouse boxes)
    pos = V3(TUG_BERTH['position'])
    R = ry(TUG_BERTH['yaw'])
    def W(p):
        return pos + R @ V3(p)
    outline = H.tug_outline(0.5)
    col = mat_flat('FG_TugGhost', (0.92, 0.55, 0.2), 0.8, alpha=0.3)
    verts, faces = [], []
    top = [W((x, H.TUG_DECK, z)) for x, z in outline]
    bot = [W((x * 0.8, -0.9, z)) for x, z in outline]
    n = len(outline)
    c = sum(top, V3()) / n
    verts = [to_b(p) for p in top] + [to_b(p) for p in bot] + [to_b(c)]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, 2 * n))
        faces.append((i, n + i, n + j, j))
    me = bpy.data.meshes.new('FG_Tug')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new('FG_Tug', me)
    H.collection('Preview').objects.link(ob)
    me.materials.append(col)
    for nm, (z0, z1, hw), y0, y1 in (('house', H.TUG_HOUSE, H.TUG_DECK, H.TUG_HOUSE_TOP), ('wheel', H.TUG_WH, H.TUG_HOUSE_TOP, H.TUG_WH_TOP)):
        vs, fs = [], []
        for (x, y, z) in ((-hw, y0, z0), (hw, y0, z0), (hw, y0, z1), (-hw, y0, z1), (-hw, y1, z0), (hw, y1, z0), (hw, y1, z1), (-hw, y1, z1)):
            vs.append(to_b(W((x, y, z))))
        fs = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        m2 = bpy.data.meshes.new('FG_Tug_' + nm)
        m2.from_pydata(vs, [], fs)
        m2.update()
        o2 = bpy.data.objects.new('FG_Tug_' + nm, m2)
        H.collection('Preview').objects.link(o2)
        m2.materials.append(col)
    # gangway marker
    gp = W((-TUG_HALF, H.TUG_DECK, H.TUG_GANGWAY_Z))
    return ob

TUG_HALF = H.TUG_B / 2

def render(path, eye, target, lens=32, sun=None):
    H.render_view(path, eye, target, lens=lens, levels=64)

def add_glows(lib, P):
    for nm, x, y, z, yaw in P:
        comp = lib[nm].extra.get('companion')
        if comp:
            g2 = new('pv_glow_' + nm)
            place(g2, lib[comp], (x, y, z), yaw)
            glow_object(g2)

def sheet(lib, rows, path, eye, target, lens, zstep, ground, grid=None, wall_decor=False, label_size=0.26):
    P, labs = sheet_layout(rows, lib, zstep=zstep)
    if wall_decor:
        P2 = []
        for nm, x, y, z, yaw in P:
            if nm in ('Decor_BassPlaque', 'Decor_NeonBait'):
                P2.append((nm, x, 0.9 if nm == 'Decor_BassPlaque' else 0.8, z - 0.4, yaw))
                P2.append(('Berth_Wall_1m', x, 0.0, z, yaw))
            else:
                P2.append((nm, x, y, z, yaw))
        P = P2
    pv = compose(lib, P, 'pv_sheet')
    pv.extra['ground'] = ground
    show_pv(pv, 'kit')
    add_glows(lib, P)
    if grid:
        checker_grid(*grid)
    else:
        kit_ground(0.0, 60)
    ly = -0.21 if grid else 0.01
    for text, x, z in labs:
        label(text, x, z, label_size, y=ly)
    if grid:
        kit_ground(-7.8, 80)
    render(path, eye, target, lens=lens)
    clear_pv()
    for nm in ('FG_Kit_Ground', 'FG_Grid'):
        o = bpy.data.objects.get(nm)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)

def build_previews(lib, site_groups, out_dir, only=None):
    os.makedirs(out_dir, exist_ok=True)
    H.preview_scene()
    scene_hide_site(True)
    want = lambda k: only is None or k in only
    if want('structure'):
        rows = [['Berth_DeckTile_1x1', 'Berth_DeckTile_1x2', 'Berth_DeckTrim_1m', ('Berth_Stair_1m', 1.0), ('Berth_Ramp_1m', 1.0), 'Berth_Cleat'],
                ['Berth_Rail_1m', 'Berth_RailPost', 'Berth_RoofPost', 'Berth_Wall_1m', ('Berth_RoofPanel_Flat', -2.5), ('Berth_RoofPanel_Pitched', -2.5), ('Berth_RoofRidge_1m', -3.0)],
                [('Berth_Piling_2m', 2.0), ('Berth_Piling_3m', 3.0), ('Berth_Piling_4m', 4.0), ('Berth_Piling_5m', 5.0), ('Berth_Piling_6m', 6.0), ('Berth_Piling_7m', 7.0)]]
        sheet(lib, rows, os.path.join(out_dir, 'kit_berth_structure.png'), (-6.2, 10.5, 14.5), (-6.4, 1.8, -4.4), 32, 3.6, False, grid=(-18, 2, -12, 3))
    if want('furniture'):
        rows = [['Berth_Bench_2m', 'Berth_CleaningTable', 'Berth_Smoker', 'Berth_CrabPotRack', 'Berth_RodRack'],
                ['Berth_NameSign', 'Berth_StringLights_2m', 'Berth_Bollard', 'Berth_TyreFender', 'Berth_Pontoon_2x2']]
        sheet(lib, rows, os.path.join(out_dir, 'kit_berth_furniture.png'), (-7.0, 8.5, 9.5), (-7.0, 0.6, -2.4), 31, 4.2, False, grid=(-18, 2, -8, 3))
    if want('standdecor'):
        srows = [['Stand_RodHolder', 'Stand_BaitBucket', 'Stand_CrateSeat', 'Stand_TackleBox', 'Stand_Lantern', 'Stand_RopeCoil', 'Decor_TikiTorch', 'Decor_Flamingo'],
                 ['Decor_BeerCooler', 'Decor_PatioChair', 'Decor_PatioChairStack', 'Decor_NoFishingSign', 'Decor_InflatableShark', 'Decor_BathtubPlanter'],
                 ['Decor_Hammock', 'Decor_BassPlaque', 'Decor_NeonBait']]
        sheet(lib, srows, os.path.join(out_dir, 'kit_stand_and_decor.png'), (-7.6, 9.5, 12.5), (-7.6, 0.4, -3.2), 33, 3.1, True, wall_decor=True, label_size=0.24)
    scene_hide_site(False)
    def site_scene(P, name):
        g = compose(lib, P, 'pv_landing')
        place(g, site_groups['Site_MooringLine'], (0, 0, 0), 0.0, name='MooringLine')
        for x, z, sd in LANE_BUOYS:
            place(g, site_groups['Site_LaneBuoy_Port' if sd == 'W' else 'Site_LaneBuoy_Starboard'], (x, 0.0, z), 0.0)
        show_pv(g, 'site')
        add_glows(lib, P)
        tug_ghost()
    if want('landing'):
        site_scene(starter_landing(), 'landing')
        render(os.path.join(out_dir, 'starter_landing_tug_moored.png'), (63.0, 9.5, -15.0), (47.5, 0.9, -35.0), lens=30)
        clear_pv()
        for o in list(bpy.data.objects):
            if o.name.startswith('FG_Tug'):
                bpy.data.objects.remove(o, do_unlink=True)
    if want('dock'):
        site_scene(example_dock(), 'dock')
        render(os.path.join(out_dir, 'example_crew_dock.png'), (60.0, 11.0, -17.0), (46.5, 1.6, -37.0), lens=32)

# ---------------------------------------------------------------------------------------------------------------------------------------
# Manifest and main.
# ---------------------------------------------------------------------------------------------------------------------------------------
NOTES = {
    'Stand_RodHolder': 'Stand dressing: place within ~1 m of a fishing spot, facing the water. Two rod sleeves.',
    'Stand_Lantern': 'Place on the stand/deck. Add a Point Light at light_points[0] (warm, range 14, flicker); Stand_Lantern_Glow is the emissive glass (same pivot, own object, own emissive material).',
    'Stand_RopeCoil': 'Low prop, no collider needed.',
    'Berth_Piling_2m': 'Piling lengths 2-7 m. Pivot = the DECK DATUM (walking surface); the body runs from y = -N up to -0.17 (tile underside). World base = datum_y - N; pick the shortest N with N >= datum_y - ground_y + 0.3. Cylinder collider (r 0.15) is enough.',
    'Berth_DeckTile_1x1': 'Walking surface at y = 0 (datum); 0.16 m deep. Box collider = bounds. Boards run along local z.',
    'Berth_DeckTile_1x2': 'As the 1x1; 1 m (x) by 2 m (z). Pivot is the middle of the two cells.',
    'Berth_DeckTrim_1m': 'Fascia for the +z cell edge, proud by 0.045 m; hangs 0.30 m below the surface. Yaw it onto other edges.',
    'Berth_Stair_1m': 'Rise exactly 1.0 m over 2.0 m: top tread flush with the datum at local z +1 end, foot on a surface 1.0 m below the datum at local z -1. Ramp collider (or 4 step boxes) from the bounds.',
    'Berth_Ramp_1m': 'Rise 1.0 m over 4.0 m (slope 14 deg), high end (datum) at local z +2. Use a slope (wedge) collider.',
    'Berth_Rail_1m': 'Rail on the +z edge, inboard 0.045; top 1.0 m; the model includes its +x end post. A run of N cells needs N of these plus one Berth_RailPost at the start (set the post at the -x end by yaw). Collider: one thin box 1.0 x 1.0 x 0.1 per rail.',
    'Berth_RailPost': 'Start post for a rail run (at the cell\'s -x,+z corner).',
    'Berth_RoofPost': '2.5 m post at the cell\'s +x,+z corner; yaw 90/180/270 for the other corners. Roof panels sit on its top (y 2.5).',
    'Berth_RoofPanel_Flat': 'Corrugated sheet at y +2.5 built in (pivot is still the cell centre at the datum). No collider.',
    'Berth_RoofPanel_Pitched': 'Rises toward +z from +2.5 to +3.0. Put a second one yawed 180 in the next cell (z + 1) to make a gable; cover the join with Berth_RoofRidge_1m at the cell boundary (its pivot is the boundary line at the datum).',
    'Berth_RoofRidge_1m': 'Ridge cap, apex at +3.0, centred on the boundary line between the two cells of a gable (place it with the lower cell\'s x and z + 0.5).',
    'Berth_Bench_2m': '2 x 1 footprint, faces +z, back at the -z side. Seat height 0.45 m (sit point at local (0, 0.45, 0.0)).',
    'Berth_CleaningTable': '2 x 1, work height 0.92 m. Interaction: gut/fillet fish here (design: dockside cleaning).',
    'Berth_Smoker': '1 x 1 drum smoker, 1.5 m tall. Interaction: smoke fish (design: smoker multiplies value).',
    'Berth_CrabPotRack': '1 x 1 rack with three pots; ~1.4 m tall. Interaction: pots (design 3.8).',
    'Berth_RodRack': '1 x 1; five rods in the rack; holds rods the crew is not using.',
    'Berth_NameSign': 'text_surface in the manifest is the blank painted board: put the crew name (UI text) on that rectangle, facing +z. 2 x 1.',
    'Berth_StringLights_2m': '2 m span between hooks at (-1, 2.3, 0) and (1, 2.3, 0) (sag 0.28). Chain pieces end to end. Bulbs are Berth_StringLights_2m_Glow (emissive); two point lights in light_points.',
    'Berth_Bollard': 'Cast bollard, 0.52 m tall. The fixed mooring line uses it; the crew may add more.',
    'Berth_Cleat': 'Horn cleat; yaw so its long axis follows the rope.',
    'Berth_TyreFender': 'Pivot is the cell\'s +z edge midpoint at the datum; the tyre hangs 0.45 m down and 0.12 m proud of the edge.',
    'Berth_Pontoon_2x2': 'Heavy pontoon section: deck at the datum, floats from -0.2 to -1.7 (waterline is at y -1.25 for datum 1.25). The mooring line is six of these.',
    'Berth_Wall_1m': 'Shed wall on the cell\'s -z edge, 2.4 m high. Wall decor pivots at the wall face: place it at z = cell_z - 0.40, at the wanted height.',
    'Decor_Flamingo': 'Plastic lawn flamingo with sunglasses (palette slot 48 FlamingoPink). Pink is NEW palette slot 48 (FlamingoPink).',
    'Decor_BassPlaque': 'Wall piece (pivot back-bottom-centre). The singing mechanic is up to Unity: the red button is at local (0.2, 0.17, 0.08).',
    'Decor_InflatableShark': 'Sits on the ground/deck; 1.7 m long, faces +z. Could be pushed around by physics.',
    'Decor_TikiTorch': 'Light point at the cup; Decor_TikiTorch_Flame is the emissive flame (animate scale/flicker).',
    'Decor_BeerCooler': 'Lid is part of the mesh (closed); a can and a lime on top.',
    'Decor_PatioChair': 'White resin chair (slot 11).',
    'Decor_PatioChairStack': 'Three stacked chairs, same family.',
    'Decor_Hammock': '3 x 1 including its two posts; the fabric is double sided in the mesh (two layers). Sit/lie interaction is up to Unity.',
    'Decor_NoFishingSign': 'NO FISHING with NO painted over: it reads FISHING.',
    'Decor_NeonBait': 'Wall sign (pivot back-bottom-centre); Decor_NeonBait_Glow is the emissive tube set (letters + worm); light point in the manifest.',
    'Decor_BathtubPlanter': '1 x 2, rim 0.6 m; soil, shrubs and plastic flowers.',
    'Site_MooringLine': 'FIXED, authored in world coordinates: place the FBX at the Unity origin. Items list every pontoon section, bollard and fender. Colliders: a box over the pontoon (x 41..53, y 0.9..1.25, z -30..-28) plus a cylinder per bollard.',
    'Site_LaneBuoy_Port': 'Floating marker for the approach lane (west edge). Pivot at the waterline; bob in Unity.',
    'Site_LaneBuoy_Starboard': 'Floating marker for the approach lane (east edge).',
}

def manifest_for(groups, site_names):
    ents = []
    for g in groups:
        lo, hi = H.u_bounds(g)
        ex = g.extra
        d = dict(name=g.name, file='Art/Export/BerthKit/%s.fbx' % g.name, family=ex.get('kind'), triangles=g.tris(), vertices=len(g.V),
                 material='M_Toon_Base', bounds_min=lo, bounds_max=hi, slots=sorted(set(s for s, _ in g.SL)), layers=sorted(set(l for _, l in g.SL)))
        if g.name in site_names:
            d['pivot'] = 'world origin (authored at world coordinates)'
        elif ex.get('kind') == 'berth':
            d['pivot'] = 'footprint centre at the walking-surface datum (y 0 = where feet stand)'
            d['footprint_cells'] = list(ex.get('footprint') or (1, 1))
        else:
            d['pivot'] = 'base centre' if not g.name.startswith(('Decor_BassPlaque', 'Decor_NeonBait')) else 'back face, bottom centre; stands out toward +z'
        if ex.get('emissive'):
            d['material'] = 'M_Toon_Base + emissive (set the material to emit; palette slot is in uv1)'
            d['emissive'] = True
            d['companion_of'] = ex.get('companion_of')
        if ex.get('companion'):
            d['emissive_companion'] = ex['companion']
        if ex.get('light_points'):
            d['light_points'] = ex['light_points']
        if ex.get('text_surface'):
            d['text_surface'] = ex['text_surface']
        if ex.get('endpoints'):
            d['endpoints'] = ex['endpoints']
        if g.name in NOTES:
            d['notes'] = NOTES[g.name]
        if g.items:
            d['items'] = g.items
        ents.append(d)
    return ents

def build_site():
    out = {}
    for fn in (b_site_mooringline, lambda: b_site_laneboy('W'), lambda: b_site_laneboy('E')):
        g = fn()
        g.extra.update(kind='site', ground=False, footprint=None)
        out[g.name] = g
    return out

def main(export=True, previews=True, only=None):
    bpy.ops.wm.read_homefile(use_empty=True)
    groups = build_all()
    lib = {g.name: g for g in groups}
    site = build_site()
    # fixed placement of the site models (they are authored in world coordinates, except the lane buoys: local models + placements)
    for g in groups:
        if g.tris() > 1500:
            print('WARNING heavy model', g.name, g.tris())
    os.makedirs(EXPORT_DIR, exist_ok=True)
    kit_ao = bake_kit(groups)
    site_list = [site['Site_MooringLine']]
    site_ao = H.bake_ao(site_list)
    buoy_ao = bake_kit([site['Site_LaneBuoy_Port'], site['Site_LaneBuoy_Starboard']])
    report = []
    all_groups = groups + list(site.values())
    raws = kit_ao + site_ao + buoy_ao
    exports = []
    for g, raw in zip(all_groups, raws):
        ob = H.make_object(g)
        H.write_ao(ob, g, raw)
        path = os.path.join(EXPORT_DIR, g.name + '.fbx')
        res = H.export_if_changed(ob, path) if export else 'skipped'
        report.append('%-34s tris=%5d verts=%5d %s' % (g.name, g.tris(), len(g.V), res))
        exports.append(ob)
    total = sum(g.tris() for g in all_groups)
    pl = starter_landing()
    man = dict(
        generator='Art/Blender/fg_berthkit.py (imports fg_harbour.py for geometry helpers; does not modify it)',
        area='BerthKit', branch='harbour-berth-kit', date='2026-10-17',
        axes='Unity metres, authored in Unity axes (Blender = (-x, -z, y)); FBX settings and encoding as fg_harbour.py. Each FBX object is at the origin with an identity transform.',
        encoding=dict(uv0='UVMap: metres', uv1='Layer: (palette slot, texture layer)', colour='Col = 0.5 x baked AO (per vertex, floor 0.22-0.62 per part)',
                      normals='smooth / analytic', materials='none exported; M_Toon_Base for every model (no _Tri models in this kit)'),
        slots=dict(S, FlamingoPink=SLOT_PINK), layers=L,
        new_palette_slots=[dict(slot=SLOT_PINK, name='FlamingoPink', srgb=list(PINK_RGB), seasonal=False, used_by=['Decor_Flamingo', 'Decor_BathtubPlanter (one flower)'],
                                note='berth-kit range 48-55; Unity must add it to the palette (_FGPalette array grows past 16: see the orchestrator palette plan)')],
        palette_note='Palette slots (FishingSeedData.PaletteSeasons): only 0 Grass, 1 Foliage, 2 FoliageDark, 7 Reed, 14 Blossom and 15 FoliageAlt change with the season. '
                     'Slot 11 ("Snow", 0.95/0.96/0.98) is white all year: plastic, enamel, sign paint, teeth, eyes. NEW slot 48 FlamingoPink (see new_palette_slots) is the flamingo and one planter flower; slot 10 Accent (orange) is the neon, the bait bucket and the hammock stripes. '
                     'Reed (7, bamboo) and Foliage (1, bass and shrubs) follow the season on purpose.',
        grid=dict(
            cell_m=1.0,
            origin='cell corners on integer Unity x, z; a 1 x 1 piece pivots on (n + 0.5, m + 0.5); a 1 x 2 / 2 x 1 piece pivots on the middle of its two cells',
            yaw='multiples of 90 degrees; pieces face +z (stairs and ramps rise toward +z, rails/trim/roof posts sit on the +z edge or +x,+z corner)',
            datum='y = 0 in every Berth_ model is the walking surface. Placement y = datum_y. Tiles hang to -0.16, pilings from -N to -0.17.',
            datum_levels='datum_y(n) = 1.25 + n x 1.0 (n = -1 .. 3): 0.25 (beach landing), 1.25 (TUG DECK, the default), 2.25, 3.25, 4.25',
            vertical_pieces=dict(stair_rise=1.0, stair_run=2.0, ramp_rise=1.0, ramp_run=4.0, rail_top=1.0, rail_post=1.12, roof_post=2.5, roof_flat_y=2.5,
                                 roof_pitched_y=[2.5, 3.0], ridge_y=3.0, string_light_hook_y=2.3, wall_height=2.4, bench_seat=0.45, table_top=0.92),
            piling_rule='world piling base = datum_y - N; choose the shortest N in (2,3,4,5,6,7) with N >= datum_y - ground_y + 0.3',
            camps='same 1 m steps from the camp\'s own base level; structural pieces snap, decor is free inside the plot'),
        site=dict(
            location='east basin, south shore, between the lookout (x 34.7-39.5, z -51.2..-47.6) and the East Ledge jetty (x 54.4-57, z -51..-34)',
            coast='COAST_Z -45 (BeachY above, SeabedY = -0.25 (z + 45) below); ground at the plot: z -47 -> 0.24, z -45 -> 0.0, z -30 -> -3.75, z -28 -> -4.25',
            plot=PLOT, buildable=dict(x0=PLOT['x0'], x1=PLOT['x1'], z0=PLOT['z0'], z1=BUILD_Z1, note='the plot minus the fixed pontoon strip'),
            water_level_y=0.0,
            mooring_line=dict(z=MOOR_Z, pontoon=PONTOON, top_y=DATUM_TUG, bollards=[[x, DATUM_TUG, z] for x, z in BOLLARDS], fender_x=[round(41.9 + 1.5 * k, 2) for k in range(7)],
                              gangway_landing=[GANGWAY_LANDING['x'], DATUM_TUG, GANGWAY_LANDING['z']], model='Site_MooringLine',
                              depth_guarantee='seabed is 3.75 m below the water at the pontoon (SeabedY at z -30) and 4.25 m at its edge: the tug keel is 1.5 m below the waterline'),
            tug=dict(position=list(TUG_BERTH['position']), yaw=TUG_BERTH['yaw'], deck_y=H.TUG_DECK, length_m=H.TUG_L1 - H.TUG_L0, beam_m=H.TUG_B,
                     local_extent_z=[H.TUG_L0, H.TUG_L1], gangway_local_z=H.TUG_GANGWAY_Z, gangway_side='port',
                     note='bow west (yaw 270) so the tug\'s PORT side (the gangway side, local -x) faces the pontoon (south). Its centre line is at z -25.2: 0.3 m of fender gap '
                          'to the pontoon edge z -28.0 for half the beam TUG_B 5.0 (the brief said about 4 m; fg_harbour.py has TUG_B = 5.0, which this uses). '
                          'Hull spans world x 37.5 .. 53.0, z -27.7 .. -22.7; stern gangway lands at x 52.5.',
                     envelope=dict(x0=37.5, x1=53.0, z0=-27.7, z1=-22.7)),
            approach_lane=dict(rect=LANE, note='KEEP CLEAR. No placed piece or camp item may intersect this rectangle, nor the tug envelope above; the host validates a placement against it. '
                               'It opens onto the open basin (north); the mole is at z about -10. The plot ends at z -28, so only a piece pushed beyond the plot could block it.',
                               buoys=[dict(x=x, z=z, model='Site_LaneBuoy_Port' if sd == 'W' else 'Site_LaneBuoy_Starboard', y=0.0) for x, z, sd in LANE_BUOYS]),
            starter_landing=dict(note='Default placements (kit model, centre x, y, centre z, yaw). Pilings were sized from fg_harbour.land_y. All are removable build pieces.',
                                 placements=[dict(model=a, position=[round(b, 3), round(c, 3), round(d, 3)], yaw=e) for a, b, c, d, e in pl])),
        total_triangles=total,
        fbx=manifest_for(all_groups, set(site.keys())),
    )
    with open(MANIFEST_PATH, 'w', encoding='utf-8') as fh:
        json.dump(man, fh, indent=1)
    print('\n'.join(report))
    print('TOTAL', total)
    if previews:
        # previews compose the models (separate Group copies) in the empty scene
        for o in exports:
            o.hide_render = True
        build_previews(lib, site, PREVIEW_DIR, only)
    return groups

if __name__ == '__main__':
    _only = os.environ.get('BK_ONLY')
    main(export=os.environ.get('BK_NOEXPORT') is None, only=_only.split(',') if _only else None)
